using System;
using System.IO;

namespace Uninarok.Networking.Packets
{
    public static class ZC
    {
        public struct ACCEPT_ENTER
        {
            public const ushort PacketId = 0x02EB;
            public const short Size = 13;
        }

        public struct REFUSE_ENTER
        {
            public const ushort PacketId = 0x0074;
            public const short Size = 3;
        }

        public struct ACK_CHARINFO_PER_PAGE
        {
            public const ushort PacketId = 0x099D;
            public const short Size = -1;
            public const int HeaderSize = 4;
            public const int CharacterInfoSize = 147;

            public static int GetCharacterCount(ReadOnlySpan<byte> packet)
            {
                if (packet.Length < HeaderSize)
                    throw new InvalidDataException("Invalid character list packet.");
                var reader = new PacketReader(packet);
                if (reader.ReadUInt16() != PacketId)
                    throw new InvalidDataException("Invalid character list packet.");
                int payloadLength = packet.Length - HeaderSize;
                if (payloadLength % CharacterInfoSize != 0)
                    throw new InvalidDataException("Character list entry size does not match Renewal PACKETVER 20190530.");
                return payloadLength / CharacterInfoSize;
            }

            public static byte FindCharacterSlot(ReadOnlySpan<byte> packet, byte requestedSlot)
            {
                int count = GetCharacterCount(packet);
                if (count == 0)
                    throw new InvalidDataException("The character list is empty.");

                byte firstSlot = 0;
                var reader = new PacketReader(packet);
                reader.Skip(HeaderSize + 104);
                for (int i = 0; i < count; i++)
                {
                    ushort slot = reader.ReadUInt16();
                    if (i == 0)
                        firstSlot = (byte)slot;
                    if (slot == requestedSlot)
                        return requestedSlot;
                    if (i + 1 < count)
                        reader.Skip(CharacterInfoSize - sizeof(ushort));
                }
                return firstSlot;
            }
        }

        public struct ACCEPT_MAKECHAR
        {
            public const ushort PacketId = 0x006D;
            public const short Size = 157;
            public const int SlotOffset = 106;

            public static byte ReadSlot(ReadOnlySpan<byte> packet)
            {
                if (packet.Length < Size)
                    throw new InvalidDataException("Invalid character-created packet.");
                var reader = new PacketReader(packet);
                if (reader.ReadUInt16() != PacketId)
                    throw new InvalidDataException("Invalid character-created packet.");
                reader.Skip(SlotOffset - reader.Offset);
                return (byte)reader.ReadUInt16();
            }
        }

        public struct REFUSE_MAKECHAR
        {
            public const ushort PacketId = 0x006E;
            public const short Size = 3;
        }
    }
}
