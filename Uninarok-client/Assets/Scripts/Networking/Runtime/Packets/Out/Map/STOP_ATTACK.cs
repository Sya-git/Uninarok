using System;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Transport;
using Uninarok.Networking.Core.Wire;

namespace Uninarok.Networking.Packets.Out.Map
{
    /// <summary>Client to map server, 0x0118 (CZ_CANCEL_LOCKON), 2 bytes: header only. Stops the own continuous attack (SPEC-0011 FR-2).</summary>
    [OutPacket(PacketIds.STOP_ATTACK, Size)]
    public readonly struct STOP_ATTACK : IOutgoingPacket
    {
        public const int Size = 2;

        public int WireSize => Size;

        public void Serialize(Span<byte> destination)
        {
            var w = new PacketWriter(destination);
            w.WriteUInt16(PacketIds.STOP_ATTACK);
        }
    }
}
