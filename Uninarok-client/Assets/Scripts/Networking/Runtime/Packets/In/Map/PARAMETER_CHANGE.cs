using System;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Wire;

namespace Uninarok.Networking.Packets.In.Map
{
    /// <summary>Map server to client, 0x00B0 (ZC_PAR_CHANGE), 8 bytes. A status value of the own character changed.</summary>
    [InPacket(PacketIds.PARAMETER_CHANGE, Size)]
    public readonly struct PARAMETER_CHANGE
    {
        public const int Size = 8;

        /// <summary>0 is the walk speed.</summary>
        public readonly ushort VarId;
        public readonly int Value;

        public PARAMETER_CHANGE(ReadOnlySpan<byte> payload)
        {
            var r = new PacketReader(payload);
            VarId = r.ReadUInt16();
            Value = r.ReadInt32();
            r.EnsureConsumed();
        }
    }
}
