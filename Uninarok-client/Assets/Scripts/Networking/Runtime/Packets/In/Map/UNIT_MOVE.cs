using System;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Wire;
using Uninarok.Networking.Data;

namespace Uninarok.Networking.Packets.In.Map
{
    /// <summary>Map server to client, 0x0086 (ZC_NOTIFY_MOVE), 16 bytes. A unit in view walks; the path between the two cells is the client's to find.</summary>
    [InPacket(PacketIds.UNIT_MOVE, Size)]
    public readonly struct UNIT_MOVE
    {
        public const int Size = 16;

        public readonly uint UnitId;
        public readonly UninarokMoveData Move;
        /// <summary>Server tick when the packet was sent: the start of the walk.</summary>
        public readonly uint StartTick;

        public UNIT_MOVE(ReadOnlySpan<byte> payload)
        {
            var r = new PacketReader(payload);
            UnitId = r.ReadUInt32();
            Move = UninarokMoveData.Read(ref r);
            StartTick = r.ReadUInt32();
            r.EnsureConsumed();
        }
    }
}
