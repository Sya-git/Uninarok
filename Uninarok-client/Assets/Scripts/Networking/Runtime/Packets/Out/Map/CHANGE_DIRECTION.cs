using System;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Transport;
using Uninarok.Networking.Core.Wire;
using Uninarok.Networking.Data;

namespace Uninarok.Networking.Packets.Out.Map
{
    /// <summary>Client to map server, 0x0361 (CZ_CHANGE_DIRECTION2), 5 bytes. Turns the own character in place; the server does not echo it.</summary>
    [OutPacket(PacketIds.CHANGE_DIRECTION, Size)]
    public readonly struct CHANGE_DIRECTION : IOutgoingPacket
    {
        public const int Size = 5;

        /// <summary>0 straight, 1 right, 2 left.</summary>
        public readonly ushort HeadDirection;
        public readonly UninarokDirection Direction;

        public CHANGE_DIRECTION(ushort headDirection, UninarokDirection direction)
        {
            HeadDirection = headDirection;
            Direction = direction;
        }

        public int WireSize => Size;

        public void Serialize(Span<byte> destination)
        {
            var w = new PacketWriter(destination);
            w.WriteUInt16(PacketIds.CHANGE_DIRECTION);
            w.WriteUInt16(HeadDirection);
            w.WriteByte((byte)Direction);
        }
    }
}
