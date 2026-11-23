using System;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Wire;

namespace Uninarok.Networking.Packets.In.Map
{
    /// <summary>Map server to client, 0x009C (ZC_CHANGE_DIRECTION), 9 bytes. A unit turned; not sent to the player who turned itself.</summary>
    [InPacket(PacketIds.UNIT_DIRECTION, Size)]
    public readonly struct UNIT_DIRECTION
    {
        public const int Size = 9;

        public readonly uint UnitId;
        /// <summary>0 straight, 1 right, 2 left.</summary>
        public readonly ushort HeadDirection;
        /// <summary>Body direction, 0 to 7.</summary>
        public readonly byte Direction;

        public UNIT_DIRECTION(ReadOnlySpan<byte> payload)
        {
            var r = new PacketReader(payload);
            UnitId = r.ReadUInt32();
            HeadDirection = r.ReadUInt16();
            Direction = r.ReadByte();
            r.EnsureConsumed();
        }
    }
}
