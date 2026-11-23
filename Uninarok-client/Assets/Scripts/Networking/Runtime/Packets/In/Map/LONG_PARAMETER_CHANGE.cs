using System;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Wire;

namespace Uninarok.Networking.Packets.In.Map
{
    /// <summary>
    /// Map server to client, 0x0ACB (ZC_LONGLONGPAR_CHANGE), 12 bytes. A 64-bit status value of the own character: EXP and
    /// zeny (SPEC-0011 FR-1).
    /// </summary>
    [InPacket(PacketIds.LONG_PARAMETER_CHANGE, Size)]
    public readonly struct LONG_PARAMETER_CHANGE
    {
        public const int Size = 12;

        public const ushort BaseExp = 1;
        public const ushort JobExp = 2;
        public const ushort Zeny = 20;
        public const ushort NextBaseExp = 22;
        public const ushort NextJobExp = 23;

        public readonly ushort VarId;
        public readonly long Value;

        public LONG_PARAMETER_CHANGE(ReadOnlySpan<byte> payload)
        {
            var r = new PacketReader(payload);
            VarId = r.ReadUInt16();
            Value = r.ReadInt64();
            r.EnsureConsumed();
        }
    }
}
