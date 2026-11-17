using System;
using System.Linq;
using Uninarok.Networking.Packets.Attributes;
using Unity.Collections;

namespace Uninarok.Networking.Packets
{
    public static class CharacterServer
    {
        [InPacket(PacketIds.MAP_ENTER_REQUEST_ACCEPTED, Size)]
        public struct MAP_ENTER_REQUEST_ACCEPTED
        {
            public const byte Size = 156;
            private const byte MapNameSize = 16;
            public const byte DnsHostSize = 128;

            public int CharId;
            public FixedString32Bytes MapName;
            public byte[] IpAddress; // TODO: GC
            public short Port;
            public FixedString32Bytes DnsHost;

            public MAP_ENTER_REQUEST_ACCEPTED(ReadOnlySpan<byte> data)
            {
                var reader = new PacketReader(data);

                reader.Read(out CharId);
                reader.Read(out MapName, MapNameSize);
                IpAddress = reader.ReadBytes(4).ToArray();
                reader.Read(out Port);
                reader.Read(out DnsHost, DnsHostSize);
            }
        }

        [InPacket(PacketIds.MAP_ENTER_REQUEST_REFUSED, Size)]
        public struct MAP_ENTER_REQUEST_REFUSED
        {
            public const byte Size = 1;

            public byte ErrorCode;

            public MAP_ENTER_REQUEST_REFUSED(ReadOnlySpan<byte> data)
            {
                var reader = new PacketReader(data);
                
                reader.Read(out ErrorCode);
            }
        }

        [InPacket(PacketIds.CHARACTER_SLOTS_INFO, Size)]
        public struct CHARACTER_SLOTS_INFO
        {
            public const short Size = -1;
            private const short UnusedBytesSize = 20;

            public short CharSlots;
            public short AvailableCharSlots;
            public byte Unknown;
            public byte CharSlots2;
            public byte CharSlots3;
            public Memory<byte> UnusedBytes;

            public CHARACTER_SLOTS_INFO(ReadOnlySpan<byte> data)
            {
                var reader = new PacketReader(data);

                CharSlots = reader.ReadByte();
                AvailableCharSlots = reader.ReadByte();
                Unknown = reader.ReadByte();
                CharSlots2 = reader.ReadByte();
                CharSlots3 = reader.ReadByte();
                UnusedBytes = new Memory<byte>(reader.ReadBytes(UnusedBytesSize).ToArray());
            }
        }

        [InPacket(PacketIds.CHARACTER_DATA, Size)]
        public struct CHARACTER_DATA
        {
            public const short Size = -1;
            public struct CharInfo
            {
                public const byte Size = 155;
                private const byte NameSize = 24;
                private const byte MapNameSize = 16;

                public int CharId;
                public ulong BaseExp;
                public int Zeny;
                public ulong JobExp;
                public int JobLevel;
                public int Opt1;
                public int Opt2;
                public int Option;
                public int Karma;
                public int Manner;
                public short StatusPoint;
                public int Hp;
                public int MaxHp;
                public short Sp;
                public short MaxSp;
                public short DefaultWalkSpeed;
                public short Class;
                public short Hair;
                public short Body;
                // TODO: FIXME on Char.cpp:2087 
                public short Weapon;
                public short BaseLevel;
                public short SkillPoint;
                public short LookHeadBottom;
                public short LookShield;
                public short LookHeadTop;
                public short LookHeadMid;
                public short HairColor;
                public short ClothesColor;
                public FixedString32Bytes Name;
                public byte Str;
                public byte Agi;
                public byte Vit;
                public byte Int;
                public byte Dex;
                public byte Luk;
                public short Slot;
                public ushort Rename;
                public FixedString32Bytes MapName;
                public int DeleteName;
                public int LookRobe;
                public int SlotChangeEnabled;
                public int RenameEnabled;
                public byte Sex;

                public CharInfo(ReadOnlySpan<byte> data)
                {
                    var reader = new PacketReader(data);

                    CharId = reader.ReadInt();
                    BaseExp = reader.ReadUint() | ((ulong)reader.ReadUint() << 32);
                    Zeny = reader.ReadInt();
                    JobExp = reader.ReadUint() | ((ulong)reader.ReadUint() << 32);
                    JobLevel = reader.ReadInt(); // 24
                    Opt1 = reader.ReadInt();
                    Opt2 = reader.ReadInt();
                    Option = reader.ReadInt();
                    Karma = reader.ReadInt();
                    Manner = reader.ReadInt();
                    StatusPoint = reader.ReadShort(); // 48
                    Hp = reader.ReadInt(); // 50
                    MaxHp = reader.ReadInt(); // 54
                    Sp = reader.ReadShort();
                    MaxSp = reader.ReadShort();
                    DefaultWalkSpeed = reader.ReadShort(); // 62
                    Class = reader.ReadShort();
                    Hair = reader.ReadShort();
                    Body = reader.ReadShort();
                    Weapon = reader.ReadShort();
                    BaseLevel = reader.ReadShort();
                    SkillPoint = reader.ReadShort();
                    LookHeadBottom = reader.ReadShort();
                    LookShield = reader.ReadShort();
                    LookHeadTop = reader.ReadShort();
                    LookHeadMid = reader.ReadShort();
                    HairColor = reader.ReadShort();
                    ClothesColor = reader.ReadShort();
                    Name = reader.ReadChars(NameSize); // 88
                    Str = reader.ReadByte(); // 112
                    Agi = reader.ReadByte();
                    Vit = reader.ReadByte();
                    Int = reader.ReadByte();
                    Dex = reader.ReadByte();
                    Luk = reader.ReadByte();
                    Slot = reader.ReadShort();
                    Rename = reader.ReadUshort();
                    MapName = reader.ReadChars(MapNameSize);
                    DeleteName = reader.ReadInt();
                    LookRobe = reader.ReadInt();
                    SlotChangeEnabled = reader.ReadInt();
                    RenameEnabled = reader.ReadInt();
                    Sex = reader.ReadByte();
                }
            }

            public const byte UnknownBytesSize = 20;

            public byte MaxChars;
            public byte AvailableSlots;
            public byte PremiumSlots;
            public byte[] UnknownBytes;
            public CharInfo[] CharsInfo;

            public CHARACTER_DATA(ReadOnlySpan<byte> data)
            {
                var reader = new PacketReader(data);

                MaxChars = reader.ReadByte();
                AvailableSlots = reader.ReadByte();
                PremiumSlots = reader.ReadByte();
                UnknownBytes = reader.ReadBytes(UnknownBytesSize).ToArray();
                CharsInfo = ReadCharsInfo(data.Slice(reader.Offset));
            }

            private static CharInfo[] ReadCharsInfo(ReadOnlySpan<byte> data)
            {
                // TODO: Check for gc
                CharInfo[] charArray;
                
                var charsAmount = data.Length / CharInfo.Size;
                if (charsAmount > 0)
                {
                    charArray = new CharInfo[charsAmount];
                    for (var i = 0; i < charsAmount; ++i)
                    {
                        var charData = data.Slice(i * CharInfo.Size, (i + 1) * CharInfo.Size);
                        var charInfo = new CharInfo(charData);
                        charArray.Append(charInfo);
                    }
                }
                else
                {
                    charArray = Array.Empty<CharInfo>();
                }

                return charArray;
            }
        }

        [InPacket(PacketIds.CHARACTER_CREATION_FAILED, Size)]
        public struct CHARACTER_CREATION_FAILED
        {
            public enum CharCreationFailedReason : byte
            {
                NameAlreadyExists = 0x00,
                CreationDenied = 0xFF,
                YouAreUnderage = 0x01,
                CantOpenCharacterSlot = 0x03,
                ForbiddenCharactersInName = 0x02
            }
            public const byte Size = 3;

            public byte ErrorCode;

            public CHARACTER_CREATION_FAILED(ReadOnlySpan<byte> data)
            {
                var reader = new PacketReader(data);

                reader.Read(out ErrorCode);
            }
        }

        [InPacket(PacketIds.CHARACTER_CREATION_SUCCESS, Size)]
        public struct CHARACTER_CREATION_SUCCESS
        {
            public const byte Size = 157;

            public CHARACTER_DATA.CharInfo CharInfo;

            public CHARACTER_CREATION_SUCCESS(ReadOnlySpan<byte> data)
            {
                CharInfo = new CHARACTER_DATA.CharInfo(data);
            }
        }

        [InPacket(PacketIds.PINCODE_LOGINSTATE, Size)]
        public struct PINCODE_LOGINSTATE
        {
            public const byte Size = 12;

            public int PincodeSeed;
            public int AccountId;
            public short State;

            public PINCODE_LOGINSTATE(ReadOnlySpan<byte> data)
            {
                var reader = new PacketReader(data);

                reader.Read(out PincodeSeed);
                reader.Read(out AccountId);
                reader.Read(out State);
            }
        }
    }
}
