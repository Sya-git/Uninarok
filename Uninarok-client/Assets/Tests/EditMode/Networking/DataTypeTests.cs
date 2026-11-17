using NUnit.Framework;
using Unity.Collections;
using Uninarok.Networking.Core.Wire;
using Uninarok.Networking.Data;

namespace Uninarok.Networking.Tests
{
    // SPEC-0001 FR-18; vectors come from docs/data/*.md.
    public class DataTypeTests
    {
        static UninarokPosition ReadPos(byte[] b)
        {
            var r = new PacketReader(b);
            var p = UninarokPosition.Read(ref r);
            Assert.AreEqual(0, r.Remaining);
            return p;
        }

        static byte[] WritePos(UninarokPosition p)
        {
            var buf = new byte[UninarokPosition.WireSize];
            var w = new PacketWriter(buf);
            p.Write(ref w);
            Assert.AreEqual(UninarokPosition.WireSize, w.Position);
            return buf;
        }

        [TestCase(new byte[] { 0x25, 0x8C, 0x84 }, 150, 200, 4)]
        [TestCase(new byte[] { 0x00, 0x00, 0x00 }, 0, 0, 0)]
        [TestCase(new byte[] { 0xFF, 0xFF, 0xFF }, 1023, 1023, 15)]
        [TestCase(new byte[] { 0x00, 0x40, 0x01 }, 1, 0, 1)]
        public void Position_DecodesVector_AndRoundTrips(byte[] wire, int x, int y, int dir)
        {
            var p = ReadPos(wire);
            Assert.AreEqual(x, p.X);
            Assert.AreEqual(y, p.Y);
            Assert.AreEqual(dir, (int)p.Direction);
            Assert.AreEqual(wire, WritePos(p));
        }

        [Test]
        public void Position_Write_MasksOutOfRangeValues()
        {
            Assert.AreEqual(WritePos(new UninarokPosition(0, 5, UninarokDirection.North)),
                            WritePos(new UninarokPosition(1024, 5, UninarokDirection.North)));
            Assert.AreEqual(WritePos(new UninarokPosition(7, 0, UninarokDirection.North)),
                            WritePos(new UninarokPosition(7, 1024, UninarokDirection.North)));
            Assert.AreEqual(WritePos(new UninarokPosition(7, 7, UninarokDirection.NorthWest)),
                            WritePos(new UninarokPosition(7, 7, (UninarokDirection)0x11)));
        }

        [Test]
        public void Position_Read_WithTooFewBytes_Throws()
        {
            Assert.Throws<PacketFormatException>(() =>
            {
                var r = new PacketReader(new byte[] { 1, 2 });
                UninarokPosition.Read(ref r);
            });
        }

        [TestCase(new byte[] { 0x25, 0x8C, 0x82, 0x60, 0xCB, 0x88 }, 150, 200, 152, 203, 8, 8)]
        [TestCase(new byte[] { 0, 0, 0, 0, 0, 0 }, 0, 0, 0, 0, 0, 0)]
        [TestCase(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }, 1023, 1023, 1023, 1023, 15, 15)]
        [TestCase(new byte[] { 0x00, 0x40, 0x20, 0x0C, 0x04, 0x56 }, 1, 2, 3, 4, 5, 6)]
        public void MoveData_DecodesVector_AndRoundTrips(byte[] wire, int sx, int sy, int dx, int dy, int subX, int subY)
        {
            var r = new PacketReader(wire);
            var m = UninarokMoveData.Read(ref r);
            Assert.AreEqual(0, r.Remaining);
            Assert.AreEqual((sx, sy, dx, dy, subX, subY), ((int)m.SourceX, (int)m.SourceY, (int)m.DestX, (int)m.DestY, (int)m.SubX, (int)m.SubY));

            var buf = new byte[UninarokMoveData.WireSize];
            var w = new PacketWriter(buf);
            m.Write(ref w);
            Assert.AreEqual(UninarokMoveData.WireSize, w.Position);
            Assert.AreEqual(wire, buf);
        }

        [Test]
        public void MoveData_Write_MasksOutOfRangeValues()
        {
            byte[] Write(UninarokMoveData m)
            {
                var buf = new byte[6];
                var w = new PacketWriter(buf);
                m.Write(ref w);
                return buf;
            }
            Assert.AreEqual(Write(new UninarokMoveData(1, 2, 3, 4, 5, 6)),
                            Write(new UninarokMoveData(1 + 1024, 2 + 1024, 3 + 1024, 4 + 1024, 5 + 16, 6 + 16)));
        }

        [Test]
        public void AccountSex_Values()
        {
            Assert.AreEqual(0, (byte)AccountSex.SEX_FEMALE);
            Assert.AreEqual(1, (byte)AccountSex.SEX_MALE);
            Assert.AreEqual(2, (byte)AccountSex.SEX_SERVER);
        }

        [Test]
        public void Direction_Values()
        {
            Assert.AreEqual(0, (byte)UninarokDirection.North);
            Assert.AreEqual(4, (byte)UninarokDirection.South);
            Assert.AreEqual(7, (byte)UninarokDirection.NorthEast);
        }

        // Builds a 155-byte CharInfo wire image field by field, straight from docs/data/CharInfo.md.
        static byte[] CharInfoWire()
        {
            var buf = new byte[CharInfo.WireSize];
            var w = new PacketWriter(buf);
            w.WriteInt32(150000);                       // CharId
            w.WriteUInt32(0x00000002); w.WriteUInt32(0x00000001);   // BaseExp = 0x1_0000_0002
            w.WriteInt32(12345);                        // Zeny
            w.WriteUInt32(7); w.WriteUInt32(0);         // JobExp
            w.WriteInt32(10);                           // JobLevel
            w.WriteInt32(1); w.WriteInt32(2); w.WriteInt32(3);   // Opt1, Opt2, Option
            w.WriteInt32(-4); w.WriteInt32(5);          // Karma, Manner
            w.WriteInt16(6);                            // StatusPoint
            w.WriteInt32(500); w.WriteInt32(600);       // Hp, MaxHp
            w.WriteInt16(50); w.WriteInt16(60);         // Sp, MaxSp
            w.WriteInt16(150);                          // DefaultWalkSpeed
            for (short v = 1; v <= 6; v++) w.WriteInt16((short)(100 + v));   // Class Hair Body Weapon BaseLevel SkillPoint
            for (short v = 1; v <= 4; v++) w.WriteInt16((short)(200 + v));   // look head bottom, shield, head top, head mid
            w.WriteInt16(7); w.WriteInt16(8);           // HairColor, ClothesColor
            for (int i = 0; i < 24; i++) w.WriteByte(i < 6 ? (byte)"Poring"[i] : (byte)0);   // Name
            for (byte v = 1; v <= 6; v++) w.WriteByte((byte)(10 * v));   // Str Agi Vit Int Dex Luk
            w.WriteInt16(3);                            // Slot
            w.WriteUInt16(9);                           // Rename
            for (int i = 0; i < 16; i++) w.WriteByte(i < 8 ? (byte)"prontera"[i] : (byte)0); // MapName
            w.WriteInt32(11); w.WriteInt32(12); w.WriteInt32(13); w.WriteInt32(14);   // DeleteName LookRobe SlotChange RenameEnabled
            w.WriteByte((byte)AccountSex.SEX_MALE);
            Assert.AreEqual(CharInfo.WireSize, w.Position, "test helper must produce exactly 155 bytes");
            return buf;
        }

        [Test]
        public void CharInfo_WireSizeIs155()
        {
            Assert.AreEqual(155, CharInfo.WireSize);
        }

        [Test]
        public void CharInfo_ReadsEveryFieldInOrder_ConsumesExactly155()
        {
            var wire = CharInfoWire();
            var r = new PacketReader(wire);
            var c = CharInfo.Read(ref r);
            Assert.AreEqual(0, r.Remaining);

            Assert.AreEqual(150000, c.CharId);
            Assert.AreEqual(0x1_0000_0002UL, c.BaseExp);
            Assert.AreEqual(12345, c.Zeny);
            Assert.AreEqual(7UL, c.JobExp);
            Assert.AreEqual(10, c.JobLevel);
            Assert.AreEqual((1, 2, 3), (c.Opt1, c.Opt2, c.Option));
            Assert.AreEqual((-4, 5), (c.Karma, c.Manner));
            Assert.AreEqual(6, c.StatusPoint);
            Assert.AreEqual((500, 600), (c.Hp, c.MaxHp));
            Assert.AreEqual(((short)50, (short)60), (c.Sp, c.MaxSp));
            Assert.AreEqual(150, c.DefaultWalkSpeed);
            Assert.AreEqual((101, 102, 103, 104, 105, 106), ((int)c.Class, (int)c.Hair, (int)c.Body, (int)c.Weapon, (int)c.BaseLevel, (int)c.SkillPoint));
            Assert.AreEqual((201, 202, 203, 204), ((int)c.LookHeadBottom, (int)c.LookShield, (int)c.LookHeadTop, (int)c.LookHeadMid));
            Assert.AreEqual(((short)7, (short)8), (c.HairColor, c.ClothesColor));
            Assert.AreEqual("Poring", c.Name.ToString());
            Assert.AreEqual((10, 20, 30, 40, 50, 60), ((int)c.Str, (int)c.Agi, (int)c.Vit, (int)c.Int, (int)c.Dex, (int)c.Luk));
            Assert.AreEqual(3, c.Slot);
            Assert.AreEqual(9, c.Rename);
            Assert.AreEqual("prontera", c.MapName.ToString());
            Assert.AreEqual((11, 12, 13, 14), (c.DeleteName, c.LookRobe, c.SlotChangeEnabled, c.RenameEnabled));
            Assert.AreEqual(AccountSex.SEX_MALE, c.Sex);
        }

        [Test]
        public void CharInfo_Write_ReproducesTheWireImage()
        {
            var wire = CharInfoWire();
            var r = new PacketReader(wire);
            var c = CharInfo.Read(ref r);

            var back = new byte[CharInfo.WireSize];
            var w = new PacketWriter(back);
            c.Write(ref w);
            Assert.AreEqual(CharInfo.WireSize, w.Position);
            Assert.AreEqual(wire, back);
        }

        [Test]
        public void CharInfo_Read_WithTooFewBytes_Throws()
        {
            var wire = CharInfoWire();
            Assert.Throws<PacketFormatException>(() =>
            {
                var r = new PacketReader(new System.ReadOnlySpan<byte>(wire, 0, CharInfo.WireSize - 1));
                CharInfo.Read(ref r);
            });
        }
    }
}
