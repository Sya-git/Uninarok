using System;
using System.Buffers.Binary;
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
                if (packet.Length < HeaderSize || BinaryPrimitives.ReadUInt16LittleEndian(packet) != PacketId)
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
                for (int i = 0; i < count; i++)
                {
                    int offset = HeaderSize + i * CharacterInfoSize + 104;
                    ushort slot = BinaryPrimitives.ReadUInt16LittleEndian(packet.Slice(offset, 2));
                    if (i == 0)
                        firstSlot = (byte)slot;
                    if (slot == requestedSlot)
                        return requestedSlot;
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
                if (packet.Length < Size || BinaryPrimitives.ReadUInt16LittleEndian(packet) != PacketId)
                    throw new InvalidDataException("Invalid character-created packet.");
                return (byte)BinaryPrimitives.ReadUInt16LittleEndian(packet.Slice(SlotOffset, 2));
            }
        }

        public struct REFUSE_MAKECHAR
        {
            public const ushort PacketId = 0x006E;
            public const short Size = 3;
        }
    }
}
