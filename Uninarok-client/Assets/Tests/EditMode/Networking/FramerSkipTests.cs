using System;
using NUnit.Framework;
using Uninarok.Networking.Core.Framing;

namespace Uninarok.Networking.Tests
{
    // SPEC-0001 FR-3: raw bytes that are not packets (char server account-ID echo) are skipped reliably.
    public class FramerSkipTests
    {
        const ushort FixedId = 0x0010;

        static TableStub Table() => new TableStub().Fixed(FixedId, 6);

        [Test]
        public void SkipBeforeAnyPacket_ThenPacketIsRouted()
        {
            var router = new RecordingRouter();
            var framer = new PacketFramer(Table(), router);
            framer.SkipNextBytes(4);
            framer.Append(Wire.Concat(new byte[] { 0x11, 0x22, 0x33, 0x44 }, Wire.Fixed(FixedId, 1, 2, 3, 4)));
            Assert.AreEqual(1, framer.Process(10));
            Assert.IsFalse(framer.IsFaulted);
            Assert.AreEqual(new byte[] { 1, 2, 3, 4 }, router.Packets[0].Payload);
        }

        [Test]
        public void SkipOfZeros_DoesNotFault_AsPacketIdZero()
        {
            var router = new RecordingRouter();
            var framer = new PacketFramer(Table(), router);
            framer.SkipNextBytes(4);
            framer.Append(Wire.Concat(new byte[] { 0, 0, 0, 0 }, Wire.Fixed(FixedId, 1, 2, 3, 4)));
            framer.Process(10);
            Assert.IsFalse(framer.IsFaulted);
            Assert.AreEqual(1, router.Packets.Count);
        }

        [Test]
        public void SkipSplitAcrossOneByteChunks()
        {
            var router = new RecordingRouter();
            var framer = new PacketFramer(Table(), router);
            framer.SkipNextBytes(4);
            var stream = Wire.Concat(new byte[] { 0, 0, 0, 0 }, Wire.Fixed(FixedId, 1, 2, 3, 4));
            for (int i = 0; i < stream.Length; i++)
            {
                framer.Append(new ReadOnlySpan<byte>(stream, i, 1));
                framer.Process(10);
                Assert.IsFalse(framer.IsFaulted, $"fault after byte {i}");
            }
            Assert.AreEqual(1, router.Packets.Count);
        }

        [Test]
        public void SkipLargerThanAvailableData_ConsumesWhatArrivedAndWaitsForTheRest()
        {
            var router = new RecordingRouter();
            var framer = new PacketFramer(Table(), router);
            framer.SkipNextBytes(10);
            framer.Append(new byte[] { 0, 0, 0, 0 });
            Assert.AreEqual(0, framer.Process(10));
            Assert.AreEqual(0, router.Packets.Count);

            framer.Append(Wire.Concat(new byte[] { 0, 0, 0, 0, 0, 0 }, Wire.Fixed(FixedId, 7, 7, 7, 7)));
            Assert.AreEqual(1, framer.Process(10));
            Assert.IsFalse(framer.IsFaulted);
            Assert.AreEqual(new byte[] { 7, 7, 7, 7 }, router.Packets[0].Payload);
        }

        [Test]
        public void SkipRequestedWithNoDataYet_AppliesToTheNextBytesReceived()
        {
            var router = new RecordingRouter();
            var framer = new PacketFramer(Table(), router);
            framer.SkipNextBytes(4);
            Assert.AreEqual(0, framer.Process(10));
            framer.Append(new byte[] { 0, 0, 0, 0 });
            Assert.AreEqual(0, framer.Process(10));
            Assert.IsFalse(framer.IsFaulted);
        }

        [Test]
        public void SkipDoesNotCountAsPackets_InTheProcessLimit()
        {
            var router = new RecordingRouter();
            var framer = new PacketFramer(Table(), router);
            framer.SkipNextBytes(4);
            framer.Append(Wire.Concat(new byte[] { 0, 0, 0, 0 }, Wire.Repeat(Wire.Fixed(FixedId, 1, 2, 3, 4), 3)));
            Assert.AreEqual(2, framer.Process(2));
            Assert.AreEqual(1, framer.Process(2));
        }

        [Test]
        public void SkipZero_IsHarmless()
        {
            var router = new RecordingRouter();
            var framer = new PacketFramer(Table(), router);
            framer.SkipNextBytes(0);
            framer.Append(Wire.Fixed(FixedId, 1, 2, 3, 4));
            Assert.AreEqual(1, framer.Process(10));
        }
    }
}
