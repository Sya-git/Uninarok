#if PENDING_T2_8 // T2.5: enabled (guard removed) by T2.8.
using System;
using NUnit.Framework;
using Uninarok.Networking.Core.Framing;

namespace Uninarok.Networking.Tests
{
    // SPEC-0001 FR-5 (bounded work per frame), FR-19 (known but unhandled packets are skipped).
    public class FramerLimitsTests
    {
        const ushort A = 0x0010, B = 0x0011;

        static TableStub Table() => new TableStub().Fixed(A, 6).Fixed(B, 4);

        [Test]
        public void ThousandPackets_WithCap256_TakeFourCalls()
        {
            var router = new RecordingRouter();
            var framer = new PacketFramer(Table(), router);
            framer.Append(Wire.Repeat(Wire.Fixed(A, 1, 2, 3, 4), 1000));

            Assert.AreEqual(256, framer.Process(256));
            Assert.AreEqual(256, framer.Process(256));
            Assert.AreEqual(256, framer.Process(256));
            Assert.AreEqual(232, framer.Process(256));
            Assert.AreEqual(0, framer.Process(256));
            Assert.AreEqual(1000, router.Packets.Count);
        }

        [Test]
        public void ThrowingRouter_PropagatesOnce_AndNextCallContinuesWithTheNextPacket()
        {
            var router = new RecordingRouter();
            int calls = 0;
            router.OnRoute = _ => { if (++calls == 3) throw new InvalidOperationException("boom"); };
            var framer = new PacketFramer(Table(), router);
            var stream = Wire.Concat(
                Wire.Fixed(A, 1, 0, 0, 0), Wire.Fixed(A, 2, 0, 0, 0), Wire.Fixed(A, 3, 0, 0, 0),
                Wire.Fixed(A, 4, 0, 0, 0), Wire.Fixed(A, 5, 0, 0, 0));
            framer.Append(stream);

            Assert.Throws<InvalidOperationException>(() => framer.Process(100));
            Assert.IsFalse(framer.IsFaulted);

            Assert.AreEqual(2, framer.Process(100));                       // packets 4 and 5; packet 3 is not delivered again
            Assert.AreEqual(5, router.Packets.Count);
            Assert.AreEqual(4, router.Packets[3].Payload[0]);
            Assert.AreEqual(5, router.Packets[4].Payload[0]);
        }

        [Test]
        public void Stop_EndsProcessingAfterTheCurrentPacket_AndLaterCallsReturnZero()
        {
            var router = new RecordingRouter();
            PacketFramer framer = null;
            router.OnRoute = _ => framer.Stop();
            framer = new PacketFramer(Table(), router);
            framer.Append(Wire.Repeat(Wire.Fixed(A, 1, 2, 3, 4), 5));

            Assert.AreEqual(1, framer.Process(100));
            Assert.AreEqual(0, framer.Process(100));
            Assert.AreEqual(1, router.Packets.Count);

            framer.Append(Wire.Fixed(A, 1, 2, 3, 4));
            Assert.AreEqual(0, framer.Process(100));
        }

        [Test]
        public void KnownButUnhandledId_IsSkippedByLength_WithoutFault()
        {
            var router = new RecordingRouter().HandleOnly(A);
            var framer = new PacketFramer(Table(), router);
            framer.Append(Wire.Concat(Wire.Fixed(B, 9, 9), Wire.Fixed(A, 1, 2, 3, 4)));

            Assert.AreEqual(2, framer.Process(10));                        // skipped packets count as framed
            Assert.IsFalse(framer.IsFaulted);
            Assert.AreEqual(1, router.Packets.Count);
            Assert.AreEqual(new byte[] { 1, 2, 3, 4 }, router.Packets[0].Payload);
        }

        [Test]
        public void ZeroOrNegativeLimit_ProcessesNothing()
        {
            var router = new RecordingRouter();
            var framer = new PacketFramer(Table(), router);
            framer.Append(Wire.Fixed(A, 1, 2, 3, 4));
            Assert.AreEqual(0, framer.Process(0));
            Assert.AreEqual(0, framer.Process(-1));
            Assert.AreEqual(1, framer.Process(1));
        }

        [Test]
        public void LargeStream_GrowsBuffersBeyondTheInitialSize()
        {
            var router = new RecordingRouter();
            var framer = new PacketFramer(Table(), router);
            var chunk = Wire.Repeat(Wire.Fixed(A, 1, 2, 3, 4), 10000);   // 60 000 bytes, more than the 32 KB initial buffer
            Assert.IsTrue(framer.Append(chunk));
            Assert.AreEqual(10000, framer.Process(int.MaxValue));
        }
    }
}
#endif
