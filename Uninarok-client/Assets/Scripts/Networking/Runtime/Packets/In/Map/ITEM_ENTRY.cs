using System;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Wire;

namespace Uninarok.Networking.Packets.In.Map
{
    /// <summary>Map server to client, 0x009D (ZC_ITEM_ENTRY), 19 bytes. An item already on the ground enters view.</summary>
    [InPacket(PacketIds.ITEM_ENTRY, Size)]
    public readonly struct ITEM_ENTRY
    {
        public const int Size = 19;

        public readonly uint ItemObjectId;
        public readonly uint ItemId;
        public readonly bool Identified;
        public readonly ushort X;
        public readonly ushort Y;
        public readonly ushort Amount;
        /// <summary>Sub-cell, twelfths (ITEM_FALL_ENTRY.md).</summary>
        public readonly byte SubX;
        public readonly byte SubY;

        public ITEM_ENTRY(ReadOnlySpan<byte> payload)
        {
            var r = new PacketReader(payload);
            ItemObjectId = r.ReadUInt32();
            ItemId = r.ReadUInt32();
            Identified = r.ReadByte() != 0;
            X = r.ReadUInt16();
            Y = r.ReadUInt16();
            Amount = r.ReadUInt16();
            SubX = r.ReadByte();
            SubY = r.ReadByte();
            r.EnsureConsumed();
        }
    }
}
