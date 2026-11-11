using System;
using System.IO;
using Uninarok.Networking.Packets.Attributes;

namespace Uninarok.Networking.Packets
{
    public static class SC
    {
        [InPacket(PacketIds.NOTIFY_BAN, Size)]
        public struct NOTIFY_BAN
        {
            public const ushort PacketId = 0x0081;
            public const short Size = 3;

            public NOTIFY_BAN(ReadOnlySpan<byte> data)
            {

            }
        }

        [InPacket(PacketIds.ACK_HASH, Size)]
        public struct ACK_HASH
        {
            public const ushort PacketId = 0x01DC;
            public const short Size = -1;
            public const int HeaderSize = 4;

            public ACK_HASH(ReadOnlySpan<byte> data)
            {

            }
        }
    }
}
