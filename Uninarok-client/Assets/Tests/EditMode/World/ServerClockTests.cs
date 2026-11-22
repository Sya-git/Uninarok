using System;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Uninarok.Networking.Tests;

namespace Uninarok.World.Tests
{
    // SPEC-0010 FR-2 (PLAN-0010 4.2, D6).
    public class ServerClockTests
    {
        // A server whose clock is the local one plus an offset, and a network with a random delay each way.
        sealed class SimulatedServer
        {
            public readonly FakeClock Local = new FakeClock { NowMs = 100_000 };
            public readonly ServerClock Clock;
            public long Offset;
            readonly Random _random;
            readonly int _minRtt, _maxRtt;
            long _answerAt = -1, _sentAt;
            uint _answerTick;
            public int LargestRtt;

            public SimulatedServer(long offset, int minRtt, int maxRtt, int seed)
            {
                Offset = offset;
                _minRtt = minRtt;
                _maxRtt = maxRtt;
                _random = new Random(seed);
                Clock = new ServerClock(Local);
            }

            public uint Tick => unchecked((uint)(Local.NowMs + Offset));

            public void Enter(int delay)
            {
                uint tick = Tick;
                Local.AdvanceMs(delay);
                Clock.Start(tick, Local.NowMs);
            }

            public void Heartbeat()
            {
                int rtt = _random.Next(_minRtt, _maxRtt + 1);
                int there = _random.Next(0, rtt + 1);     // however the delay splits
                LargestRtt = Math.Max(LargestRtt, rtt);
                _sentAt = Local.NowMs;
                _answerTick = unchecked((uint)(Local.NowMs + there + Offset));
                _answerAt = Local.NowMs + rtt;
            }

            /// <summary>One frame: time passes and an answer that arrived in it is handled.</summary>
            public void Frame(int ms)
            {
                long target = Local.NowMs + ms;
                if (_answerAt >= 0 && _answerAt <= target)
                {
                    Local.NowMs = _answerAt;
                    Clock.Sample(_answerTick, _sentAt, _answerAt);
                    _answerAt = -1;
                }
                Local.NowMs = target;
            }
        }

        [Test]
        public void ServerClock_Offset5s_Rtt50To200_WithinHalfRttPlus10([Values(1, 2, 3, 4, 5)] int seed, [Values(-5_000, 5_000)] int offset)
        {
            var s = new SimulatedServer(offset, 50, 200, seed);
            s.Enter(80);
            s.Heartbeat();
            for (int t = 0; t < 300; t += 16) s.Frame(16);
            for (int second = 0; second < 600; second++)
            {
                if (second % 10 == 0) s.Heartbeat();
                for (int f = 0; f < 60; f++)
                {
                    s.Frame(17);
                    // the kept sample is the shortest of eight, so its half is at most the largest half seen; slewing needs its time
                    int error = Math.Abs(ServerClock.Diff(s.Clock.Now, s.Tick));
                    Assert.LessOrEqual(error, s.LargestRtt / 2 + 10, $"second {second}, frame {f}");
                }
            }
        }

        [Test]
        public void ServerClock_MonotonicOver10Minutes([Values(1, 2, 3)] int seed)
        {
            var s = new SimulatedServer(5_000, 50, 200, seed);
            s.Enter(10);
            uint last = s.Clock.Now;
            for (int second = 0; second < 600; second++)
            {
                if (second % 10 == 0) s.Heartbeat();
                if (second % 60 == 0) s.Offset += second % 120 == 0 ? -120 : 90;     // the server's clock is adjusted
                for (int f = 0; f < 100; f++)
                {
                    s.Frame(10);
                    uint now = s.Clock.Now;
                    Assert.GreaterOrEqual(ServerClock.Diff(now, last), 0);
                    last = now;
                }
            }
        }

        [Test]
        public void ServerClock_HigherCorrection_JumpsByExactlyTheCorrection()
        {
            var local = new FakeClock { NowMs = 1_000 };
            var clock = new ServerClock(local);
            clock.Start(50_000, 1_000);
            Assert.AreEqual(50_000u, clock.Now);
            local.NowMs = 2_000;
            clock.Sample(51_060, 1_980, 2_000);        // round trip 20: the server is at 51 070 now, 70 ahead of the estimate
            Assert.AreEqual(51_070u, clock.Now);
            Assert.AreEqual(20, clock.LastRoundTripMs);
        }

        [Test]
        public void ServerClock_LowerCorrectionSlews()
        {
            var local = new FakeClock { NowMs = 1_000 };
            var clock = new ServerClock(local);
            clock.Start(50_000, 1_000);
            local.NowMs = 2_000;
            clock.Sample(50_800, 2_000, 2_000);        // the estimate is 200 ahead
            Assert.AreEqual(51_000u, clock.Now, "no step back");
            local.NowMs = 2_100;
            Assert.AreEqual(51_050u, clock.Now, "half speed while it catches up");
            local.NowMs = 2_400;
            Assert.AreEqual(51_200u, clock.Now, "caught up after 400 ms");
            local.NowMs = 2_500;
            Assert.AreEqual(51_300u, clock.Now, "full speed again");
        }

        [Test]
        public void ServerClock_KeepsTheShortestOfTheLastEight()
        {
            var local = new FakeClock { NowMs = 0 };
            var clock = new ServerClock(local);
            clock.Start(10_000, 0);
            local.NowMs = 1_000;
            clock.Sample(11_000, 1_000, 1_000);        // exact
            for (int i = 0; i < ServerClock.SampleCount - 1; i++)
            {
                local.NowMs += 1_000;
                clock.Sample((uint)(local.NowMs + 10_000), local.NowMs - 400, local.NowMs);   // answered late: would put it 200 ahead
                Assert.AreEqual((uint)(local.NowMs + 10_000), clock.Now, "the exact sample wins");
            }
            local.NowMs += 1_000;
            clock.Sample((uint)(local.NowMs + 10_000), local.NowMs - 400, local.NowMs);       // the exact one is forgotten
            Assert.AreEqual((uint)(local.NowMs + 10_200), clock.Now);
        }

        [Test]
        public void ServerClock_Wraps()
        {
            var local = new FakeClock { NowMs = 0 };
            var clock = new ServerClock(local);
            clock.Start(uint.MaxValue - 499, 0);       // 500 ms before the wrap
            local.NowMs = 400;
            Assert.AreEqual(uint.MaxValue - 99, clock.Now);
            local.NowMs = 600;
            Assert.AreEqual(100u, clock.Now);
            clock.Sample(150, 600, 600);
            Assert.AreEqual(150u, clock.Now, "a correction across the wrap");
            Assert.AreEqual(250, ServerClock.Diff(150, uint.MaxValue - 99));
            Assert.AreEqual(-250, ServerClock.Diff(uint.MaxValue - 99, 150));
        }

        [Test]
        public void ServerClock_BeforeStart_IsNotSynced_AndASampleAloneStartsIt()
        {
            var local = new FakeClock { NowMs = 500 };
            var clock = new ServerClock(local);
            Assert.IsFalse(clock.IsSynced);
            Assert.AreEqual(0u, clock.Now);
            clock.Sample(7_000, 400, 500);
            Assert.IsTrue(clock.IsSynced);
            Assert.AreEqual(7_050u, clock.Now);
        }

        [Test]
        public void ServerClock_Start_ForgetsTheOldServer()
        {
            var local = new FakeClock { NowMs = 0 };
            var clock = new ServerClock(local);
            clock.Start(900_000, 0);
            clock.Sample(900_000, 0, 0);
            local.NowMs = 100;
            clock.Start(5_000, 100);                   // another map server, an earlier tick
            Assert.AreEqual(5_000u, clock.Now);
            Assert.AreEqual(-1, clock.LastRoundTripMs);
        }

        [Test]
        public void ServerClock_Sample_AllocatesNothing()
        {
            var local = new FakeClock { NowMs = 0 };
            var clock = new ServerClock(local);
            clock.Start(0, 0);
            uint sum = 0;
            Action run = () =>
            {
                for (int i = 0; i < 1000; i++)
                {
                    local.NowMs += 10;
                    clock.Sample((uint)local.NowMs, local.NowMs - 5, local.NowMs);
                    sum += clock.Now;
                }
            };
            run();
            Assert.That(() => run(), UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory());
        }
    }
}
