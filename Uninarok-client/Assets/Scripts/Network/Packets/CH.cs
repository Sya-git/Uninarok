using System;
using Uninarok.Networking.Packets.Attributes;
using Unity.Collections;

namespace Uninarok.Networking.Packets.Out
{
    public static class CH
    {
        [OutPacket(PacketIds.CH_ENTER, CH_ENTER.Size)]
        public struct CH_ENTER
        {
            public const short Size = 17;

            public uint AccountId;
            public int AuthCode;
            public uint UserLevel;
            public byte Sex;

            public CH_ENTER(uint accountId, int authCode, uint userLevel, byte sex)
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
                writer.Write((ushort)PacketIds.CH_ENTER);
                writer.Write(AccountId);
                writer.Write(AuthCode);
                writer.Write(UserLevel);
                writer.Skip(2);
                writer.Write(Sex);
                return buffer;
            }
        }

        [OutPacket(PacketIds.SELECT_CHAR, SELECT_CHAR.Size)]
        public struct SELECT_CHAR
        {
            public const short Size = 3;

            public byte Slot;

            public SELECT_CHAR(byte slot)
            {
                Slot = slot;
            }

            public void Serialize(Span<byte> buffer)
            {
                PacketSerialization.RequireSize(buffer, Size);
                var writer = new PacketWriter(buffer);
                writer.Write((ushort)PacketIds.SELECT_CHAR);
                writer.Write(Slot);
            }
        }

        [OutPacket(PacketIds.MAKE_CHAR, MAKE_CHAR.Size)]
        public struct MAKE_CHAR
        {
            public const short Size = 36;
            private const byte NameSize = 24;
            // S 0x0a39 <name>.24B <slot>.B <hair color>.W <hair style>.W <starting job class ID>.W <Unknown>.(W or 2 B's)??? <sex>.B

            //TODO: GC
            public string Name;
            public byte Slot;
            public ushort HairColor;
            public ushort HairStyle;
            public int JobClass;
            //public ushort Unknown; ?
            public byte Sex;

            public MAKE_CHAR(string name, byte slot, ushort hairColor, ushort hairStyle, ushort jobClass, byte sex)
            {
                Name = name;
                Slot = slot;
                HairColor = hairColor;
                HairStyle = hairStyle;
                JobClass = jobClass;
                //Unknown = 0;
                Sex = sex;
            }

            public void Serialize(Memory<byte> buffer)
            {
                PacketSerialization.RequireSize(buffer.Span, Size);
                buffer.Slice(0, Size).Span.Clear();

                var writer = new PacketWriter(buffer.Span);
                writer.Write((ushort)PacketIds.MAKE_CHAR);
                writer.WriteAscii(Name, NameSize);
                writer.Write(Slot);
                writer.Write(HairColor);
                writer.Write(HairStyle);
                writer.Write(JobClass);
                //writer.Skip(Unknown);
                writer.Write(Sex);
            }
        }


        //TODO: PINCODE SYSTEM
        public struct PINCODE_SET_NEW
        {

            public int AccountId;

        }
    }
}
