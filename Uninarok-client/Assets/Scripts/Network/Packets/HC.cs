using System;
using System.Buffers.Binary;
using System.IO;
using System.Net;

namespace Uninarok.Networking.Packets
{
    public static class HC
    {
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

        public struct NOTIFY_ZONESVR
        {
            public const ushort PacketId = 0x0AC5;
            public const short Size = 156;
            public const int CharacterIdOffset = 2;
            public const int MapAddressOffset = 22;
            public const int MapPortOffset = 26;

            public static uint ReadCharacterId(ReadOnlySpan<byte> packet)
            {
                RequirePacket(packet);
                return BinaryPrimitives.ReadUInt32LittleEndian(packet.Slice(CharacterIdOffset, 4));
            }

            public static bool TryGetMapEndpoint(ReadOnlySpan<byte> packet, out RagnarokServerEndpoint endpoint)
            {
                endpoint = default;
                if (packet.Length < Size || BinaryPrimitives.ReadUInt16LittleEndian(packet) != PacketId)
                    return false;
                var address = new IPAddress(new[] { packet[MapAddressOffset], packet[MapAddressOffset + 1], packet[MapAddressOffset + 2], packet[MapAddressOffset + 3] });
                int port = BinaryPrimitives.ReadUInt16LittleEndian(packet.Slice(MapPortOffset, 2));
                if (port == 0)
                    return false;
                endpoint = new RagnarokServerEndpoint(address, port);
                return true;
            }

            private static void RequirePacket(ReadOnlySpan<byte> packet)
            {
                if (packet.Length < Size || BinaryPrimitives.ReadUInt16LittleEndian(packet) != PacketId)
                    throw new InvalidDataException("Invalid map server redirect packet.");
            }
        }
    }
}
