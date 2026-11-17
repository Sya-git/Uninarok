using System;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Uninarok.Networking.Core.Wire;

namespace Uninarok.Networking.Tests
{
    // SPEC-0001 FR-6, FR-18; plan 4.1.
    public class PacketReaderWriterTests
    {
        [Test]
        public void Reader_ReadsLittleEndianPrimitives()
        {
            byte[] data =
            {
                0x7F,                   // byte
                0x34, 0x12,             // ushort 0x1234
                0xFE, 0xFF,             // short -2
                0x78, 0x56, 0x34, 0x12, // uint 0x12345678
                0xFF, 0xFF, 0xFF, 0xFF  // int -1
            };
            var r = new PacketReader(data);
            Assert.AreEqual(data.Length, r.Remaining);
            Assert.AreEqual(0x7F, r.ReadByte());
            Assert.AreEqual(0x1234, r.ReadUInt16());
            Assert.AreEqual(-2, r.ReadInt16());
            Assert.AreEqual(0x12345678u, r.ReadUInt32());
            Assert.AreEqual(-1, r.ReadInt32());
            Assert.AreEqual(0, r.Remaining);
        }

        [Test]
        public void Reader_ReadBytes_ReadSpan_Skip()
        {
            byte[] data = { 1, 2, 3, 4, 5, 6, 7, 8 };
            var r = new PacketReader(data);
            byte[] dst = new byte[3];
            r.ReadBytes(dst);
            Assert.AreEqual(new byte[] { 1, 2, 3 }, dst);
            r.Skip(1);
            Assert.AreEqual(4, r.Remaining);
            Assert.AreEqual(new byte[] { 5, 6 }, r.ReadSpan(2).ToArray());
            Assert.AreEqual(2, r.Remaining);
            Assert.AreEqual(7, r.ReadByte());
        }

        [Test]
        public void Reader_Overrun_ThrowsPacketFormatException()
        {
            byte[] data = { 1, 2, 3 };
            Assert.Throws<PacketFormatException>(() => { var r = new PacketReader(data); r.ReadUInt32(); });
            Assert.Throws<PacketFormatException>(() => { var r = new PacketReader(data); r.ReadInt32(); });
            Assert.Throws<PacketFormatException>(() => { var r = new PacketReader(data); r.Skip(4); });
            Assert.Throws<PacketFormatException>(() => { var r = new PacketReader(data); r.ReadSpan(4); });
            Assert.Throws<PacketFormatException>(() =>
            {
                var r = new PacketReader(data);
                byte[] dst = new byte[4];
                r.ReadBytes(dst);
            });
            Assert.Throws<PacketFormatException>(() =>
            {
                var r = new PacketReader(data);
                r.ReadUInt16(); r.ReadUInt16();
            });
            Assert.Throws<PacketFormatException>(() => { var r = new PacketReader(ReadOnlySpan<byte>.Empty); r.ReadByte(); });
        }

        [Test]
        public void Reader_NegativeCount_Throws()
        {
            byte[] data = { 1, 2, 3 };
            Assert.Throws<PacketFormatException>(() => { var r = new PacketReader(data); r.Skip(-1); });
            Assert.Throws<PacketFormatException>(() => { var r = new PacketReader(data); r.ReadSpan(-1); });
        }

        [Test]
        public void EnsureConsumed_ThrowsWhenBytesAreLeft_AndPassesWhenAllWereRead()
        {
            byte[] data = { 1, 2, 3 };
            var r = new PacketReader(data);
            r.ReadUInt16();
            Assert.Throws<PacketFormatException>(() => { var x = new PacketReader(data); x.ReadUInt16(); x.EnsureConsumed(); });
            r.ReadByte();
            r.EnsureConsumed();                            // all three bytes read: no throw
            var empty = new PacketReader(ReadOnlySpan<byte>.Empty);
            empty.EnsureConsumed();
        }

        [Test]
        public void SixtyFourBit_ReadAndWrite_AreLittleEndian_AndRoundTrip()
        {
            byte[] buf = new byte[16];
            var w = new PacketWriter(buf);
            w.WriteInt64(-2L);
            w.WriteUInt64(0x0102030405060708UL);
            Assert.AreEqual(16, w.Position);
            Assert.AreEqual(new byte[] { 0xFE, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 8, 7, 6, 5, 4, 3, 2, 1 }, buf);

            var r = new PacketReader(buf);
            Assert.AreEqual(-2L, r.ReadInt64());
            Assert.AreEqual(0x0102030405060708UL, r.ReadUInt64());
            r.EnsureConsumed();
        }

        [Test]
        public void SixtyFourBit_Overrun_Throws()
        {
            Assert.Throws<PacketFormatException>(() => { var r = new PacketReader(new byte[7]); r.ReadInt64(); });
            Assert.Throws<PacketFormatException>(() => { var r = new PacketReader(new byte[7]); r.ReadUInt64(); });
            Assert.Throws<PacketFormatException>(() => { var w = new PacketWriter(new byte[7]); w.WriteInt64(1); });
            Assert.Throws<PacketFormatException>(() => { var w = new PacketWriter(new byte[7]); w.WriteUInt64(1); });
        }

        [Test]
        public void Writer_WritesLittleEndianPrimitives()
        {
            byte[] buf = new byte[13];
            var w = new PacketWriter(buf);
            w.WriteByte(0x7F);
            w.WriteUInt16(0x1234);
            w.WriteInt16(-2);
            w.WriteUInt32(0x12345678u);
            w.WriteInt32(-1);
            Assert.AreEqual(13, w.Position);
            Assert.AreEqual(new byte[] { 0x7F, 0x34, 0x12, 0xFE, 0xFF, 0x78, 0x56, 0x34, 0x12, 0xFF, 0xFF, 0xFF, 0xFF }, buf);
        }

        [Test]
        public void Writer_WriteBytes_And_Skip_WritesZeros()
        {
            byte[] buf = { 9, 9, 9, 9, 9, 9 };
            var w = new PacketWriter(buf);
            w.WriteBytes(new byte[] { 1, 2 });
            w.Skip(3);
            Assert.AreEqual(5, w.Position);
            Assert.AreEqual(new byte[] { 1, 2, 0, 0, 0, 9 }, buf);
        }

        [Test]
        public void Writer_Overrun_ThrowsPacketFormatException()
        {
            byte[] buf = new byte[3];
            Assert.Throws<PacketFormatException>(() => { var w = new PacketWriter(buf); w.WriteUInt32(1); });
            Assert.Throws<PacketFormatException>(() => { var w = new PacketWriter(buf); w.WriteBytes(new byte[4]); });
            Assert.Throws<PacketFormatException>(() => { var w = new PacketWriter(buf); w.Skip(4); });
            Assert.Throws<PacketFormatException>(() => { var w = new PacketWriter(buf); w.WriteUInt16(1); w.WriteUInt16(2); });
        }

        [Test]
        public void HappyPath_AllocatesNoManagedMemory()
        {
            byte[] src = new byte[32];
            byte[] dst = new byte[32];
            Assert.That(() =>
            {
                var w = new PacketWriter(dst);
                w.WriteByte(1); w.WriteUInt16(2); w.WriteInt16(3); w.WriteUInt32(4); w.WriteInt32(5);
                w.WriteBytes(new ReadOnlySpan<byte>(src, 0, 2)); w.Skip(2);

                var r = new PacketReader(src);
                r.ReadByte(); r.ReadUInt16(); r.ReadInt16(); r.ReadUInt32(); r.ReadInt32();
                r.ReadSpan(2); r.Skip(2);
            }, UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory());
        }
    }
}
