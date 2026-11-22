using System;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Wire;

namespace Uninarok.Networking.Packets.In.Map
{
    /// <summary>Map server to client, 0x01FF (ZC_HIGHJUMP), 10 bytes. A unit moves to a cell at once, without walking.</summary>
    [InPacket(PacketIds.UNIT_SLIDE, Size)]
    public readonly struct UNIT_SLIDE
    {
        public const int Size = 10;

        public readonly uint UnitId;
        public readonly ushort X;
        public readonly ushort Y;

        public UNIT_SLIDE(ReadOnlySpan<byte> payload)
        {
            var r = new PacketReader(payload);
            UnitId = r.ReadUInt32();
            X = r.ReadUInt16();
            Y = r.ReadUInt16();
            r.EnsureConsumed();
        }
    }
}
