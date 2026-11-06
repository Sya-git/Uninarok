using System;

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
                var writer = new PacketWriter(buffer);
                writer.WriteUInt16(PacketId);
                writer.WriteUInt32(AccountId);
                writer.WriteInt32(AuthCode);
                writer.WriteUInt32(UserLevel);
                writer.Skip(2);
                writer.WriteByte(Sex);
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
                var writer = new PacketWriter(buffer);
                writer.WriteUInt16(PacketId);
                writer.WriteByte(Slot);
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
                var writer = new PacketWriter(buffer);
                writer.WriteUInt16(PacketId);
                writer.WriteAscii(Name, 24);
                writer.WriteByte(Slot);
                writer.WriteUInt16(HairColor);
                writer.WriteUInt16(HairStyle);
                writer.WriteUInt16(JobClass);
                writer.Skip(2);
                writer.WriteByte(Sex);
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
                var writer = new PacketWriter(buffer);
                writer.WriteUInt16(PacketId);
                writer.WriteUInt32(AccountId);
                writer.WriteUInt32(CharacterId);
                writer.WriteInt32(AuthCode);
                writer.WriteUInt32(Tick);
                writer.WriteByte(Sex);
            }
        }
    }
}
