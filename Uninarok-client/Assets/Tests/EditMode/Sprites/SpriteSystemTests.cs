using System;
using NUnit.Framework;
using UnityEngine;

namespace Uninarok.Sprites.Tests
{
    // TASKS-0007 T2.2 / T2.3 (FR-3, plan 4.6, D4): one loop for every actor, no allocation per frame.
    [Ignore("pending T2.3")]
    public class SpriteSystemTests
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

        [Test]
        public void EnabledActors_Register_AndDisabledOnesLeave()
        {
            var a = _data.NewActor("a");
            var b = _data.NewActor("b");
            Assert.AreEqual(2, SpriteSystem.Count);

            a.Play(0, 0);
            b.Play(0, 0);
            a.enabled = false;
            Assert.AreEqual(1, SpriteSystem.Count);

            SpriteSystem.Tick(0.1f);
            Assert.AreEqual(0, a.CurrentFrame, "a disabled actor does not advance");
            Assert.AreEqual(1, b.CurrentFrame);

            a.enabled = true;
            Assert.AreEqual(2, SpriteSystem.Count);
        }

        [Test]
        public void ThousandActors_AllocateNothing_PerFrame()
        {
            var actors = new SpriteActor[1000];
            for (int i = 0; i < actors.Length; i++)
            {
                actors[i] = _data.NewActor("actor" + i);
                actors[i].Play(i % 2, i % 8);
            }
            int events = 0;
            foreach (var a in actors) a.FrameEvent += (actor, name) => events++;
            for (int i = 0; i < 5; i++) SpriteSystem.Tick(0.03f);   // warm up: first frames, renderers, events

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 60; i++) SpriteSystem.Tick(0.016f);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(1000, SpriteSystem.Count);
            Assert.Greater(events, 0, "frames changed and fired events during the measure");
            Assert.AreEqual(0, allocated, "bytes allocated by 60 ticks of 1,000 actors");
        }

        [Test]
        public void Billboard_TurnsTheActorToTheCameraYaw()
        {
            var cameraObject = new GameObject("camera");
            _data.Owned.Add(cameraObject);
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.rotation = Quaternion.Euler(40f, 30f, 0f);
            SpriteSystem.Camera = camera;

            var a = _data.NewActor();
            var fixedOne = _data.NewActor("fixed");
            fixedOne.Billboard = false;
            a.Play(0, 0);
            SpriteSystem.Tick(0f);

            Assert.AreEqual(30f, a.transform.eulerAngles.y, 1e-3f);
            Assert.AreEqual(0f, a.transform.eulerAngles.x, 1e-3f, "yaw only");
            Assert.AreEqual(0f, fixedOne.transform.eulerAngles.y, 1e-3f);

            camera.transform.rotation = Quaternion.Euler(40f, 75f, 0f);
            SpriteSystem.Tick(0f);
            Assert.AreEqual(75f, a.transform.eulerAngles.y, 1e-3f);
        }
    }
}
