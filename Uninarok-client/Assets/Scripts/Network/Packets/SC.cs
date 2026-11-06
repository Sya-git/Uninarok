using System;
using System.IO;

namespace Uninarok.Networking.Packets
{
    public static class SC
    {
        public struct NOTIFY_BAN
        {
            public const ushort PacketId = 0x0081;
            public const short Size = 3;
        }

        public struct REFUSE_LOGIN_R2
        {
            public const ushort PacketId = 0x083E;
            public const short Size = 27;
        }

        public struct REFUSE_LOGIN_R3
        {
            public const ushort PacketId = 0x0B02;
            public const short Size = 27;
        }

        public struct ACK_HASH
        {
            public const ushort PacketId = 0x01DC;
            public const short Size = -1;
            public const int HeaderSize = 4;

            public static int GetPacketLength(ReadOnlySpan<byte> header)
            {
                if (header.Length < HeaderSize)
                    throw new InvalidDataException("Invalid SC_ACK_HASH packet header.");
                var reader = new PacketReader(header);
                if (reader.ReadUInt16() != PacketId)
                    throw new InvalidDataException("Invalid SC_ACK_HASH packet header.");
                int length = reader.ReadUInt16();
                if (length < HeaderSize)
                    throw new InvalidDataException($"Invalid packet length {length} for 0x{PacketId:X4}.");
                return length;
            }
        }
    }
}
