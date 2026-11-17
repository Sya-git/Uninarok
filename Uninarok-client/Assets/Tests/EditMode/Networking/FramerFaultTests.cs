#if PENDING_T2_10 // T2.3: enabled (guard removed) by T2.10.
using System;
using NUnit.Framework;
using Uninarok.Networking.Core.Framing;

namespace Uninarok.Networking.Tests
{
    // SPEC-0001 FR-2: protocol faults are detected once, with a diagnostic, and stop all routing.
    public class FramerFaultTests
    {
        const ushort FixedId = 0x0010, VarId = 0x0030;

        static TableStub Table() => new TableStub().Fixed(FixedId, 6).Variable(VarId);

        static (PacketFramer framer, RecordingRouter router) Make(TableStub table = null, int cap = PacketFramer.DefaultMaxBufferedBytes)
        {
            var router = new RecordingRouter();
            return (new PacketFramer(table ?? Table(), router, cap), router);
        }

        [Test]
        public void UnknownId_Faults_WithIdAndNextBytes()
        {
            var (framer, router) = Make();
            framer.Append(new byte[] { 0x34, 0x12, 0xAA, 0xBB });
            Assert.AreEqual(0, framer.Process(10));
            Assert.IsTrue(framer.IsFaulted);
            StringAssert.Contains("Unknown packet 0x1234", framer.FaultReason);
            StringAssert.Contains("34 12 AA BB", framer.FaultReason);
            Assert.AreEqual(0, router.Packets.Count);
        }

        [Test]
        public void IdZero_Faults()
        {
            var (framer, _) = Make();
            framer.Append(new byte[] { 0, 0, 1, 2 });
            framer.Process(10);
            Assert.IsTrue(framer.IsFaulted);
            StringAssert.Contains("Packet ID 0", framer.FaultReason);
        }

        [Test]
        public void VariableLengthBelowFour_Faults()
        {
            var (framer, _) = Make();
            framer.Append(new byte[] { (byte)VarId, 0x00, 0x03, 0x00 });   // declared total length 3 < 4
            framer.Process(10);
            Assert.IsTrue(framer.IsFaulted);
            StringAssert.Contains("Bad length", framer.FaultReason);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(-5)]
        public void InvalidTableLength_Faults(int value)
        {
            var table = new TableStub().Raw(0x0042, value);
            var (framer, _) = Make(table);
            framer.Append(new byte[] { 0x42, 0x00, 1, 2, 3 });
            framer.Process(10);
            Assert.IsTrue(framer.IsFaulted);
            StringAssert.Contains("Invalid table length", framer.FaultReason);
        }

        [Test]
        public void BufferCapExceeded_AppendReturnsFalse()
        {
            var (framer, _) = Make(cap: 16);
            Assert.IsTrue(framer.Append(new byte[16]));
            Assert.IsFalse(framer.Append(new byte[1]));
        }

        [Test]
        public void PacketsBeforeTheFault_AreRouted_ThenNothingMore()
        {
            var (framer, router) = Make();
            framer.Append(Wire.Concat(Wire.Fixed(FixedId, 1, 2, 3, 4), new byte[] { 0x99, 0x99 }, Wire.Fixed(FixedId, 5, 6, 7, 8)));
            Assert.AreEqual(1, framer.Process(10));
            Assert.IsTrue(framer.IsFaulted);
            Assert.AreEqual(1, router.Packets.Count);

            framer.Append(Wire.Fixed(FixedId, 9, 9, 9, 9));
            Assert.AreEqual(0, framer.Process(10));
            Assert.AreEqual(1, router.Packets.Count, "no routing after a fault");
        }

        [Test]
        public void FaultReason_IsStable_AcrossFurtherProcessCalls()
        {
            var (framer, _) = Make();
            framer.Append(new byte[] { 0x34, 0x12, 0xAA });
            framer.Process(10);
            string first = framer.FaultReason;
            framer.Append(new byte[] { 0xCC, 0xDD });
            framer.Process(10);
            framer.Process(10);
            Assert.AreSame(first, framer.FaultReason);
        }

        [Test]
        public void FaultReason_ShowsAtMost32Bytes()
        {
            var (framer, _) = Make();
            var data = new byte[2 + 40];
            data[0] = 0x34; data[1] = 0x12;
            for (int i = 2; i < data.Length; i++) data[i] = 0xEE;
            framer.Append(data);
            framer.Process(10);
            string reason = framer.FaultReason;
            int hexStart = reason.IndexOf("34 12", StringComparison.Ordinal);
            Assert.GreaterOrEqual(hexStart, 0);
            string hex = reason.Substring(hexStart);
            Assert.AreEqual(32, hex.Split(' ').Length, "32 bytes shown, space separated");
        }

        [Test]
        public void NotFaulted_ByDefault()
        {
            var (framer, _) = Make();
            Assert.IsFalse(framer.IsFaulted);
            Assert.IsNull(framer.FaultReason);
        }
    }
}
#endif
