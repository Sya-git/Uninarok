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

        /// <summary>MAIN thread. Moves the inbox into the work buffer.</summary>
        public int Process(int maxPackets)
        {
            Drain();
            return 0; // T2.8 implements parsing.
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
