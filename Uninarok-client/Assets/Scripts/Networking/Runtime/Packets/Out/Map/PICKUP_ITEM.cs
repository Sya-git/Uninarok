using System;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Transport;
using Uninarok.Networking.Core.Wire;

namespace Uninarok.Networking.Packets.Out.Map
{
    /// <summary>
    /// Client to map server, 0x0362 (CZ_ITEM_PICKUP2), 6 bytes. Picks up a floor item within 2 cells of the own server cell
    /// (SPEC-0011 FR-10).
    /// </summary>
    [OutPacket(PacketIds.PICKUP_ITEM, Size)]
    public readonly struct PICKUP_ITEM : IOutgoingPacket
    {
        public const int Size = 6;

        /// <summary>Object ID of the floor item.</summary>
        public readonly uint ItemObjectId;

        public PICKUP_ITEM(uint itemObjectId) => ItemObjectId = itemObjectId;

        public int WireSize => Size;

        public void Serialize(Span<byte> destination)
        {
            var w = new PacketWriter(destination);
            w.WriteUInt16(PacketIds.PICKUP_ITEM);
            w.WriteUInt32(ItemObjectId);
        }
    }
}
