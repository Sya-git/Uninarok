using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Uninarok.Sprites.Tests
{
    // TASKS-0007 T2.2 / T2.3 (FR-3, FR-5, FR-6, plan 4.3, 4.6). Time is advanced with SpriteSystem.Tick, never by the
    // editor loop, so the results do not depend on frame rate.
    [Ignore("pending T2.3")]
    public class SpriteActorTests
    {
        const float Eps = 1e-4f;
        SpriteActorTestData _data;

        [SetUp]
        public void SetUp()
        {
            SpriteSystem.ResetForTests();
            _data = new SpriteActorTestData();
        }

        [TearDown]
        public void TearDown()
        {
            _data.Destroy();
            SpriteSystem.ResetForTests();
        }

        static SpriteRenderer R(SpriteActor a, int slot) => a.LayerRenderers[slot];

        static void AssertNear(Vector3 expected, Vector3 actual, string what)
        {
            Assert.AreEqual(expected.x, actual.x, Eps, what + " x");
            Assert.AreEqual(expected.y, actual.y, Eps, what + " y");
            Assert.AreEqual(expected.z, actual.z, Eps, what + " z");
        }

        [Test]
        public void TheActor_HasOneRendererPerLayerSlot()
        {
            var a = _data.NewActor();

            Assert.AreEqual(2, a.LayerRenderers.Count);
            Assert.AreEqual("Layer_0", R(a, 0).gameObject.name);
            Assert.AreEqual("Layer_1", R(a, 1).gameObject.name);
            Assert.Less(R(a, 0).sortingOrder, R(a, 1).sortingOrder, "later layers on top");
            Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, R(a, 0).shadowCastingMode);
        }

        [Test]
        public void FirstFrame_SetsSpritePosition_AndHidesTheUnusedSlot()
        {
            var a = _data.NewActor();
            a.Play(0, 0);
            SpriteSystem.Tick(0f);

            Assert.IsTrue(R(a, 0).enabled);
            Assert.AreEqual("f0", R(a, 0).sprite.name);
            AssertNear(new Vector3(1f, 2f, 0f), R(a, 0).transform.localPosition, "offset / PPU, Y up");
            Assert.IsFalse(R(a, 1).enabled, "unused slot");
        }

        [Test]
        public void SecondFrame_AppliesMirrorScaleRotationTintAndDrawOrder()
        {
            var a = _data.NewActor();
            a.Play(0, 0);
            SpriteSystem.Tick(0.1f);

            Assert.AreEqual(1, a.CurrentFrame);
            var top = R(a, 0);
            Assert.AreEqual("f1", top.sprite.name);
            Assert.IsTrue(top.flipX, "mirror");
            AssertNear(new Vector3(2f, 0.5f, 1f), top.transform.localScale, "scale");
            Assert.AreEqual(270f, top.transform.localEulerAngles.z, 1e-3f, "90 degrees clockwise on screen");
            Assert.AreEqual(new Color(1f, 0f, 0f, 128f / 255f), top.color);
            Assert.IsTrue(R(a, 1).enabled);
            Assert.AreEqual("f2", R(a, 1).sprite.name);
            Assert.AreEqual(0.5f, R(a, 1).transform.localPosition.x, Eps);
            Assert.AreEqual(0.5f, R(a, 1).transform.localPosition.y, Eps);
            Assert.Less(R(a, 1).transform.localPosition.z, R(a, 0).transform.localPosition.z, "a later layer is nearer the camera");
        }

        [Test]
        public void Frames_AdvanceWithFrameTime_ScaledBySpeed()
        {
            var a = _data.NewActor();
            a.Play(0, 0);
            SpriteSystem.Tick(0.099f);
            Assert.AreEqual(0, a.CurrentFrame);
            SpriteSystem.Tick(0.002f);
            Assert.AreEqual(1, a.CurrentFrame);
            SpriteSystem.Tick(0.1f);
            Assert.AreEqual(0, a.CurrentFrame, "every action loops");

            a.Speed = 2f;
            a.Play(0, 0);
            SpriteSystem.Tick(0.15f);
            Assert.AreEqual(0, a.CurrentFrame, "speed 2 doubles the frame time");
            SpriteSystem.Tick(0.06f);
            Assert.AreEqual(1, a.CurrentFrame);
        }

        [Test]
        public void Renderers_AreTouchedOnlyWhenTheFrameChanges()
        {
            var a = _data.NewActor();
            a.Play(0, 0);
            SpriteSystem.Tick(0f);
            int applied = a.ApplyCount;

            for (int i = 0; i < 9; i++) SpriteSystem.Tick(0.01f);
            Assert.AreEqual(applied, a.ApplyCount, "same frame: nothing written");

            SpriteSystem.Tick(0.02f);
            Assert.AreEqual(applied + 1, a.ApplyCount, "frame change: written once");
        }

        [Test]
        public void AnEmptyLayer_HidesItsSlot()
        {
            var a = _data.NewActor();
            a.Play(1, 0);
            SpriteSystem.Tick(0f);
            Assert.IsTrue(R(a, 0).enabled);

            SpriteSystem.Tick(0.05f);
            Assert.AreEqual(1, a.CurrentFrame);
            Assert.IsFalse(R(a, 0).enabled, "layer −1 draws nothing");
            Assert.IsFalse(R(a, 1).enabled);
        }

        [Test]
        public void AnEmptyAction_DrawsNothing_EvenWithFallback()
        {
            var a = _data.NewActor();
            a.Missing = SpriteMissingAction.Fallback;
            a.Play(2, 3);
            SpriteSystem.Tick(0f);

            Assert.IsTrue(a.HasAction, "the action exists");
            Assert.IsFalse(R(a, 0).enabled);
            Assert.IsFalse(R(a, 1).enabled);
        }

        [Test]
        public void AMissingAction_DrawsNothing_OrFallsBack()
        {
            var a = _data.NewActor();
            a.Play(5, 2);
            SpriteSystem.Tick(0f);
            Assert.IsFalse(a.HasAction);
            Assert.IsFalse(R(a, 0).enabled);

            a.Missing = SpriteMissingAction.Fallback;
            a.Play(5, 2);
            SpriteSystem.Tick(0f);
            Assert.IsTrue(a.HasAction);
            Assert.IsTrue(R(a, 0).enabled);
            Assert.AreEqual("f0", R(a, 0).sprite.name, "idle in the requested direction");
        }

        [Test]
        public void Anchors_AreInLocalSpace_ForTheCurrentFrame()
        {
            var a = _data.NewActor();
            a.Play(0, 0);
            SpriteSystem.Tick(0f);

            Assert.IsTrue(a.TryGetAnchor(0, out var p));
            AssertNear(new Vector3(0.1f, 5.6f, 0f), p, "anchor / PPU, Y up");
            Assert.IsFalse(a.TryGetAnchor(1, out _));

            SpriteSystem.Tick(0.1f);
            Assert.IsTrue(a.TryGetAnchor(0, out p));
            AssertNear(new Vector3(0.2f, 5.7f, 0f), p, "follows the frame");
        }

        [Test]
        public void FrameEvents_FireOncePerFrameEntry()
        {
            var a = _data.NewActor();
            var events = new List<string>();
            a.FrameEvent += (actor, name) => events.Add(name);

            a.Play(0, 0);
            SpriteSystem.Tick(0f);
            SpriteSystem.Tick(0.01f);
            CollectionAssert.AreEqual(new[] { "atk" }, events);

            SpriteSystem.Tick(0.1f);
            SpriteSystem.Tick(0.1f);
            CollectionAssert.AreEqual(new[] { "atk", "x.wav", "atk" }, events, "re-entering frame 0 on loop fires again");
        }

        [Test]
        public void FrameEvents_OfSkippedFrames_StillFire()
        {
            var a = _data.NewActor();
            var events = new List<string>();
            a.FrameEvent += (actor, name) => events.Add(name);
            a.Play(0, 0);
            SpriteSystem.Tick(0f);
            events.Clear();

            SpriteSystem.Tick(0.2f);   // passes frame 1 and lands on frame 0 again
            CollectionAssert.AreEqual(new[] { "x.wav", "atk" }, events);
        }

        [Test]
        public void PlayWithoutRestart_KeepsTheFrame_RestartGoesBackToZero()
        {
            var a = _data.NewActor();
            a.Play(0, 0);
            SpriteSystem.Tick(0.15f);
            Assert.AreEqual(1, a.CurrentFrame);

            a.Play(0, 4, restart: false);
            SpriteSystem.Tick(0f);
            Assert.AreEqual((0, 4, 1), (a.Animation, a.Direction, a.CurrentFrame), "direction change keeps the frame");

            a.Play(0, 4);
            SpriteSystem.Tick(0f);
            Assert.AreEqual(0, a.CurrentFrame);
        }
    }
}
