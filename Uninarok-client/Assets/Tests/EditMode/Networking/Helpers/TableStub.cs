using System.Collections.Generic;
using Uninarok.Networking.Core.Framing;

namespace Uninarok.Networking.Tests
{
    /// <summary>Table-driven length table for framer tests. Unlisted IDs are unknown (-2).</summary>
    public sealed class TableStub : IPacketLengthTable
    {
        private readonly Dictionary<ushort, int> _lengths = new Dictionary<ushort, int>();

        public TableStub Fixed(ushort id, int totalLength) { _lengths[id] = totalLength; return this; }
        public TableStub Variable(ushort id) { _lengths[id] = -1; return this; }
        /// <summary>Registers any raw value, including invalid ones (0, 1, below -1) to test the framer's guard.</summary>
        public TableStub Raw(ushort id, int value) { _lengths[id] = value; return this; }

        public int GetLength(ushort packetId) => _lengths.TryGetValue(packetId, out int len) ? len : -2;
    }
}
