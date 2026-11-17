using System;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Transport;
using Uninarok.Networking.Core.Wire;

namespace Uninarok.Networking.Packets.Out.Map
{
    /// <summary>
    /// Client to map server, 0x007D (CZ_NOTIFY_ACTORINIT), 2 bytes: header only. Sent when the client finished loading the
    /// map; the session is then in game and the map keepalive starts (spec section 6).
    /// </summary>
    [OutPacket(PacketIds.MAP_LOADED, Size)]
    public readonly struct MAP_LOADED : IOutgoingPacket
    {
        public const int Size = 2;

        public int WireSize => Size;

        public void Serialize(Span<byte> destination)
        {
            var w = new PacketWriter(destination);
            w.WriteUInt16(PacketIds.MAP_LOADED);
        }
    }
}
