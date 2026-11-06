using System;
using System.IO;
using System.Net;
using Unity.Scripting.LifecycleManagement;

namespace Uninarok.Networking.Packets
{
    public static class HC
    {
        [NoAutoStaticsCleanup]
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

            public delegate void CharacterInfoPageDelegate(in ACK_CHARINFO_PER_PAGE packet);
            private static event CharacterInfoPageDelegate OnCharacterInfoPageReceived;
            public static void Subscribe(CharacterInfoPageDelegate callback)
            {
                OnCharacterInfoPageReceived += callback;
            }
            public static void Unsubscribe(CharacterInfoPageDelegate callback)
            {
                OnCharacterInfoPageReceived -= callback;
            }
            internal static void CallCharacterInfoPageReceived(in ACK_CHARINFO_PER_PAGE packet)
            {
                OnCharacterInfoPageReceived?.Invoke(in packet);
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
                var reader = new PacketReader(packet);
                reader.Skip(CharacterIdOffset);
                return reader.ReadUInt32();
            }

            public static bool TryGetMapEndpoint(ReadOnlySpan<byte> packet, out UninarokServerEndpoint endpoint)
            {
                endpoint = default;
                if (packet.Length < Size)
                    return false;
                var reader = new PacketReader(packet);
                if (reader.ReadUInt16() != PacketId)
                    return false;
                reader.Skip(MapAddressOffset - reader.Offset);
                var address = new IPAddress(new[] { reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte() });
                int port = reader.ReadUInt16();
                if (port == 0)
                    return false;
                endpoint = new UninarokServerEndpoint(address, port);
                return true;
            }

            private static void RequirePacket(ReadOnlySpan<byte> packet)
            {
                if (packet.Length < Size)
                    throw new InvalidDataException("Invalid map server redirect packet.");
                var reader = new PacketReader(packet);
                if (reader.ReadUInt16() != PacketId)
                    throw new InvalidDataException("Invalid map server redirect packet.");
            }
        }
    }
}
