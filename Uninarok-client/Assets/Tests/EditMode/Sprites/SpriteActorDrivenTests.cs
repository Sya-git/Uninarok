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
