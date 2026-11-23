using System;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Wire;

namespace Uninarok.Networking.Packets.In.Map
{
    /// <summary>
    /// Map server to client, 0x0B41 (<c>PACKET_ZC_ITEM_PICKUP_ACK</c>), 70 bytes. An item came into the inventory (or could not).
    /// What nothing uses yet (cards, options, refine) is skipped.
    /// </summary>
    [InPacket(PacketIds.ITEM_PICKUP_ACK, Size)]
    public readonly struct ITEM_PICKUP_ACK
    {
        public const int Size = 70;

        /// <summary>The inventory slot plus 2.</summary>
        public readonly ushort InventoryIndex;
        /// <summary>The amount added.</summary>
        public readonly ushort Amount;
        public readonly uint ItemId;
        public readonly bool Identified;
        public readonly byte ItemType;
        /// <summary>0 success, else the failure.</summary>
        public readonly byte Result;

        public ITEM_PICKUP_ACK(ReadOnlySpan<byte> payload)
        {
            var r = new PacketReader(payload);
            InventoryIndex = r.ReadUInt16();
            Amount = r.ReadUInt16();
            ItemId = r.ReadUInt32();
            Identified = r.ReadByte() != 0;
            r.Skip(1 + 16 + 4); // damaged, cards, location
            ItemType = r.ReadByte();
            Result = r.ReadByte();
            r.Skip(4 + 2 + 25 + 1 + 2 + 1 + 1); // expiry, bind, options, favourite, look, refine, grade
            r.EnsureConsumed();
        }
    }
}
