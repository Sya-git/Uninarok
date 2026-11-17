using System;
using Unity.Collections;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Wire;
using Uninarok.Networking.Text;

namespace Uninarok.Networking.Packets.In.Character
{
    /// <summary>Character server to client, 0x0AC5, 156 bytes. The character ID and the endpoint of the map server to enter.</summary>
    [InPacket(PacketIds.MAP_ENTER_REQUEST_ACCEPTED, Size)]
    public readonly struct MAP_ENTER_REQUEST_ACCEPTED
    {
        public const int Size = 156;
        private const int MapNameWireBytes = 16;
        private const int DnsHostWireBytes = 128;

        public readonly int CharId;
        public readonly FixedString32Bytes MapName;

        /// <summary>The four IPv4 octets, first octet in the lowest byte. See <see cref="FormatIpAddress"/>.</summary>
        public readonly uint IpAddress;

        /// <summary>Read as <c>ushort</c>: a signed read makes ports above 32767 negative (FR-12).</summary>
        public readonly ushort Port;

        /// <summary>128 wire bytes hold up to 127 characters, more than FixedString128Bytes can (125).</summary>
        public readonly FixedString512Bytes DnsHost;

        public MAP_ENTER_REQUEST_ACCEPTED(ReadOnlySpan<byte> payload)
        {
            var r = new PacketReader(payload);
            CharId = r.ReadInt32();
            var map = new FixedString32Bytes();
            WireText.Read(ref r, MapNameWireBytes, ref map);
            MapName = map;
            IpAddress = r.ReadUInt32();                      // octets in wire order, first octet lowest
            Port = r.ReadUInt16();
            var dns = new FixedString512Bytes();
            WireText.Read(ref r, DnsHostWireBytes, ref dns);
            DnsHost = dns;
            r.EnsureConsumed();
        }

        /// <summary>Dotted IPv4 text, "a.b.c.d". Allocates: call once per session, not per packet.</summary>
        public string FormatIpAddress() =>
            (IpAddress & 0xFF) + "." + ((IpAddress >> 8) & 0xFF) + "." + ((IpAddress >> 16) & 0xFF) + "." + (IpAddress >> 24);
    }
}
