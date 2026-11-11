using System;
using System.IO;
using Uninarok.Networking.Packets.Attributes;

namespace Uninarok.Networking.Packets
{
    public static class ZC
    {
        [InPacket(PacketIds.ACCEPT_ENTER, Size)]
        public struct ACCEPT_ENTER
        {
            public const short Size = 13;

            public ACCEPT_ENTER(ReadOnlySpan<byte> data)
            {

            }
        }

        [InPacket(PacketIds.REFUSE_ENTER, Size)]
        public struct REFUSE_ENTER
        {
            public const ushort PacketId = 0x0074;
            public const short Size = 3;

            public REFUSE_ENTER(ReadOnlySpan<byte> data)
            {

            }
        }

        [InPacket(PacketIds.ZC_ACK_PING, Size)]
        public struct ZC_ACK_PING
        {
            public const short Size = 6;

            public uint Tick;

            public ZC_ACK_PING(ReadOnlySpan<byte> data)
            {
                var reader = new PacketReader(data);

                reader.Read(out Tick);
            }
        }
    }
}
