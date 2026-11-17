using System;
using System.Collections.Generic;
using Uninarok.Networking.Core.Framing;

namespace Uninarok.Networking.Tests
{
    /// <summary>Thread-safe inbound sink that stores everything it receives. Can be told to refuse data (buffer overflow).</summary>
    public sealed class CollectingSink : IInboundSink
    {
        private readonly object _lock = new object();
        private readonly List<byte> _bytes = new List<byte>();

        /// <summary>When true, Append stores nothing and returns false, like a framer over its buffer cap.</summary>
        public volatile bool Refuse;

        public bool Append(ReadOnlySpan<byte> data)
        {
            if (Refuse) return false;
            lock (_lock) _bytes.AddRange(data.ToArray());
            return true;
        }

        public int Count { get { lock (_lock) return _bytes.Count; } }

        public byte[] Snapshot() { lock (_lock) return _bytes.ToArray(); }
    }
}
