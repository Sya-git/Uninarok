using System;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Wire;

namespace Uninarok.Networking.Packets.In.Map
{
    /// <summary>Map server to client, 0x007F (ZC_NOTIFY_TIME), 6 bytes. Reply to <c>HEARTBEAT_REQUEST</c> with the server tick.</summary>
    [InPacket(PacketIds.SERVER_HEARTBEAT_RESPONSE, Size)]
    public readonly struct SERVER_HEARTBEAT_RESPONSE
    {
        public const int Size = 6;

        public readonly uint ServerTimestamp;

        public SERVER_HEARTBEAT_RESPONSE(ReadOnlySpan<byte> payload)
        {
            var r = new PacketReader(payload);
            ServerTimestamp = r.ReadUInt32();
            r.EnsureConsumed();
        }
    }
}
