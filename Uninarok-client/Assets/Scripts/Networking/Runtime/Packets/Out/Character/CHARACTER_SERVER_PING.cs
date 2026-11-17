using System;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Transport;
using Uninarok.Networking.Core.Wire;

namespace Uninarok.Networking.Packets.Out.Character
{
    /// <summary>
    /// Client to character server, 0x0187 (CH_PING), 6 bytes. Keepalive, sent every 12 seconds while connected to the
    /// character server (SPEC-0001 FR-14). Not the map keepalive (HEARTBEAT_REQUEST, 0x0360).
    /// </summary>
    [OutPacket(PacketIds.CHARACTER_SERVER_PING, Size)]
    public readonly struct CHARACTER_SERVER_PING : IOutgoingPacket
    {
        public const int Size = 6;

        public readonly uint AccountId;

        public CHARACTER_SERVER_PING(uint accountId)
        {
            AccountId = accountId;
        }

        public int WireSize => Size;

        public void Serialize(Span<byte> destination)
        {
            var w = new PacketWriter(destination);
            w.WriteUInt16(PacketIds.CHARACTER_SERVER_PING);
            w.WriteUInt32(AccountId);
        }
    }
}
