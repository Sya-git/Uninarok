using System;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Wire;

namespace Uninarok.Networking.Packets.In.Map
{
    /// <summary>Map server to client, 0x00AF (ZC_ITEM_THROW_ACK), 6 bytes. Items left the inventory: dropped or deleted.</summary>
    [InPacket(PacketIds.DROP_ITEM_ACK, Size)]
    public readonly struct DROP_ITEM_ACK
    {
        public const int Size = 6;

        /// <summary>The inventory slot plus 2.</summary>
        public readonly ushort InventoryIndex;
        public readonly ushort Amount;

        public DROP_ITEM_ACK(ReadOnlySpan<byte> payload)
        {
            var r = new PacketReader(payload);
            InventoryIndex = r.ReadUInt16();
            Amount = r.ReadUInt16();
            r.EnsureConsumed();
        }
    }
}
