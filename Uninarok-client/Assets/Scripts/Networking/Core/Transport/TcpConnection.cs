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
    public sealed partial class TcpConnection : IDisposable
    {
        private readonly TcpClient _client;
        private readonly Socket _socket;
        private readonly IInboundSink _sink;
        private readonly AutoResetEvent _outSignal = new AutoResetEvent(false);

        private CloseInfo _closure;
        private int _liveThreads;

        private TcpConnection(TcpClient client, ConnectionRole role, IInboundSink sink)
        {
            _client = client;
            _socket = client.Client;
            Role = role;
            _sink = sink;
        }

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
            finally { ThreadExited(); }
        }

        private void WriteLoop()
        {
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
            finally { ThreadExited(); }
        }

        // Writes whatever the outbox holds. Implemented with the outbox itself (T3.7).
        partial void WriteQueued();

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

        // The last thread to leave releases the wake-up signal, so the writer never waits on a disposed handle.
        private void ThreadExited()
        {
            if (Interlocked.Decrement(ref _liveThreads) != 0) return;
            try { _outSignal.Dispose(); } catch (Exception) { }
        }
    }
}
