using System;
using Unity.Collections;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Wire;
using Uninarok.Networking.Text;

namespace Uninarok.Networking.Packets.In.Map
{
    /// <summary>Map server to client, 0x0AC7 (ZC_NPCACK_SERVERMOVE), 156 bytes. The player moves to another map server. Cold.</summary>
    [InPacket(PacketIds.MAP_SERVER_CHANGE, Size)]
    public readonly struct MAP_SERVER_CHANGE
    {
        public const int Size = 156;

        /// <summary>With the <c>.gat</c> suffix.</summary>
        public readonly FixedString32Bytes MapName;
        public readonly ushort X;
        public readonly ushort Y;
        /// <summary>The four IPv4 octets, first octet in the lowest byte (big-endian on the wire). See <see cref="FormatIpAddress"/>.</summary>
        public readonly uint IpAddress;
        /// <summary>Little-endian, 0 to 65535.</summary>
        public readonly ushort Port;
        public readonly FixedString512Bytes DnsHost;

        public MAP_SERVER_CHANGE(ReadOnlySpan<byte> payload)
        {
            var r = new PacketReader(payload);
            MapName = default;
            WireText.Read(ref r, 16, ref MapName);
            X = r.ReadUInt16();
            Y = r.ReadUInt16();
            IpAddress = r.ReadUInt32();                      // octets in wire order, first octet lowest
            Port = r.ReadUInt16();
            DnsHost = default;
            WireText.Read(ref r, 128, ref DnsHost);
            r.EnsureConsumed();
        }

        /// <summary>Dotted IPv4 text, "a.b.c.d". Allocates: cold packet.</summary>
        public string FormatIpAddress() =>
            (IpAddress & 0xFF) + "." + ((IpAddress >> 8) & 0xFF) + "." + ((IpAddress >> 16) & 0xFF) + "." + (IpAddress >> 24);
    }
}
