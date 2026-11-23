using System;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Wire;

namespace Uninarok.Networking.Packets.In.Map
{
    /// <summary>
    /// Map server to client, 0x0139 (ZC_ATTACK_FAILURE_FOR_DISTANCE), 16 bytes. The own character's attack was refused: the
    /// target is too far. Both cells as the server sees them, and the range (SPEC-0011 FR-3).
    /// </summary>
    [InPacket(PacketIds.ATTACK_TOO_FAR, Size)]
    public readonly struct ATTACK_TOO_FAR
    {
        public const int Size = 16;

        public readonly uint TargetId;
        public readonly short TargetX, TargetY;
        /// <summary>The own cell on the server.</summary>
        public readonly short X, Y;
        public readonly short Range;

        public ATTACK_TOO_FAR(ReadOnlySpan<byte> payload)
        {
            var r = new PacketReader(payload);
            TargetId = r.ReadUInt32();
            TargetX = r.ReadInt16();
            TargetY = r.ReadInt16();
            X = r.ReadInt16();
            Y = r.ReadInt16();
            Range = r.ReadInt16();
            r.EnsureConsumed();
        }
    }
}
