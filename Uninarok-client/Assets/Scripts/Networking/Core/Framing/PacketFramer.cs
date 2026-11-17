using System;

namespace Uninarok.Networking.Core.Framing
{
    /// <summary>
    /// Turns a TCP byte stream into packets (SPEC-0001 FR-1, FR-2, FR-3, FR-5, FR-6, FR-19; plan 4.3).
    /// The socket thread calls <see cref="Append"/>; the main thread calls <see cref="Process"/>.
    /// Double buffer (D1): <c>Append</c> fills a locked inbox; <c>Process</c> takes the lock only to move the inbox
    /// into a main-thread-only work buffer and parses that buffer with no lock held, so dispatch never blocks the socket thread.
    /// </summary>
    public sealed class PacketFramer : IInboundSink
    {
        public const int DefaultMaxBufferedBytes = 4 * 1024 * 1024;
        private const int InitialBufferBytes = 32 * 1024;

        private readonly IPacketLengthTable _lengths;
        private readonly IPacketRouter _router;
        private readonly int _maxBuffered;

        // Inbox: written by the socket thread, guarded by _lock.
        private readonly object _lock = new object();
        private byte[] _inbox;
        private int _inboxLength;

        // Work buffer: main thread only. Unparsed bytes are [_read, _write).
        private byte[] _work;
        private int _read;
        private int _write;
        private bool _stopped;
        private bool _faulted;
        private string _faultReason;

        public PacketFramer(IPacketLengthTable lengths, IPacketRouter router, int maxBufferedBytes = DefaultMaxBufferedBytes)
        {
            if (lengths == null) throw new ArgumentNullException(nameof(lengths));
            if (router == null) throw new ArgumentNullException(nameof(router));
            if (maxBufferedBytes < 1) throw new ArgumentOutOfRangeException(nameof(maxBufferedBytes));
            _lengths = lengths;
            _router = router;
            _maxBuffered = maxBufferedBytes;
            int initial = Math.Min(InitialBufferBytes, maxBufferedBytes);
            _inbox = new byte[initial];
            _work = new byte[initial];
        }

        /// <summary>Bytes received but not yet parsed (inbox plus unparsed work buffer). Main thread; for tests and diagnostics.</summary>
        public int PendingBytes
        {
            get
            {
                lock (_lock) return _inboxLength + (_write - _read);
            }
        }

        /// <summary>ANY thread. Returns false, storing nothing, when the inbox would exceed the buffer cap.</summary>
        public bool Append(ReadOnlySpan<byte> data)
        {
            if (data.Length == 0) return true;
            lock (_lock)
            {
                if (data.Length > _maxBuffered - _inboxLength) return false;
                int needed = _inboxLength + data.Length;
                if (needed > _inbox.Length) Array.Resize(ref _inbox, GrownSize(_inbox.Length, needed));
                data.CopyTo(new Span<byte>(_inbox, _inboxLength, data.Length));
                _inboxLength = needed;
            }
            return true;
        }

        /// <summary>True once a protocol fault was found. Main thread.</summary>
        public bool IsFaulted => _faulted;

        /// <summary>Why the stream faulted; null until then. Main thread.</summary>
        public string FaultReason => _faultReason;

        /// <summary>MAIN thread. Ends processing after the current packet; later <see cref="Process"/> calls return 0.</summary>
        public void Stop() => _stopped = true;

        /// <summary>
        /// MAIN thread. Frames up to <paramref name="maxPackets"/> packets and hands each to the router.
        /// Returns the number of packets framed (routed or skipped); 0 once faulted or stopped.
        /// The read position advances BEFORE routing, so if the router throws the framer is already consistent
        /// and the exception propagates to the caller.
        /// </summary>
        public int Process(int maxPackets)
        {
            if (_faulted || _stopped || maxPackets <= 0) return 0;
            Drain();

            int processed = 0;
            while (processed < maxPackets && !_stopped)
            {
                int available = _write - _read;
                if (available < 2) break;

                ushort id = (ushort)(_work[_read] | (_work[_read + 1] << 8));
                if (id == 0) { Fault("Packet ID 0"); break; }

                int length = _lengths.GetLength(id);
                if (length == -2) { Fault("Unknown packet 0x" + id.ToString("X4")); break; }
                if (length != -1 && length < 2) { Fault("Invalid table length " + length + " for packet 0x" + id.ToString("X4")); break; }

                int header = 2;
                if (length == -1)
                {
                    if (available < 4) break;
                    length = _work[_read + 2] | (_work[_read + 3] << 8);
                    if (length < 4) { Fault("Bad length " + length + " for packet 0x" + id.ToString("X4")); break; }
                    header = 4;
                }

                if (available < length) break;           // wait for the rest of the packet

                int payloadStart = _read + header;
                int payloadLength = length - header;
                _read += length;                          // advance first: the router may throw or call Stop
                processed++;

                // false = known ID without a handler: already skipped by its length (FR-19).
                _router.Route(id, new ReadOnlySpan<byte>(_work, payloadStart, payloadLength));
            }

            if (_read == _write) { _read = 0; _write = 0; }
            return processed;
        }

        private void Fault(string reason)
        {
            _faulted = true;
            _faultReason = reason;
        }

        // Moves as much of the inbox as the cap allows into the work buffer. Lock is held only for the copy.
        private void Drain()
        {
            lock (_lock)
            {
                if (_inboxLength == 0) return;
                int unread = _write - _read;
                int move = Math.Min(_inboxLength, _maxBuffered - unread);
                if (move <= 0) return;

                EnsureWorkRoom(move);
                Buffer.BlockCopy(_inbox, 0, _work, _write, move);
                _write += move;

                _inboxLength -= move;
                if (_inboxLength > 0) Buffer.BlockCopy(_inbox, move, _inbox, 0, _inboxLength);
            }
        }

        // Makes room for 'extra' bytes at the end of the work buffer: compact first, then grow by doubling (up to the cap).
        private void EnsureWorkRoom(int extra)
        {
            if (_work.Length - _write >= extra) return;

            int unread = _write - _read;
            if (_read > 0)
            {
                Buffer.BlockCopy(_work, _read, _work, 0, unread);   // overlapping copy is handled by BlockCopy
                _read = 0;
                _write = unread;
            }
            if (_work.Length - _write < extra)
                Array.Resize(ref _work, GrownSize(_work.Length, _write + extra));
        }

        // Doubles until 'needed' fits, never beyond the cap (callers guarantee needed <= cap).
        private int GrownSize(int current, int needed)
        {
            long size = Math.Max(current, 1);
            while (size < needed) size *= 2;
            return (int)Math.Min(size, Math.Max(_maxBuffered, needed));
        }
    }
}
