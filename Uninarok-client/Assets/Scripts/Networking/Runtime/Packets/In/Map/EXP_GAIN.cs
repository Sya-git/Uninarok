using System;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Wire;

namespace Uninarok.Networking.Packets.In.Map
{
    /// <summary>
    /// Map server to client, 0x0ACC (ZC_NOTIFY_EXP2), 18 bytes. EXP gained by the own character, for display only; the
    /// totals come from <see cref="LONG_PARAMETER_CHANGE"/> (SPEC-0011 FR-11).
    /// </summary>
    [InPacket(PacketIds.EXP_GAIN, Size)]
    public readonly struct EXP_GAIN
    {
        public const int Size = 18;

        public readonly uint UnitId;
        public readonly long Amount;
        /// <summary>1 base EXP, 2 job EXP.</summary>
        public readonly ushort VarId;
        /// <summary>1 quest EXP, 0 otherwise.</summary>
        public readonly ushort Quest;

        public EXP_GAIN(ReadOnlySpan<byte> payload)
        {
            var r = new PacketReader(payload);
            UnitId = r.ReadUInt32();
            Amount = r.ReadInt64();
            VarId = r.ReadUInt16();
            Quest = r.ReadUInt16();
            r.EnsureConsumed();
        }
    }
}
