using System;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Wire;

namespace Uninarok.Networking.Packets.In.Map
{
    /// <summary>
    /// Map server to client, 0x013B (ZC_ACTION_FAILURE), 4 bytes. An action failed; type 0 is an attack without arrows, which
    /// ends the attack (SPEC-0011 FR-2, plan D9).
    /// </summary>
    [InPacket(PacketIds.ACTION_FAILURE, Size)]
    public readonly struct ACTION_FAILURE
    {
        public const int Size = 4;

        public const ushort NoArrows = 0;

        public readonly ushort Type;

        public ACTION_FAILURE(ReadOnlySpan<byte> payload)
        {
            var r = new PacketReader(payload);
            Type = r.ReadUInt16();
            r.EnsureConsumed();
        }
    }
}
