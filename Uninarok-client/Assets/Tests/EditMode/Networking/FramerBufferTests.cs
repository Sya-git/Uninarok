using System;
using NUnit.Framework;
using Uninarok.Networking.Core.Framing;

namespace Uninarok.Networking.Tests
{
    // T2.7: inbox / work double buffer, cap and growth (plan 4.3, D1). Parsing is covered by the Framer*Tests suites.
    public class FramerBufferTests
    {
        static PacketFramer Make(int cap = PacketFramer.DefaultMaxBufferedBytes)
            => new PacketFramer(new TableStub(), new RecordingRouter(), cap);

        [Test]
        public void Append_StoresBytes_UntilDrained()
        {
            var f = Make();
            Assert.IsTrue(f.Append(new byte[100]));
            Assert.AreEqual(100, f.PendingBytes);
            f.Process(10);
            Assert.AreEqual(100, f.PendingBytes);   // nothing is lost by draining
        }

        [Test]
        public void Append_Empty_IsHarmless()
        {
            var f = Make();
            Assert.IsTrue(f.Append(ReadOnlySpan<byte>.Empty));
            Assert.AreEqual(0, f.PendingBytes);
        }

        [Test]
        public void Append_AboveCap_ReturnsFalse_AndStoresNothing()
        {
            var f = Make(cap: 64);
            Assert.IsTrue(f.Append(new byte[40]));
            Assert.IsFalse(f.Append(new byte[25]));       // 40 + 25 > 64
            Assert.AreEqual(40, f.PendingBytes);
            Assert.IsTrue(f.Append(new byte[24]));        // exactly at the cap
            Assert.IsFalse(f.Append(new byte[1]));
        }

        [Test]
        public void Buffers_GrowBeyondTheInitialSize()
        {
            var f = Make();
            Assert.IsTrue(f.Append(new byte[100_000]));    // initial buffers are 32 KB
            f.Process(10);
            Assert.AreEqual(100_000, f.PendingBytes);
            Assert.IsTrue(f.Append(new byte[200_000]));
            f.Process(10);
            Assert.AreEqual(300_000, f.PendingBytes);
        }

        [Test]
        public void Drain_RespectsTheCap_AndKeepsTheRemainderInTheInbox()
        {
            var f = Make(cap: 64);
            f.Append(new byte[64]);
            f.Process(10);                                 // work buffer is now full (64 unparsed bytes)
            Assert.IsTrue(f.Append(new byte[1]));          // the inbox itself is empty, so one more byte is accepted
            f.Process(10);                                 // but it cannot move into the full work buffer
            Assert.AreEqual(65, f.PendingBytes);           // nothing lost
            Assert.IsTrue(f.Append(new byte[63]));
            Assert.IsFalse(f.Append(new byte[1]), "inbox is at the cap");
        }
    }
}
