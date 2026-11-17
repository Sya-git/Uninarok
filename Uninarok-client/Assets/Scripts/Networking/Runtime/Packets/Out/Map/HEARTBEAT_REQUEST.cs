using System;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Transport;
using Uninarok.Networking.Core.Wire;

namespace Uninarok.Networking.Packets.Out.Map
{
    /// <summary>
    /// Client to map server, 0x0360 (CZ_REQUEST_TIME2), 6 bytes. Map keepalive, sent every 10 seconds once in game
    /// (SPEC-0001 FR-14). Answered by <c>SERVER_HEARTBEAT_RESPONSE</c>. Not the character-server ping (0x0187).
    /// </summary>
    [OutPacket(PacketIds.HEARTBEAT_REQUEST, Size)]
    public readonly struct HEARTBEAT_REQUEST : IOutgoingPacket
    {
        public const int Size = 6;

        /// <summary>Monotonic client tick in milliseconds.</summary>
        public readonly uint ClientTimestamp;

        public HEARTBEAT_REQUEST(uint clientTimestamp)
        {
            ClientTimestamp = clientTimestamp;
        }

        public int WireSize => Size;

        public void Serialize(Span<byte> destination)
        {
            var w = new PacketWriter(destination);
            w.WriteUInt16(PacketIds.HEARTBEAT_REQUEST);
            w.WriteUInt32(ClientTimestamp);
        }
    }
}
