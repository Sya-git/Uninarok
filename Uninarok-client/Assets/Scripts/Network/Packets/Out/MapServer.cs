using System;
using Uninarok.Networking.Packets.Attributes;

namespace Uninarok.Networking.Packets
{
    public static partial class MapServer
    {
        [OutPacket(PacketIds.REQUEST_ENTER_MAP, Size)]
        public struct REQUEST_ENTER_MAP
        {
            public const byte Size = 19;
            public uint AccountId;
            public int CharId;
            public int AuthCode;
            public uint Timestamp;
            public byte Sex;

            public Memory<byte> Serialize()
            {
                var buffer = new Memory<byte>(new byte[Size]);
                var writer = new PacketWriter(buffer.Span);

                writer.Write((ushort)PacketIds.REQUEST_ENTER_MAP);
                writer.Write(AccountId);
                writer.Write(CharId);
                writer.Write(AuthCode);
                writer.Write(Timestamp);
                writer.Write(Sex);

                return buffer;
            }
        }

        [OutPacket(PacketIds.MAP_HEARTBEAT, Size)]
        public struct MAP_HEARTBEAT
        {
            public const byte Size = 6;
            
            public uint Timestamp;

            public Memory<byte> Serialize()
            {
                var bytes = new Memory<byte>(new byte[Size]);
                var writer = new PacketWriter(bytes.Span);

                writer.Write((ushort)PacketIds.MAP_HEARTBEAT);
                writer.Write(Timestamp);

                return bytes;
            }
        }

        [OutPacket(PacketIds.SEND_PACKET_VER, Size)]
        public struct SEND_PACKET_VER
        {
            public const byte Size = 6;

            public uint PacketVer;

            public SEND_PACKET_VER(uint packetVer)
            {
                PacketVer = packetVer;
            }

            public Memory<byte> Serialize()
            {
                var bytes = new Memory<byte>(new byte[Size]);
                var writer = new PacketWriter(bytes.Span);

                writer.Write((ushort)PacketIds.SEND_PACKET_VER);
                writer.Write(PacketVer);

                return bytes;
            }
        }
    }
}
