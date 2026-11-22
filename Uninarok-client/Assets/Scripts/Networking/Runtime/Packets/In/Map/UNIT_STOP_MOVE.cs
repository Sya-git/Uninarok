using System;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Wire;

namespace Uninarok.Networking.Packets.In.Map
{
    /// <summary>Map server to client, 0x0088 (ZC_STOPMOVE), 10 bytes. A unit stops on a cell.</summary>
    [InPacket(PacketIds.UNIT_STOP_MOVE, Size)]
    public readonly struct UNIT_STOP_MOVE
    {
        public const int Size = 10;

        public readonly uint UnitId;
        public readonly ushort X;
        public readonly ushort Y;

        public UNIT_STOP_MOVE(ReadOnlySpan<byte> payload)
        {
            var r = new PacketReader(payload);
            UnitId = r.ReadUInt32();
            X = r.ReadUInt16();
            Y = r.ReadUInt16();
            r.EnsureConsumed();
        }
    }
}
