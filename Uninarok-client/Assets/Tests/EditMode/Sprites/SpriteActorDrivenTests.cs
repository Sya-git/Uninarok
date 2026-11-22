using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Uninarok.Sprites.Tests
{
    // TASKS-0009 T2.1 (FR-5, FR-14; PLAN-0009 4.4): the driven mode of SpriteActor.
    public class SpriteActorDrivenTests
    {
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

        SpriteActor Driven()
        {
            var a = _data.NewActor();
            a.Driven = true;
            return a;
        }

        [Test]
        public void SpriteActor_Driven_NotAdvancedBySystem_WritesOnlyOnChange()
        {
            var a = Driven();
            Assert.That(a.ShowFrame(0, 0, 0), Is.True);
            Assert.That(a.ApplyCount, Is.EqualTo(1));
            SpriteSystem.Tick(10f);
            Assert.That(a.CurrentFrame, Is.EqualTo(0), "the system does not advance a driven actor");
            Assert.That(a.ApplyCount, Is.EqualTo(1));

            a.ShowFrame(0, 0, 0);
            Assert.That(a.ApplyCount, Is.EqualTo(1), "the same frame writes nothing");
            a.ShowFrame(0, 3, 0);
            Assert.That(a.ApplyCount, Is.EqualTo(1), "another direction of the same unique action writes nothing");
            a.ShowFrame(0, 3, 1);
            Assert.That(a.ApplyCount, Is.EqualTo(2));
            a.ShowFrame(1, 3, 1);
            Assert.That(a.ApplyCount, Is.EqualTo(3), "another unique action writes");
            Assert.That(a.CurrentFrame, Is.EqualTo(1));
        }

        [Test]
        public void SpriteActor_Driven_FrameWraps()
        {
            var a = Driven();
            a.ShowFrame(1, 0, 7);
            Assert.That(a.CurrentFrame, Is.EqualTo(1), "7 of 3 frames");
            Assert.That(a.FrameCountOf(1, 0), Is.EqualTo(3));
        }

        [Test]
        public void SpriteActor_Driven_EventsFireOnEnteringAFrame_NotOnTurning()
        {
            var a = Driven();
            var events = new List<string>();
            a.FrameEvent += (_, e) => events.Add(e);
            a.ShowFrame(0, 0, 0);
            a.ShowFrame(0, 0, 0);
            a.ShowFrame(0, 5, 0);
            CollectionAssert.AreEqual(new[] { "atk" }, events);
            a.ShowFrame(0, 5, 1);
            a.ShowFrame(0, 5, 0);
            CollectionAssert.AreEqual(new[] { "atk", "x.wav", "atk" }, events);
        }

        // ---- FR-16 (TASKS-0009 T9.1; PLAN-0009 4.10, D28): the layer's height above the ground point in its z scale ----

        static float HeightOf(SpriteActor a, int layer, float scale) => a.LayerRenderers[layer].transform.localScale.z * scale - SpriteActor.HeightOffset;

        [Test]
        public void SpriteActor_SetGround_WritesTheHeightInZScale()
        {
            var a = Driven();
            a.SetGround(0.75f, 2f);
            a.ShowFrame(0, 0, 0);       // slot 0 at (10, -20) px: 2 units up, 4 at scale 2
            Assert.That(HeightOf(a, 0, 2f), Is.EqualTo(4.75f).Within(1e-4f));
            a.ShowFrame(0, 0, 1);       // the layers are at -20 and -5 px: 2 and 0.5 units up
            Assert.That(HeightOf(a, 0, 2f), Is.EqualTo(4.75f).Within(1e-4f));
            Assert.That(HeightOf(a, 1, 2f), Is.EqualTo(1.75f).Within(1e-4f));
            var scale = a.LayerRenderers[0].transform.localScale;
            Assert.That(new Vector2(scale.x, scale.y), Is.EqualTo(new Vector2(2f, 0.5f)), "the layer's own scale stays");
        }

        [Test]
        public void SpriteActor_SetGround_SameValue_WritesNothing()
        {
            var a = Driven();
            a.SetGround(1f, 1f);
            a.ShowFrame(0, 0, 0);
            int applied = a.ApplyCount;
            a.SetGround(1f, 1f);
            a.ShowFrame(0, 0, 0);
            Assert.That(a.ApplyCount, Is.EqualTo(applied));
            a.SetGround(1.25f, 1f);
            a.ShowFrame(0, 0, 0);
            Assert.That(a.ApplyCount, Is.EqualTo(applied + 1), "another height is written, on the same frame");
            Assert.That(HeightOf(a, 0, 1f), Is.EqualTo(3.25f).Within(1e-4f));
        }

        [Test]
        public void SpriteActor_SetGround_Off_KeepsZScale1()
        {
            var a = Driven();
            a.ShowFrame(0, 0, 0);
            a.ShowFrame(0, 0, 1);
            Assert.That(a.LayerRenderers[0].transform.localScale.z, Is.EqualTo(1f));
            Assert.That(a.LayerRenderers[1].transform.localScale.z, Is.EqualTo(1f));
        }

        [Test]
        public void SpriteActor_Driven_MissingActionDrawsNothing_AndComesBack()
        {
            var a = Driven();
            a.ShowFrame(0, 0, 0);
            Assert.That(a.ShowFrame(9, 0, 0), Is.False);
            Assert.That(a.LayerRenderers[0].enabled, Is.False);
            Assert.That(a.ShowFrame(0, 0, 0), Is.True);
            Assert.That(a.LayerRenderers[0].enabled, Is.True);
        }

        [Test]
        public void SpriteActor_Driven_DataChange_TintAndSortingBase()
        {
            var a = Driven();
            a.SortingBase = 64;
            a.ShowFrame(0, 0, 1);
            Assert.That(a.LayerRenderers[1].sortingOrder, Is.EqualTo(65));
            a.SortingBase = 32;
            Assert.That(a.LayerRenderers[1].sortingOrder, Is.EqualTo(33));
            a.Tint = new Color32(255, 255, 255, 127);
            a.ShowFrame(0, 0, 1);
            Assert.That(((Color32)a.LayerRenderers[0].color).a, Is.EqualTo(63), "the layer's 128 times 127 / 255");

            a.Data = null;
            Assert.That(a.ShowFrame(0, 0, 1), Is.False);
            Assert.That(a.LayerRenderers[0].enabled, Is.False);
            a.Data = _data.Animation;
            Assert.That(a.ShowFrame(0, 0, 1), Is.True);
            Assert.That(a.LayerRenderers[0].enabled, Is.True);
            a.Hide();
            Assert.That(a.LayerRenderers[0].enabled, Is.False);
            a.ShowFrame(0, 0, 1);
            Assert.That(a.LayerRenderers[0].enabled, Is.True);
        }

        [Test]
        public void SpriteActor_Driven_AllocatesNothing()
        {
            var a = Driven();
            a.ShowFrame(0, 0, 0);
            a.ShowFrame(1, 0, 0);
            Run(a);
            Assert.That(Run(a), Is.EqualTo(0));
        }

        static long Run(SpriteActor a)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 60; i++) a.ShowFrame(i / 20 % 2, i & 7, i);
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }
    }
}
