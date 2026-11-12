using System;
using Uninarok.Networking.Packets.Attributes;

namespace Uninarok.Networking.Packets
{
    public static class CZ
    {
        [OutPacket(PacketIds.CZ_ENTER, Size)]
        public struct CZ_ENTER
        {
            public const short Size = 17;

            public uint AccountId;
            public int AuthCode;
            public uint UserLevel;
            public byte Sex;

            public CZ_ENTER(uint accountId, int authCode, uint userLevel, byte sex)
            {
                AccountId = accountId;
                AuthCode = authCode;
                UserLevel = userLevel;
                Sex = sex;
            }

            public Memory<byte> Serialize()
            {
                var buffer = new Memory<byte>(new byte[Size]);
                var writer = new PacketWriter(buffer.Span);
                
                writer.Write((ushort)PacketIds.CZ_ENTER);
                // TODO: ??
                return buffer;
                writer.Write(AccountId);
                writer.Write(AuthCode);
                writer.Write(UserLevel);
                writer.Skip(2);
                writer.Write(Sex);

                return buffer;
            }
        }

        [OutPacket(PacketIds.CZ_ENTER2, Size)]
        public struct CZ_ENTER2
        {
            public const byte Size = 23-4;
            public uint AccountId;
            public int CharId;
            public int AuthCode;
            public uint Timestamp;
            public byte Sex;

            public Memory<byte> Serialize()
            {
                var buffer = new Memory<byte>(new byte[Size]);
                var writer = new PacketWriter(buffer.Span);

                writer.Write((ushort)PacketIds.CZ_ENTER2);
                writer.Write(AccountId);
                writer.Write(CharId);
                writer.Write(AuthCode);
                //writer.Write(0); // Skip 4 bytes, maybe auth code is long
                writer.Write(Timestamp);
                writer.Write(Sex);

                return buffer;
            }
        }

        [OutPacket(PacketIds.CZ_REQUEST_TIME2, Size)]
        public struct CZ_REQUEST_TIME2
        {
            public const byte Size = 6;
            
            public uint Timestamp;

            public CZ_REQUEST_TIME2(ReadOnlySpan<byte> data)
            {
                var reader = new PacketReader(data);

                reader.Read(out Timestamp);
            }

            public Memory<byte> Serialize()
            {
                var bytes = new Memory<byte>(new byte[Size]);
                var writer = new PacketWriter(bytes.Span);

                writer.Write((ushort)PacketIds.CZ_REQUEST_TIME2);
                writer.Write(Timestamp);

                return bytes;
            }
        }

        [OutPacket(PacketIds.CZ_PACKET_DB, Size)]
        public struct CZ_PACKET_DB
        {
            public const byte Size = 6;

            public uint PacketVer;

            public CZ_PACKET_DB(uint packetVer)
            {
                PacketVer = packetVer;
            }

            public Memory<byte> Serialize()
            {
                var bytes = new Memory<byte>(new byte[Size]);
                var writer = new PacketWriter(bytes.Span);

                writer.Write((ushort)PacketIds.CZ_PACKET_DB);
                writer.Write(PacketVer);

                return bytes;
            }
        }
    }
}
