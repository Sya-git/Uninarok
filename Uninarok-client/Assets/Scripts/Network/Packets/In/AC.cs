using System;
using System.IO;
using System.Net;
using Uninarok.Networking.Packets.Attributes;
using Unity.Collections;

namespace Uninarok.Networking.Packets.In
{
    public static class AC
    {
        public enum AccountSex
        {
            SEX_FEMALE = 0,
            SEX_MALE = 1,
            SEX_SERVER = 2
        }
        public static char ToChar(this AccountSex sex)
        {
            return sex switch
            {
                AccountSex.SEX_FEMALE => 'F',
                AccountSex.SEX_MALE => 'M',
                AccountSex.SEX_SERVER => 'S',
                _ => (char)byte.MaxValue,
            };
        }
        public static byte ToConvertedByte(this AccountSex sex)
        {
            return (byte)sex.ToChar();
        }

        [InPacket(PacketIds.ACCEPT_LOGIN, Size)]
        public struct ACCEPT_LOGIN2
        {
            public const short Size = -1;

            public readonly struct UninarokServerEndpoint
            {
                public const short Size = 160;
                private const byte UnknownArraySize = 128;
                private const byte ServerNameSize = 20;

                public readonly IPAddress Address;
                public readonly short Port;
                public readonly FixedString32Bytes ServerName;
                public readonly short PlayerCount;
                public readonly short State;
                public readonly short Property;
                public readonly Memory<byte> Unknown;

                public UninarokServerEndpoint(ReadOnlySpan<byte> data)
                {
                    var reader = new PacketReader(data);

                    Address = new IPAddress(new[] { reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte() });
                    Port = reader.ReadShort();
                    ServerName = reader.ReadChars(ServerNameSize);
                    PlayerCount = reader.ReadShort();
                    State = reader.ReadShort();
                    Property = reader.ReadShort();
                    Unknown = new Memory<byte>();
                    reader.Skip(UnknownArraySize);
                }
            }

            private const byte LastLoginTimeSize = 26;
            private const byte AuthTokenSize = 16;
            public FixedString32Bytes LastLoginTimeString => LastLoginTime;
            public UninarokServerEndpoint[] ServersEndpointList => ServerList;

            public uint AccountId { get; }
            public int AuthCode { get; }
            public uint UserLevel { get; }
            public uint LastLoginIP { get; }
            private FixedString32Bytes LastLoginTime;
            public byte Sex { get; }
            public FixedString32Bytes AuthToken { get; } // 16
            public byte TwitterFlag { get; }
            private UninarokServerEndpoint[] ServerList;

            public ACCEPT_LOGIN2(ReadOnlySpan<byte> data)
            {
                var reader = new PacketReader(data);
                //if (packetLength > data.Length)
                //    throw new InvalidDataException("Invalid AC_ACCEPT_LOGIN packet length.");

                AuthCode = reader.ReadInt();
                AccountId = reader.ReadUint();
                UserLevel = reader.ReadUint();
                LastLoginIP = reader.ReadUint();
                LastLoginTime = reader.ReadChars(LastLoginTimeSize);
                Sex = reader.ReadByte();
                AuthToken = reader.ReadChars(AuthTokenSize);
                TwitterFlag = reader.ReadByte();

                var serverListBytes = reader.ReadBytes(data.Length - reader.Offset);
                var serversAmount = serverListBytes.Length / UninarokServerEndpoint.Size;
                ServerList = new UninarokServerEndpoint[serversAmount];
                for (var i = 0; i < serversAmount; ++i)
                {
                    ServerList[i] = new UninarokServerEndpoint(serverListBytes);
                }
            }
        }

        [InPacket(PacketIds.REFUSE_LOGIN, REFUSE_LOGIN.Size)]
        public struct REFUSE_LOGIN
        {
            public const short Size = 23;

            public REFUSE_LOGIN(ReadOnlySpan<byte> data)
            {

            }
        }

        /// <summary>
        /// Recusa quando se tenta fazer o login usando _M e _F em uma conta já criada?
        /// </summary>
        [InPacket(PacketIds.REFUSE_LOGIN_R3, Size)]
        public struct REFUSE_LOGIN_R3
        {
            public const byte Size = 26;
            private const byte BlockDateSize = 20;

            public int ErrorCode;
            public FixedString32Bytes BlockDate;

            public REFUSE_LOGIN_R3(ReadOnlySpan<byte> data)
            {
                var reader = new PacketReader(data);

                reader.Read(out ErrorCode);
                reader.Read(out BlockDate, BlockDateSize);
            }
        }
    }
}
