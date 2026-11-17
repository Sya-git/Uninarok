using System;
using System.Diagnostics;
using System.Threading;
using NUnit.Framework;
using Uninarok.Networking.Core.Framing;

namespace Uninarok.Networking.Tests
{
    // SPEC-0001 FR-5: the socket thread is never blocked while the main thread dispatches.
    public class FramerConcurrencyTests
    {
        const ushort A = 0x0010;

        [Test]
        public void Append_IsNotBlocked_WhileTheRouterRuns()
        {
            var routerEntered = new ManualResetEventSlim(false);
            var router = new RecordingRouter();
            router.OnRoute = _ => { routerEntered.Set(); Thread.Sleep(100); };

            var framer = new PacketFramer(new TableStub().Fixed(A, 6), router);
            framer.Append(Wire.Fixed(A, 1, 2, 3, 4));

            var main = new Thread(() => framer.Process(10));
            main.Start();
            Assert.IsTrue(routerEntered.Wait(2000), "router should have started");

            var sw = Stopwatch.StartNew();
            bool ok = framer.Append(Wire.Fixed(A, 5, 6, 7, 8));
            sw.Stop();

            Assert.IsTrue(ok);
            Assert.Less(sw.ElapsedMilliseconds, 50, "Append must not wait for the router");
            main.Join();

            // The data appended during dispatch is delivered by the next Process.
            Assert.AreEqual(1, framer.Process(10));
            Assert.AreEqual(2, router.Packets.Count);
        }

        [Test]
        public void ConcurrentAppends_DeliverEveryPacketExactlyOnce()
        {
            var router = new RecordingRouter();
            var framer = new PacketFramer(new TableStub().Fixed(A, 6), router);
            const int perThread = 500;
            var packet = Wire.Fixed(A, 1, 2, 3, 4);

            var producer = new Thread(() => { for (int i = 0; i < perThread; i++) framer.Append(packet); });
            producer.Start();
            int total = 0;
            var sw = Stopwatch.StartNew();
            while (total < perThread && sw.ElapsedMilliseconds < 5000) total += framer.Process(64);
            producer.Join();
            total += framer.Process(int.MaxValue);

            Assert.AreEqual(perThread, total);
            Assert.IsFalse(framer.IsFaulted);
        }
    }
}
