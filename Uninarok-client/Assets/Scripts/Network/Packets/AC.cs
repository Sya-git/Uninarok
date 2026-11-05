using System;
using System.Buffers.Binary;
using System.IO;
using System.Net;

namespace Uninarok.Networking.Packets
{
    public static class AC
    {
        // TODO: check this, i had to add ref struct to allow using ReadOnlySpan<byte>
        public ref struct ACCEPT_LOGIN
        {
            public const ushort PacketId = 0x0AC4;
            public const short Size = -1;
            public const int HeaderSize = 4;
            public const int CharacterServerListOffset = 64;
            public const int CharacterServerEntrySize = 160;
            public const int AccountIdOffset = 8;
            public const int AuthCodeOffset = 4;
            public const int UserLevelOffset = 12;
            public const int SexOffset = 46;

            public uint AccountId => BinaryPrimitives.ReadUInt32LittleEndian(Data.Slice(AccountIdOffset, 4));
            public int AuthCode => BinaryPrimitives.ReadInt32LittleEndian(Data.Slice(AuthCodeOffset, 4));
            public uint UserLevel => BinaryPrimitives.ReadUInt32LittleEndian(Data.Slice(UserLevelOffset, 4));
            public byte Sex => Data[SexOffset];

            private readonly ReadOnlySpan<byte> Data;

            public ACCEPT_LOGIN(ReadOnlySpan<byte> data)
            {
                Data = data;
                if (data.Length < CharacterServerListOffset || BinaryPrimitives.ReadUInt16LittleEndian(data) != PacketId)
                    throw new InvalidDataException("Invalid AC_ACCEPT_LOGIN packet.");
            }

            public bool TryGetCharacterServer(int index, out RagnarokServerEndpoint endpoint)
            {
                endpoint = default;
                int count = (Data.Length - CharacterServerListOffset) / CharacterServerEntrySize;
                if (count == 0)
                    return false;

                int selected = index >= 0 && index < count ? index : 0;
                int offset = CharacterServerListOffset + selected * CharacterServerEntrySize;
                var address = new IPAddress(new[] { Data[offset], Data[offset + 1], Data[offset + 2], Data[offset + 3] });
                int port = BinaryPrimitives.ReadUInt16LittleEndian(Data.Slice(offset + 4, 2));
                if (port == 0)
                    return false;
                endpoint = new RagnarokServerEndpoint(address, port);
                return true;
            }
        }

        public struct REFUSE_LOGIN
        {
            public const ushort PacketId = 0x006A;
            public const short Size = 23;
        }
    }
}
