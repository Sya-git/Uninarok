using Unity.Collections;
using Uninarok.Networking.Core.Wire;
using Uninarok.Networking.Text;

namespace Uninarok.Networking.Data
{
    /// <summary>
    /// Character data embedded in several character-server packets: 155 bytes on the wire (docs/data/CharInfo.md).
    /// Plain mutable struct: it is stored in session lists and filled field by field.
    /// <see cref="Name"/> and <see cref="MapName"/> are held in <c>FixedString32Bytes</c>; a name whose UTF-8 form
    /// exceeds 29 bytes (only possible with several non-ASCII characters) is truncated at a character boundary (accepted).
    /// </summary>
    public struct CharInfo
    {
        public const int WireSize = 155;
        public const int NameWireBytes = 24;
        public const int MapNameWireBytes = 16;

        public int CharId;
        public ulong BaseExp;
        public int Zeny;
        public ulong JobExp;
        public int JobLevel;
        public int Opt1, Opt2, Option;
        public int Karma, Manner;
        public short StatusPoint;
        public int Hp, MaxHp;
        public short Sp, MaxSp;
        public short DefaultWalkSpeed;
        public short Class, Hair, Body, Weapon, BaseLevel, SkillPoint;
        public short LookHeadBottom, LookShield, LookHeadTop, LookHeadMid;
        public short HairColor, ClothesColor;
        public FixedString32Bytes Name;
        public byte Str, Agi, Vit, Int, Dex, Luk;
        public short Slot;
        public ushort Rename;
        public FixedString32Bytes MapName;
        public int DeleteName, LookRobe, SlotChangeEnabled, RenameEnabled;
        public AccountSex Sex;

        /// <summary>Consumes exactly <see cref="WireSize"/> bytes.</summary>
        public static CharInfo Read(ref PacketReader r)
        {
            var c = new CharInfo();
            c.CharId = r.ReadInt32();
            c.BaseExp = ReadUInt64(ref r);
            c.Zeny = r.ReadInt32();
            c.JobExp = ReadUInt64(ref r);
            c.JobLevel = r.ReadInt32();
            c.Opt1 = r.ReadInt32();
            c.Opt2 = r.ReadInt32();
            c.Option = r.ReadInt32();
            c.Karma = r.ReadInt32();
            c.Manner = r.ReadInt32();
            c.StatusPoint = r.ReadInt16();
            c.Hp = r.ReadInt32();
            c.MaxHp = r.ReadInt32();
            c.Sp = r.ReadInt16();
            c.MaxSp = r.ReadInt16();
            c.DefaultWalkSpeed = r.ReadInt16();
            c.Class = r.ReadInt16();
            c.Hair = r.ReadInt16();
            c.Body = r.ReadInt16();
            c.Weapon = r.ReadInt16();
            c.BaseLevel = r.ReadInt16();
            c.SkillPoint = r.ReadInt16();
            c.LookHeadBottom = r.ReadInt16();
            c.LookShield = r.ReadInt16();
            c.LookHeadTop = r.ReadInt16();
            c.LookHeadMid = r.ReadInt16();
            c.HairColor = r.ReadInt16();
            c.ClothesColor = r.ReadInt16();
            WireText.Read(ref r, NameWireBytes, ref c.Name);
            c.Str = r.ReadByte();
            c.Agi = r.ReadByte();
            c.Vit = r.ReadByte();
            c.Int = r.ReadByte();
            c.Dex = r.ReadByte();
            c.Luk = r.ReadByte();
            c.Slot = r.ReadInt16();
            c.Rename = r.ReadUInt16();
            WireText.Read(ref r, MapNameWireBytes, ref c.MapName);
            c.DeleteName = r.ReadInt32();
            c.LookRobe = r.ReadInt32();
            c.SlotChangeEnabled = r.ReadInt32();
            c.RenameEnabled = r.ReadInt32();
            c.Sex = (AccountSex)r.ReadByte();
            return c;
        }

        /// <summary>Writes exactly <see cref="WireSize"/> bytes.</summary>
        public void Write(ref PacketWriter w)
        {
            w.WriteInt32(CharId);
            WriteUInt64(ref w, BaseExp);
            w.WriteInt32(Zeny);
            WriteUInt64(ref w, JobExp);
            w.WriteInt32(JobLevel);
            w.WriteInt32(Opt1);
            w.WriteInt32(Opt2);
            w.WriteInt32(Option);
            w.WriteInt32(Karma);
            w.WriteInt32(Manner);
            w.WriteInt16(StatusPoint);
            w.WriteInt32(Hp);
            w.WriteInt32(MaxHp);
            w.WriteInt16(Sp);
            w.WriteInt16(MaxSp);
            w.WriteInt16(DefaultWalkSpeed);
            w.WriteInt16(Class);
            w.WriteInt16(Hair);
            w.WriteInt16(Body);
            w.WriteInt16(Weapon);
            w.WriteInt16(BaseLevel);
            w.WriteInt16(SkillPoint);
            w.WriteInt16(LookHeadBottom);
            w.WriteInt16(LookShield);
            w.WriteInt16(LookHeadTop);
            w.WriteInt16(LookHeadMid);
            w.WriteInt16(HairColor);
            w.WriteInt16(ClothesColor);
            WireText.Write(ref w, NameWireBytes, in Name);
            w.WriteByte(Str);
            w.WriteByte(Agi);
            w.WriteByte(Vit);
            w.WriteByte(Int);
            w.WriteByte(Dex);
            w.WriteByte(Luk);
            w.WriteInt16(Slot);
            w.WriteUInt16(Rename);
            WireText.Write(ref w, MapNameWireBytes, in MapName);
            w.WriteInt32(DeleteName);
            w.WriteInt32(LookRobe);
            w.WriteInt32(SlotChangeEnabled);
            w.WriteInt32(RenameEnabled);
            w.WriteByte((byte)Sex);
        }

        // 64-bit values travel as two little-endian 32-bit words, low word first.
        private static ulong ReadUInt64(ref PacketReader r)
        {
            ulong lo = r.ReadUInt32();
            ulong hi = r.ReadUInt32();
            return (hi << 32) | lo;
        }

        private static void WriteUInt64(ref PacketWriter w, ulong v)
        {
            w.WriteUInt32((uint)v);
            w.WriteUInt32((uint)(v >> 32));
        }
    }
}
