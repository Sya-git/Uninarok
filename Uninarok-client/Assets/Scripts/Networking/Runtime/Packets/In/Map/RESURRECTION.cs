using System;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Wire;

namespace Uninarok.Networking.Packets.In.Map
{
    /// <summary>Map server to client, 0x0148 (ZC_RESURRECTION), 8 bytes. A dead unit stands up where it lies (SPEC-0011 FR-8, FR-9).</summary>
    [InPacket(PacketIds.RESURRECTION, Size)]
    public readonly struct RESURRECTION
    {
        public const int Size = 8;

        public readonly uint UnitId;
        /// <summary>Always 0.</summary>
        public readonly ushort Type;

        public RESURRECTION(ReadOnlySpan<byte> payload)
        {
            var r = new PacketReader(payload);
            UnitId = r.ReadUInt32();
            Type = r.ReadUInt16();
            r.EnsureConsumed();
        }
    }
}
