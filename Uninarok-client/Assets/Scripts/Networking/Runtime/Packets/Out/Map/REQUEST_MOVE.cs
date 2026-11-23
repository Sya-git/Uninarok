using System;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Transport;
using Uninarok.Networking.Core.Wire;
using Uninarok.Networking.Data;

namespace Uninarok.Networking.Packets.Out.Map
{
    /// <summary>Client to map server, 0x035F (CZ_REQUEST_MOVE2), 5 bytes. Asks to walk to a cell; answered by <c>PLAYER_MOVE</c>, or by nothing when refused.</summary>
    [OutPacket(PacketIds.REQUEST_MOVE, Size)]
    public readonly struct REQUEST_MOVE : IOutgoingPacket
    {
        public const int Size = 5;

        public readonly UninarokPosition Destination;

        public REQUEST_MOVE(ushort x, ushort y)
        {
            Destination = new UninarokPosition(x, y, UninarokDirection.North);
        }

        public int WireSize => Size;

        public void Serialize(Span<byte> destination)
        {
            var w = new PacketWriter(destination);
            w.WriteUInt16(PacketIds.REQUEST_MOVE);
            Destination.Write(ref w);                        // the direction bits are ignored by the server
        }
    }
}
