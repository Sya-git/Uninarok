using System;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine.TestTools.Constraints;
using Uninarok.Networking.Core.Wire;
using Uninarok.Networking.Text;

namespace Uninarok.Networking.Tests
{
    // SPEC-0001 FR-6; plan 4.2. Wire encoding is Windows-1252 (spec section 6).
    public class WireTextTests
    {
        static byte[] Bytes(params byte[] b) => b;

        [Test]
        public void Read_ConsumesExactlyWireBytes_AndStopsAtFirstNul()
        {
            byte[] payload = { (byte)'A', (byte)'b', 0, (byte)'Z', 0, 0, 0xAA, 0xBB };
            var r = new PacketReader(payload);
            var s = new FixedString32Bytes();
            WireText.Read(ref r, 6, ref s);
            Assert.AreEqual("Ab", s.ToString());
            Assert.AreEqual(2, r.Remaining);          // exactly 6 consumed, text after NUL discarded
        }

        [Test]
        public void Read_FullFieldWithoutNul_UsesAllBytes()
        {
            byte[] payload = { (byte)'a', (byte)'b', (byte)'c' };
            var r = new PacketReader(payload);
            var s = new FixedString32Bytes("old");
            WireText.Read(ref r, 3, ref s);
            Assert.AreEqual("abc", s.ToString());
            Assert.AreEqual(0, r.Remaining);
        }

        [Test]
        public void Read_ReplacesPreviousValue()
        {
            byte[] payload = { 0, 0, 0 };
            var r = new PacketReader(payload);
            var s = new FixedString32Bytes("old");
            WireText.Read(ref r, 3, ref s);
            Assert.AreEqual(0, s.Length);
        }

        [Test]
        public void Read_Overrun_Throws()
        {
            byte[] payload = { 1, 2 };
            Assert.Throws<PacketFormatException>(() =>
            {
                var r = new PacketReader(payload);
                var s = new FixedString32Bytes();
                WireText.Read(ref r, 3, ref s);
            });
        }

        [Test]
        public void Write_ZeroFillsAndNulTerminates()
        {
            byte[] buf = { 9, 9, 9, 9, 9, 9, 9 };
            var w = new PacketWriter(buf);
            WireText.Write(ref w, 6, new FixedString32Bytes("Hi"));
            Assert.AreEqual(6, w.Position);
            Assert.AreEqual(Bytes((byte)'H', (byte)'i', 0, 0, 0, 0, 9), buf);
        }

        [Test]
        public void Write_TruncatesToWireBytesMinusOne()
        {
            byte[] buf = new byte[5];
            var w = new PacketWriter(buf);
            WireText.Write(ref w, 4, new FixedString32Bytes("abcdef"));
            Assert.AreEqual(Bytes((byte)'a', (byte)'b', (byte)'c', 0, 0), buf);   // 3 chars + NUL, fifth byte untouched
            Assert.AreEqual(4, w.Position);
        }

        [Test]
        public void Write_Overrun_Throws()
        {
            byte[] buf = new byte[3];
            Assert.Throws<PacketFormatException>(() =>
            {
                var w = new PacketWriter(buf);
                WireText.Write(ref w, 4, new FixedString32Bytes("a"));
            });
        }

        [Test]
        public void Windows1252_HighRange_MapsPerChart()
        {
            // 0x80 euro, 0x93/0x94 curly quotes, 0x99 trademark, 0x9F Y-diaeresis, 0xE9 e-acute, 0xFF y-diaeresis
            byte[] payload = { 0x80, 0x93, 0x94, 0x99, 0x9F, 0xE9, 0xFF, 0 };
            var r = new PacketReader(payload);
            var s = new FixedString32Bytes();
            WireText.Read(ref r, payload.Length, ref s);
            Assert.AreEqual("€“”™Ÿéÿ", s.ToString());
        }

        [Test]
        public void Windows1252_AllBytesRoundTrip()
        {
            // 0x01..0xFF, split to fit the destination capacity. Undefined 0x81 0x8D 0x8F 0x90 0x9D map to U+0081 etc.
            for (int start = 1; start <= 255; start += 100)
            {
                int count = Math.Min(100, 256 - start);
                byte[] wire = new byte[count + 1];
                for (int i = 0; i < count; i++) wire[i] = (byte)(start + i);

                var r = new PacketReader(wire);
                var s = new FixedString512Bytes();
                WireText.Read(ref r, wire.Length, ref s);

                byte[] back = new byte[wire.Length];
                var w = new PacketWriter(back);
                WireText.Write(ref w, wire.Length, s);
                Assert.AreEqual(wire, back, $"round trip failed for chunk starting at 0x{start:X2}");
            }
        }

        [Test]
        public void WriteAscii_ReplacesEveryNonAsciiCharacterWithQuestionMark()
        {
            byte[] buf = new byte[8];
            var w = new PacketWriter(buf);
            WireText.WriteAscii(ref w, 8, new FixedString32Bytes("aé€b"));    // e-acute and euro are fine in Windows-1252, not in ASCII
            Assert.AreEqual(Bytes((byte)'a', (byte)'?', (byte)'?', (byte)'b', 0, 0, 0, 0), buf);
        }

        [Test]
        public void Write_UnmappableCharacter_BecomesQuestionMark()
        {
            byte[] buf = new byte[4];
            var w = new PacketWriter(buf);
            WireText.Write(ref w, 4, new FixedString32Bytes("a中b"));   // CJK char has no Windows-1252 mapping
            Assert.AreEqual(Bytes((byte)'a', (byte)'?', (byte)'b', 0), buf);
        }

        [Test]
        public void Read_ResultLongerThanCapacity_TruncatesAtCharacterBoundary()
        {
            byte[] payload = new byte[24];
            for (int i = 0; i < 23; i++) payload[i] = 0x99;   // trademark: 3 bytes each in UTF-8
            var r = new PacketReader(payload);
            var s = new FixedString32Bytes();                 // capacity 29 bytes
            WireText.Read(ref r, payload.Length, ref s);   // must not throw
            Assert.AreEqual(9, s.Length / 3);
            Assert.AreEqual(0, s.Length % 3);                 // no split character
            Assert.AreEqual(0, r.Remaining);
        }

        [Test]
        public void WorstCaseUtf8Bytes_IsThreeBytesPerCharacter()
        {
            // Capacity rule used by the per-packet capacity check (plan 4.2, FR-6).
            Assert.AreEqual(3 * 23, WireText.WorstCaseUtf8Bytes(24));
            Assert.AreEqual(0, WireText.WorstCaseUtf8Bytes(1));
        }

        [Test]
        public void ReadAndWrite_AllocateNoManagedMemory()
        {
            byte[] src = { (byte)'N', (byte)'a', (byte)'m', (byte)'e', 0x80, 0xE9, 0, 0 };
            byte[] dst = new byte[8];
            Assert.That(() =>
            {
                var r = new PacketReader(src);
                var s = new FixedString32Bytes();
                WireText.Read(ref r, 8, ref s);
                var w = new PacketWriter(dst);
                WireText.Write(ref w, 8, s);
            }, UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void AllFixedStringSizes_ReadAndWrite()
        {
            byte[] wire = { (byte)'o', (byte)'k', 0, 0 };
            var r1 = new PacketReader(wire); var a = new FixedString64Bytes();  WireText.Read(ref r1, 4, ref a);
            var r2 = new PacketReader(wire); var b = new FixedString128Bytes(); WireText.Read(ref r2, 4, ref b);
            var r3 = new PacketReader(wire); var c = new FixedString512Bytes(); WireText.Read(ref r3, 4, ref c);
            Assert.AreEqual("ok", a.ToString());
            Assert.AreEqual("ok", b.ToString());
            Assert.AreEqual("ok", c.ToString());

            byte[] o = new byte[12];
            var w = new PacketWriter(o);
            WireText.Write(ref w, 4, a); WireText.Write(ref w, 4, b); WireText.Write(ref w, 4, c);
            Assert.AreEqual(Bytes((byte)'o', (byte)'k', 0, 0, (byte)'o', (byte)'k', 0, 0, (byte)'o', (byte)'k', 0, 0), o);
        }
    }
}
