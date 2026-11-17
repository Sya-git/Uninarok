using System;
using NUnit.Framework;
using Uninarok.Networking.Core.Framing;
using UnityEngine.TestTools.Constraints;

namespace Uninarok.Networking.Tests
{
    // SPEC-0001 FR-6: framing allocates no managed memory per packet on the main thread.
    public class FramerAllocTests
    {
        const ushort A = 0x0010, V = 0x0030;

        private sealed class NullRouter : IPacketRouter
        {
            public int Count;
            public bool Route(ushort packetId, ReadOnlySpan<byte> payload) { Count++; return true; }
        }

        [Test]
        public void Process_Over1000FixedPackets_AllocatesNothing()
        {
            var table = new TableStub().Fixed(A, 6).Variable(V);
            var router = new NullRouter();
            var framer = new PacketFramer(table, router);

            // Warm up the framer and JIT with a small batch, then stage the measured batch.
            framer.Append(Wire.Repeat(Wire.Fixed(A, 1, 2, 3, 4), 10));
            framer.Process(100);
            framer.Append(Wire.Repeat(Wire.Fixed(A, 1, 2, 3, 4), 1000));

            Assert.That(() => { framer.Process(2000); }, UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory());
            Assert.AreEqual(1010, router.Count);
        }

        [Test]
        public void Process_OverVariablePackets_AllocatesNothing()
        {
            var table = new TableStub().Fixed(A, 6).Variable(V);
            var router = new NullRouter();
            var framer = new PacketFramer(table, router);

            framer.Append(Wire.Repeat(Wire.Var(V, 1, 2, 3), 10));
            framer.Process(100);
            framer.Append(Wire.Repeat(Wire.Var(V, 1, 2, 3, 4, 5), 500));

            Assert.That(() => { framer.Process(2000); }, UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory());
            Assert.AreEqual(510, router.Count);
        }
    }
}
