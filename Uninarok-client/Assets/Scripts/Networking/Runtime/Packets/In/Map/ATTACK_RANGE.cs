using System;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Wire;

namespace Uninarok.Networking.Packets.In.Map
{
    /// <summary>Map server to client, 0x013A (ZC_ATTACK_RANGE), 4 bytes. The own character's attack range in cells (SPEC-0011 FR-3).</summary>
    [InPacket(PacketIds.ATTACK_RANGE, Size)]
    public readonly struct ATTACK_RANGE
    {
        public const int Size = 4;

        public readonly short Range;

        public ATTACK_RANGE(ReadOnlySpan<byte> payload)
        {
            var r = new PacketReader(payload);
            Range = r.ReadInt16();
            r.EnsureConsumed();
        }
    }
}
