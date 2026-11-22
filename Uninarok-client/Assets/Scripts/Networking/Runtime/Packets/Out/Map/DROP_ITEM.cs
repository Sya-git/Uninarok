using System;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Transport;
using Uninarok.Networking.Core.Wire;

namespace Uninarok.Networking.Packets.Out.Map
{
    /// <summary>Client to map server, 0x0363 (CZ_ITEM_THROW), 6 bytes. Drops items of the inventory onto the ground.</summary>
    [OutPacket(PacketIds.DROP_ITEM, Size)]
    public readonly struct DROP_ITEM : IOutgoingPacket
    {
        public const int Size = 6;

        /// <summary>As the server sends it: the inventory slot plus 2.</summary>
        public readonly ushort InventoryIndex;
        public readonly ushort Amount;

        public DROP_ITEM(ushort inventoryIndex, ushort amount)
        {
            InventoryIndex = inventoryIndex;
            Amount = amount;
        }

        public int WireSize => Size;

        public void Serialize(Span<byte> destination)
        {
            var w = new PacketWriter(destination);
            w.WriteUInt16(PacketIds.DROP_ITEM);
            w.WriteUInt16(InventoryIndex);
            w.WriteUInt16(Amount);
        }
    }
}
