using System;
using System.IO;
using System.Net;
using Unity.Collections;

namespace Uninarok.Networking.Packets.In
{
    public static class AC
    {
        // TODO: check this, i had to add ref struct to allow using ReadOnlySpan<byte>
        public struct ACCEPT_LOGIN
        {
            public const ushort PacketId = 0x0AC4;
            public const short Size = -1;
            public const int LastLoginTimeSize = 26;
            public const int ServerNameSize = 20;
            public const int CharacterServerEntrySize = sizeof(uint) + sizeof(ushort) + ServerNameSize + sizeof(ushort) * 3;
            private const int MinimumPacketSize = sizeof(ushort) * 2 + sizeof(int) + sizeof(uint) * 3 + LastLoginTimeSize + sizeof(byte);
            public FixedString32Bytes LastLoginTimeString => LastLoginTime;
            public UninarokServerEndpoint ServersEndpointList => ServerList;

            public int AuthCode { get; }
            public uint AccountId { get; }
            public uint UserLevel { get; }
            public uint LastLoginIP { get; }
            private FixedString32Bytes LastLoginTime;
            private UninarokServerEndpoint ServerList;
            public byte Sex { get; }


            public ACCEPT_LOGIN(ReadOnlySpan<byte> data)
            {
                if (data.Length < MinimumPacketSize)
                    throw new InvalidDataException("Invalid AC_ACCEPT_LOGIN packet.");

                var reader = new PacketReader(data);
                if (reader.ReadUInt16() != PacketId)
                    throw new InvalidDataException("Invalid AC_ACCEPT_LOGIN packet.");
                int packetLength = reader.ReadUInt16();
                if (packetLength < MinimumPacketSize || packetLength > data.Length)
                    throw new InvalidDataException("Invalid AC_ACCEPT_LOGIN packet length.");

                AuthCode = reader.ReadInt32();
                AccountId = reader.ReadUInt32();
                UserLevel = reader.ReadUInt32();
                LastLoginIP = reader.ReadUInt32();
                var lastLoginTimeBytes = reader.ReadBytes(LastLoginTimeSize);
                var serverList = reader.ReadBytes(packetLength - reader.Offset - 1);
                Sex = reader.ReadByte();

                LastLoginTime = new FixedString32Bytes();
                // Copia o bloco de memória inteiro de uma vez
                unsafe
                {
                    fixed (byte* ptr = lastLoginTimeBytes)
                    {
                        LastLoginTime.Append(ptr, lastLoginTimeBytes.Length);
                    }
                }

                //TODO: Initialize all servers in the list, currently we only grab the first
                int count = serverList.Length / CharacterServerEntrySize;

                var serverReader = new PacketReader(serverList);
                // serverReader.Skip(selected * CharacterServerEntrySize);
                var address = new IPAddress(new[] { serverReader.ReadByte(), serverReader.ReadByte(), serverReader.ReadByte(), serverReader.ReadByte() });
                int port = serverReader.ReadUInt16();
                serverReader.Skip(ServerNameSize);
                _ = serverReader.ReadUInt16();
                _ = serverReader.ReadUInt16();
                _ = serverReader.ReadUInt16();

                ServerList = new UninarokServerEndpoint(address, port);
            }

            public delegate void LoginAcceptedHandler(in ACCEPT_LOGIN packet);
            private static LoginAcceptedHandler _loginAcceptedHandlers;
            public static void Register(LoginAcceptedHandler handler)
            {
                _loginAcceptedHandlers += handler;
            }

            public static void Unregister(LoginAcceptedHandler handler)
            {
                _loginAcceptedHandlers -= handler;
            }

            internal static void Call(in ACCEPT_LOGIN packet)
            {
                _loginAcceptedHandlers?.Invoke(in packet);
            }
        }

        public struct REFUSE_LOGIN
        {
            public const ushort PacketId = 0x006A;
            public const short Size = 23;
        }
    }
}
