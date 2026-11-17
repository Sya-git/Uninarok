using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Uninarok.Networking.Core.Framing;

namespace Uninarok.Networking.Core.Transport
{
    /// <summary>
    /// One TCP connection to a Hercules server (SPEC-0001 FR-2, FR-6, FR-7, FR-8; plan 4.4).
    /// A dedicated reader thread blocks in <c>Receive</c> and hands bytes to the <see cref="IInboundSink"/>; a dedicated
    /// writer thread drains the outbox. Neither thread touches the engine or game code.
    /// Closure is one immutable <see cref="CloseInfo"/> set exactly once (first close wins).
    /// </summary>
    public sealed class TcpConnection : IDisposable
    {
        private readonly TcpClient _client;
        private readonly Socket _socket;
        private readonly IInboundSink _sink;
        private readonly AutoResetEvent _outSignal = new AutoResetEvent(false);

        private CloseInfo _closure;
        private int _liveThreads;

        // Outbox: appended by Send under a short lock; the writer swaps it with its own buffer and writes with no lock held.
        private const int MaxSendBacklogBytes = 1024 * 1024;
        private readonly object _outLock = new object();
        private byte[] _outbox = new byte[16 * 1024];
        private byte[] _writeBuf = new byte[16 * 1024];
        private int _outLength;
        private bool _writerIdle = true;            // guarded by _outLock

        private TcpConnection(TcpClient client, ConnectionRole role, IInboundSink sink)
        {
            _client = client;
            _socket = client.Client;
            Role = role;
            _sink = sink;
        }

        /// <summary>
        /// Called on each socket thread as it starts, with the thread's name (for example <c>Uninarok.Net.Login.Read</c>).
        /// Core is engine-free, so the Runtime assembly installs the Profiler registration here once, before any connection
        /// opens (SPEC-0001 T8.1). Never touched from the socket threads except to call it.
        /// </summary>
        public static Action<string> SocketThreadStarted;

        /// <summary>Called on each socket thread just before it exits, as the counterpart of <see cref="SocketThreadStarted"/>.</summary>
        public static Action SocketThreadStopping;

        public ConnectionRole Role { get; }

        /// <summary>True once the connection closed, for any reason.</summary>
        public bool IsClosed => Volatile.Read(ref _closure) != null;

        /// <summary>Null until closed. The first close wins.</summary>
        public CloseInfo Closure => Volatile.Read(ref _closure);

        /// <summary>Number of socket threads still running (2 while open). For tests and diagnostics.</summary>
        public int LiveThreads => Volatile.Read(ref _liveThreads);

        /// <summary>
        /// Resolves the host, connects with <c>NoDelay</c>, then starts the reader and writer threads.
        /// One-shot (may allocate). On cancellation the socket is disposed and <see cref="OperationCanceledException"/> is thrown.
        /// Never touches the engine.
        /// </summary>
        public static async Task<TcpConnection> ConnectAsync(string host, ushort port, ConnectionRole role,
                                                             IInboundSink sink, CancellationToken token)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (sink == null) throw new ArgumentNullException(nameof(sink));
            token.ThrowIfCancellationRequested();

            var client = new TcpClient { NoDelay = true };
            try
            {
                using (token.Register(() => client.Dispose()))
                {
                    await client.ConnectAsync(host, port).ConfigureAwait(false);
                }
                token.ThrowIfCancellationRequested();
            }
            catch (Exception) when (token.IsCancellationRequested)
            {
                client.Dispose();
                throw new OperationCanceledException(token);
            }
            catch
            {
                client.Dispose();
                throw;
            }

            var connection = new TcpConnection(client, role, sink);
            connection.StartThreads();
            return connection;
        }

        private void StartThreads()
        {
            _liveThreads = 2;                       // set before either thread can exit
            Start("Uninarok.Net." + Role + ".Read", ReadLoop);
            Start("Uninarok.Net." + Role + ".Write", WriteLoop);
        }

        private static void Start(string name, ThreadStart body)
        {
            new Thread(body) { Name = name, IsBackground = true }.Start();
        }

        private void ReadLoop()
        {
            NotifyThreadStarted();
            try
            {
                var buffer = new byte[32 * 1024];   // allocated once per connection
                while (true)
                {
                    int n = _socket.Receive(buffer, 0, buffer.Length, SocketFlags.None);
                    if (n == 0)
                    {
                        Close(CloseReason.RemoteClosed, "Server closed the connection");
                        return;
                    }
                    if (!_sink.Append(new ReadOnlySpan<byte>(buffer, 0, n)))
                    {
                        Close(CloseReason.ProtocolFault, "Receive buffer overflow");
                        return;
                    }
                }
            }
            catch (Exception) when (IsClosed) { }   // our own shutdown: stay quiet (FR-7)
            catch (Exception e) { Close(CloseReason.Error, e.Message); }
            finally { NotifyThreadStopping(); ThreadExited(); }
        }

        private void WriteLoop()
        {
            NotifyThreadStarted();
            try
            {
                while (true)
                {
                    _outSignal.WaitOne();
                    if (IsClosed) return;
                    WriteQueued();
                }
            }
            catch (Exception) when (IsClosed) { }
            catch (Exception e) { Close(CloseReason.Error, e.Message); }
            finally { NotifyThreadStopping(); ThreadExited(); }
        }

        // Drains the outbox: swap buffers under the lock, write with no lock held, until it is empty.
        private void WriteQueued()
        {
            while (true)
            {
                int n;
                lock (_outLock)
                {
                    if (_outLength == 0) { _writerIdle = true; return; }
                    byte[] tmp = _outbox; _outbox = _writeBuf; _writeBuf = tmp;
                    n = _outLength;
                    _outLength = 0;
                }
                for (int sent = 0; sent < n;)
                    sent += _socket.Send(_writeBuf, sent, n - sent, SocketFlags.None);
            }
        }

        /// <summary>
        /// Any thread, normally the main thread. Never blocks on I/O and never throws because of connection state.
        /// Returns true when the packet was QUEUED (not "sent"); false when it was not queued (connection closed, or the
        /// backlog limit was hit, which also closes the connection). A packet is committed whole or not at all, and the
        /// order on the wire is the order of the calls. An empty packet is a no-op. If <c>Serialize</c> throws (a programmer
        /// error) the exception propagates and nothing is queued.
        /// </summary>
        public bool Send<T>(in T packet) where T : struct, IOutgoingPacket
        {
            int size = packet.WireSize;
            if (size <= 0) return true;
            bool overflow = false;
            lock (_outLock)
            {
                if (IsClosed) return false;
                if (_outLength + size > MaxSendBacklogBytes) overflow = true;
                else
                {
                    if (_outLength + size > _outbox.Length) Array.Resize(ref _outbox, GrownOutboxSize(_outLength + size));
                    packet.Serialize(new Span<byte>(_outbox, _outLength, size));   // if this throws, _outLength is not advanced
                    _outLength += size;
                    if (!WakeWriterIfIdle()) return false;
                }
            }
            if (overflow)
            {
                Close(CloseReason.Error, "Send backlog exceeded");
                return false;
            }
            return true;
        }

        /// <summary>Queues raw bytes with the same rules as <see cref="Send{T}"/>.</summary>
        public bool Send(ReadOnlySpan<byte> rawPacket)
        {
            int size = rawPacket.Length;
            if (size <= 0) return true;
            bool overflow = false;
            lock (_outLock)
            {
                if (IsClosed) return false;
                if (_outLength + size > MaxSendBacklogBytes) overflow = true;
                else
                {
                    if (_outLength + size > _outbox.Length) Array.Resize(ref _outbox, GrownOutboxSize(_outLength + size));
                    rawPacket.CopyTo(new Span<byte>(_outbox, _outLength, size));
                    _outLength += size;
                    if (!WakeWriterIfIdle()) return false;
                }
            }
            if (overflow)
            {
                Close(CloseReason.Error, "Send backlog exceeded");
                return false;
            }
            return true;
        }

        // Called under _outLock. The signal is set only on the idle -> busy transition: no lost wake-up, no redundant signals.
        private bool WakeWriterIfIdle()
        {
            if (!_writerIdle) return true;
            _writerIdle = false;
            try { _outSignal.Set(); }
            catch (ObjectDisposedException) { return false; }   // closed and fully shut down in the meantime
            return true;
        }

        private static int GrownOutboxSize(int needed)
        {
            long size = 16 * 1024;
            while (size < needed) size *= 2;
            return (int)Math.Min(size, MaxSendBacklogBytes);
        }

        /// <summary>Any thread. Idempotent: only the first close counts.</summary>
        public void Close(CloseReason reason, string message)
        {
            var info = new CloseInfo(reason, message);
            if (Interlocked.CompareExchange(ref _closure, info, null) != null) return;

            // Closure is recorded first, so the exception a blocked thread gets from the closed socket is silent (FR-7).
            // Shutdown first, then close: on Unity's Mono, Shutdown alone does not wake a blocked Receive (plan D17).
            try { _socket.Shutdown(SocketShutdown.Both); } catch (Exception) { }
            try { _client.Dispose(); } catch (Exception) { }
            try { _outSignal.Set(); } catch (ObjectDisposedException) { }   // wake the writer
        }

        /// <summary>Closes as <see cref="CloseReason.LocalDispose"/> (silent). Idempotent.</summary>
        public void Dispose() => Close(CloseReason.LocalDispose, null);

        // A hook that throws must never take a socket thread down.
        private static void NotifyThreadStarted()
        {
            try { SocketThreadStarted?.Invoke(Thread.CurrentThread.Name); } catch (Exception) { }
        }

        private static void NotifyThreadStopping()
        {
            try { SocketThreadStopping?.Invoke(); } catch (Exception) { }
        }

        // The last thread to leave releases the wake-up signal, so the writer never waits on a disposed handle.
        private void ThreadExited()
        {
            if (Interlocked.Decrement(ref _liveThreads) != 0) return;
            try { _outSignal.Dispose(); } catch (Exception) { }
        }
    }
}
