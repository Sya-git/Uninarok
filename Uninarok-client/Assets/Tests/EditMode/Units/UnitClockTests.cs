using NUnit.Framework;
using Uninarok.Sprites;

namespace Uninarok.Units.Tests
{
    // TASKS-0009 T3.2 (FR-5, FR-7, FR-8; PLAN-0009 4.4, D9, D19)
    public class UnitClockTests
    {
        static SpriteAnimation.ActionData Action(int frames, float ms) => new SpriteAnimation.ActionData { FrameCount = frames, FrameMs = ms };

        static UnitClock Clock(PlayMode mode = PlayMode.Loop, int animation = (int)PcAnimation.Walk)
        {
            var c = new UnitClock();
            c.Play(animation, mode);
            return c;
        }

        [Test]
        public void UnitClock_BodyDrivesFrame_TrailOf150msFollows100msBody()
        {
            var body = Action(8, 100f);
            var c = Clock();
            for (int i = 0; i < 40; i++)
            {
                int frame = c.Advance(i == 0 ? 0f : 50f, body, -1, out _);
                Assert.That(frame, Is.EqualTo(i / 2 % 8), $"at {i * 50} ms");
                Assert.That(UnitClock.PartFrame(frame, 8, -1, c.Ticks), Is.EqualTo(frame), "a trail with its own 150 ms shows the body's frame");
                Assert.That(UnitClock.PartFrame(frame, 5, -1, c.Ticks), Is.EqualTo(frame % 5), "fewer frames wrap");
            }
        }

        [Test]
        public void UnitClock_NoDriftAfter10MinutesSimulated()
        {
            var body = Action(8, 100f);
            var c = Clock();
            const float dt = 1000f / 60f;
            int frame = 0;
            const int frames = 10 * 60 * 60;
            for (int i = 0; i < frames; i++)
            {
                frame = c.Advance(dt, body, -1, out _);
                Assert.That(c.Ticks, Is.LessThan(8));
                Assert.That(UnitClock.PartFrame(frame, 8, -1, c.Ticks), Is.EqualTo(frame));
            }
            // 600,000 ms = 6,000 frame times = 750 loops: back at the start, give or take one frame of float rounding.
            Assert.That(frame, Is.EqualTo(0).Or.EqualTo(7));
        }

        [Test]
        public void UnitClock_OnceHoldStops()
        {
            var body = Action(4, 100f);
            var c = Clock(PlayMode.OnceHold, (int)PcAnimation.Dead);
            Assert.That(c.Advance(250f, body, -1, out bool ended), Is.EqualTo(2));
            Assert.That(ended, Is.False);
            Assert.That(c.Advance(500f, body, -1, out ended), Is.EqualTo(3));
            Assert.That(c.Advance(10000f, body, -1, out ended), Is.EqualTo(3), "holds the last frame");
            Assert.That(ended, Is.False, "hold does not report");
            Assert.That(c.Finished, Is.True);
        }

        [Test]
        public void UnitClock_OnceEndReportsOnce()
        {
            var body = Action(4, 100f);
            var c = Clock(PlayMode.OnceEnd, (int)PcAnimation.Attack1);
            c.Advance(350f, body, -1, out bool ended);
            Assert.That(ended, Is.False, "the last frame is still being shown");
            int frame = c.Advance(50f, body, -1, out ended);
            Assert.That(ended, Is.True);
            Assert.That(frame, Is.EqualTo(3));
            for (int i = 0; i < 10; i++)
            {
                c.Advance(100f, body, -1, out ended);
                Assert.That(ended, Is.False);
            }
            c.Play((int)PcAnimation.Attack1, PlayMode.OnceEnd);
            c.Advance(1000f, body, -1, out ended);
            Assert.That(ended, Is.True, "playing again reports again, even over a long frame");
        }

        [Test]
        public void UnitClock_IdleAndSit_FrameIsHeadFacing()
        {
            var body = Action(3, 100f);
            foreach (var animation in new[] { PcAnimation.Idle, PcAnimation.Sit })
            {
                Assert.That(UnitAnimations.IsHeadFrame((int)animation), Is.True);
                var c = Clock(PlayMode.Loop, (int)animation);
                foreach (var head in new[] { HeadFacing.Straight, HeadFacing.Left, HeadFacing.Right })
                    for (int i = 0; i < 10; i++) Assert.That(c.Advance(70f, body, (int)head, out _), Is.EqualTo((int)head));
            }
            Assert.That(UnitAnimations.IsHeadFrame((int)PcAnimation.Walk), Is.False);
        }

        [Test]
        public void UnitClock_HeadFacingIgnoredWhileWalking_ShowsAgainOnIdle()
        {
            var c = Clock(PlayMode.Loop, (int)PcAnimation.Idle);
            Assert.That(c.Advance(10f, Action(3, 100f), (int)HeadFacing.Left, out _), Is.EqualTo(2));
            c.Play((int)PcAnimation.Walk, PlayMode.Loop);
            Assert.That(c.Advance(250f, Action(8, 100f), -1, out _), Is.EqualTo(2), "two frame times");
            Assert.That(c.Advance(100f, Action(8, 100f), -1, out _), Is.EqualTo(3));
            c.Play((int)PcAnimation.Idle, PlayMode.Loop);
            Assert.That(c.Advance(10f, Action(3, 100f), (int)HeadFacing.Left, out _), Is.EqualTo(2));
        }

        [Test]
        public void UnitClock_AnimatedHeadgear_PlaysItsGroup_ForTheHeadFacing()
        {
            var body = Action(3, 100f);
            var c = Clock(PlayMode.Loop, (int)PcAnimation.Idle);
            for (int i = 0; i < 20; i++)
            {
                int frame = c.Advance(i == 0 ? 0f : 100f, body, (int)HeadFacing.Right, out _);
                Assert.That(UnitClock.PartFrame(frame, 24, (int)HeadFacing.Right, c.Ticks), Is.EqualTo(8 + i % 8), "8 frames per head facing");
                Assert.That(UnitClock.PartFrame(frame, 3, (int)HeadFacing.Right, c.Ticks), Is.EqualTo(1));
            }
        }

        [Test]
        public void UnitClock_Speed_And_SetFrame()
        {
            var body = Action(4, 100f);
            var c = Clock();
            c.Speed = 2f;
            Assert.That(c.Advance(150f, body, -1, out _), Is.EqualTo(0));
            Assert.That(c.Advance(50f, body, -1, out _), Is.EqualTo(1));
            c.SetFrame(3);
            Assert.That(c.FrameOf(4), Is.EqualTo(3));
        }
    }

    // TASKS-0009 T3.3 (FR-7)
    public class UnitAnimationsTests
    {
        [TestCase(PcAnimation.Attack1, MonsterAnimation.Attack)]
        [TestCase(PcAnimation.Attack2, MonsterAnimation.Attack)]
        [TestCase(PcAnimation.Attack3, MonsterAnimation.Attack)]
        [TestCase(PcAnimation.Dead, MonsterAnimation.Die)]
        [TestCase(PcAnimation.Hurt, MonsterAnimation.Hurt)]
        [TestCase(PcAnimation.Walk, MonsterAnimation.Walk)]
        [TestCase(PcAnimation.Idle, MonsterAnimation.Idle)]
        [TestCase(PcAnimation.Sit, MonsterAnimation.Idle)]
        [TestCase(PcAnimation.Cast, MonsterAnimation.Idle)]
        [TestCase(PcAnimation.PickUp, MonsterAnimation.Idle)]
        public void ToUnit_MapsByName(PcAnimation player, MonsterAnimation unit) => Assert.That(UnitAnimations.ToUnit(player), Is.EqualTo(unit));

        [Test]
        public void TheValues_AreTheDocumentedOnes()
        {
            Assert.That((int)PcAnimation.Standby, Is.EqualTo(4));
            Assert.That((int)PcAnimation.Dead * 8, Is.EqualTo(64));
            Assert.That((int)PcAnimation.Cast * 8, Is.EqualTo(96));
            Assert.That((int)MonsterAnimation.Die * 8, Is.EqualTo(32));
        }
    }
}
