using System;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Wire;

namespace Uninarok.Networking.Packets.In.Map
{
    /// <summary>Map server to client, 0x0ADD (<c>packet_dropflooritem</c>), 24 bytes. An item is dropped.</summary>
    [InPacket(PacketIds.ITEM_FALL_ENTRY, Size)]
    public readonly struct ITEM_FALL_ENTRY
    {
        public const int Size = 24;

        public readonly uint ItemObjectId;
        public readonly uint ItemId;
        public readonly ushort ItemType;
        public readonly bool Identified;
        public readonly short X;
        public readonly short Y;
        /// <summary>Sub-cell, twelfths (ITEM_FALL_ENTRY.md).</summary>
        public readonly byte SubX;
        public readonly byte SubY;
        public readonly short Amount;
        public readonly sbyte ShowDropEffect;
        public readonly short DropEffectMode;

        public ITEM_FALL_ENTRY(ReadOnlySpan<byte> payload)
        {
            var r = new PacketReader(payload);
            ItemObjectId = r.ReadUInt32();
            ItemId = r.ReadUInt32();
            ItemType = r.ReadUInt16();
            Identified = r.ReadByte() != 0;
            X = r.ReadInt16();
            Y = r.ReadInt16();
            SubX = r.ReadByte();
            SubY = r.ReadByte();
            Amount = r.ReadInt16();
            ShowDropEffect = (sbyte)r.ReadByte();
            DropEffectMode = r.ReadInt16();
            r.EnsureConsumed();
        }
    }
}
