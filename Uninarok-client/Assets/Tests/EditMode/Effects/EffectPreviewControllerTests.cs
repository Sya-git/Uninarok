using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Uninarok.Effects.Editor;

namespace Uninarok.Effects.Tests
{
    /// <summary>
    /// FR-11: the browse list built from the project's effect assets (the index asset does not exist yet) and the play state of
    /// the preview window, driven without a window. The window's own drawing is not covered here.
    /// </summary>
    public sealed class EffectPreviewControllerTests
    {
        const string SparkleKey = "texture/effect/_authored/warp_portal/sparkle.quad";

        EffectBrowseSource source;
        EffectPreviewStage stage;
        EffectPreviewController player;

        [SetUp]
        public void SetUp()
        {
            source = EffectBrowseSource.Load();
            stage = new EffectPreviewStage(source.CreateCatalog());
            player = new EffectPreviewController(stage);
        }

        [TearDown]
        public void TearDown()
        {
            stage?.Dispose();
            source?.Dispose();
        }

        [Test]
        public void FilterWarpPortal_ListsExactlyTheThreeWarpKeys_FR11()
        {
            var keys = source.Keys;
            var hits = EffectBrowse.Filter(keys, "warp_portal").Select(i => keys[i]).ToArray();
            Assert.That(hits, Is.EqualTo(new[]
            {
                "texture/effect/_authored/warp_portal/ring_inner.cylinder",
                "texture/effect/_authored/warp_portal/ring_outer.cylinder",
                "texture/effect/_authored/warp_portal/sparkle.quad",
            }));
            Assert.That(keys.SequenceEqual(keys.OrderBy(k => k, System.StringComparer.Ordinal)), Is.True, "keys sorted ordinal");
        }

        [Test]
        public void Tick_NTicksOfDt_AdvanceTheAgeByExactlyNDt_AtSpeedOne_FR11()
        {
            Assert.That(player.Play(SparkleKey), Is.True);
            player.SetLoop(true);
            float dt = 0.01f;
            for (int i = 0; i < 10; i++) player.Tick(dt);
            Assert.That(player.Seconds, Is.EqualTo(10 * dt).Within(1e-4f));
        }

        [Test]
        public void Step_ChangesTheShownFrameByOne_FR11()
        {
            Assert.That(player.Play(SparkleKey), Is.True);
            Assert.That(stage.Runtime.ActiveCount, Is.EqualTo(1));
            Assert.That(player.Length, Is.GreaterThan(0f));
            Assert.That(player.FrameRate, Is.EqualTo(stage.Runtime.Settings.AuthoredFrameRate), "authored entries step at AuthoredFrameRate");

            player.Pause();
            player.SeekFrame(10);
            Assert.That(player.ShownFrame, Is.EqualTo(10));
            player.Step(1);
            Assert.That(player.ShownFrame, Is.EqualTo(11));
            Assert.That(player.Paused, Is.True, "a step pauses");
            player.Step(-1);
            Assert.That(player.ShownFrame, Is.EqualTo(10));
            player.Step(-1);
            Assert.That(player.ShownFrame, Is.EqualTo(9));
        }

        [Test]
        public void Step_ClampsAtFrameZeroAndLastFrame_FR11()
        {
            player.Play(SparkleKey);
            player.Stop();
            Assert.That(player.ShownFrame, Is.EqualTo(0));
            Assert.That(player.Paused, Is.True);
            player.Step(-1);
            Assert.That(player.ShownFrame, Is.EqualTo(0));

            int last = player.LastFrame;
            Assert.That(last, Is.GreaterThan(0));
            player.SeekFrame(last);
            Assert.That(player.ShownFrame, Is.EqualTo(last));
            player.Step(1);
            Assert.That(player.ShownFrame, Is.EqualTo(last));
        }

        [Test]
        public void PauseHoldsTheFrameAndResumeAdvances_FR11()
        {
            player.Play(SparkleKey);
            player.Pause();
            player.SeekFrame(5);
            float before = player.Seconds;
            player.Tick(0.05f);
            Assert.That(player.Seconds, Is.EqualTo(before), "paused: TimeScale 0");

            player.Resume();
            player.Tick(0.05f);
            Assert.That(player.Seconds, Is.GreaterThan(before));
        }

        [Test]
        public void SpeedIsClampedAndScalesTheClock_FR11()
        {
            player.Play(SparkleKey);
            Assert.That(player.SetSpeed(10f), Is.EqualTo(4f));
            Assert.That(stage.Runtime.SpeedOf(player.Handle), Is.EqualTo(4f));
            Assert.That(player.SetSpeed(0f), Is.EqualTo(0.1f));
            Assert.That(player.SetSpeed(2f), Is.EqualTo(2f));

            player.Pause();
            player.SeekSeconds(0f);
            player.Resume();
            player.Tick(0.01f);
            Assert.That(player.Seconds, Is.EqualTo(0.02f).Within(1e-4f));
        }

        [Test]
        public void LoopOffEndsTheEffectAndStepRestartsIt_FR11()
        {
            player.Play(SparkleKey);
            player.SetLoop(false);
            Assert.That(player.Loop, Is.False);
            player.Tick(player.Length + 0.5f);
            Assert.That(player.Ended, Is.True);
            Assert.That(stage.Runtime.ActiveCount, Is.EqualTo(0));
            Assert.That(player.ShownFrame, Is.EqualTo(player.LastFrame));

            player.Step(-1);
            Assert.That(player.Ended, Is.False);
            Assert.That(stage.Runtime.ActiveCount, Is.EqualTo(1));
            Assert.That(player.ShownFrame, Is.EqualTo(player.LastFrame - 1));
        }

        [Test]
        public void UnknownKeyPlaysNothing_FR11()
        {
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("unknown path"));
            Assert.That(player.Play("texture/effect/no_such_effect.str"), Is.False);
            Assert.That(player.HasEffect, Is.False);
            Assert.That(stage.Runtime.ActiveCount, Is.EqualTo(0));
            player.Step(1);
            player.Tick(0.1f);
        }
    }
}
