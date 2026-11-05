using System;
using System.Buffers.Binary;

namespace Uninarok.Networking.Packets
{
    public static class CZ
    {
        public struct ENTER
        {
            public const ushort PacketId = 0x0065;
            public const short Size = 17;

            public uint AccountId;
            public int AuthCode;
            public uint UserLevel;
            public byte Sex;

            public ENTER(uint accountId, int authCode, uint userLevel, byte sex)
            {
                AccountId = accountId;
                AuthCode = authCode;
                UserLevel = userLevel;
                Sex = sex;
            }

            public void Serialize(Span<byte> buffer)
            {
                PacketSerialization.RequireSize(buffer, Size);
                BinaryPrimitives.WriteUInt16LittleEndian(buffer, PacketId);
                BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(2), AccountId);
                BinaryPrimitives.WriteInt32LittleEndian(buffer.Slice(6), AuthCode);
                BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(10), UserLevel);
                buffer[16] = Sex;
            }
        }

        public struct SELECT_CHAR
        {
            public const ushort PacketId = 0x0066;
            public const short Size = 3;
            public byte Slot;

            public SELECT_CHAR(byte slot) => Slot = slot;

            public void Serialize(Span<byte> buffer)
            {
                PacketSerialization.RequireSize(buffer, Size);
                BinaryPrimitives.WriteUInt16LittleEndian(buffer, PacketId);
                buffer[2] = Slot;
            }
        }

        public struct MAKE_CHAR
        {
            public const ushort PacketId = 0x0A39;
            public const short Size = 36;

            public string Name;
            public byte Slot;
            public ushort HairColor;
            public ushort HairStyle;
            public ushort JobClass;
            public byte Sex;

            public MAKE_CHAR(string name, byte slot, ushort hairColor, ushort hairStyle, ushort jobClass, byte sex)
            {
                Name = name;
                Slot = slot;
                HairColor = hairColor;
                HairStyle = hairStyle;
                JobClass = jobClass;
                Sex = sex;
            }

            public void Serialize(Span<byte> buffer)
            {
                PacketSerialization.RequireSize(buffer, Size);
                buffer.Slice(0, Size).Clear();
                BinaryPrimitives.WriteUInt16LittleEndian(buffer, PacketId);
                PacketSerialization.WriteAscii(buffer.Slice(2, 24), Name);
                buffer[26] = Slot;
                BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(27), HairColor);
                BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(29), HairStyle);
                BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(31), JobClass);
                buffer[35] = Sex;
            }
        }

        public struct ENTER_MAP
        {
            public const ushort PacketId = 0x0436;
            public const short Size = 19;

            public uint AccountId;
            public uint CharacterId;
            public int AuthCode;
            public uint Tick;
            public byte Sex;

            public ENTER_MAP(uint accountId, uint characterId, int authCode, uint tick, byte sex)
            {
                AccountId = accountId;
                CharacterId = characterId;
                AuthCode = authCode;
                Tick = tick;
                Sex = sex;
            }

            public void Serialize(Span<byte> buffer)
            {
                PacketSerialization.RequireSize(buffer, Size);
                BinaryPrimitives.WriteUInt16LittleEndian(buffer, PacketId);
                BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(2), AccountId);
                BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(6), CharacterId);
                BinaryPrimitives.WriteInt32LittleEndian(buffer.Slice(10), AuthCode);
                BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(14), Tick);
                buffer[18] = Sex;
            }
        }
    }
}
