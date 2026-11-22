using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Uninarok.Networking.Data;

namespace Uninarok.Units.PlayTests
{
    // TASKS-0009 T5.1 to T5.4, T6.3 (FR-5 to FR-8, FR-11; PLAN-0009 4.6) on the imported catalog.
    [Category("NeedsData")]
    public class UnitViewPlayTests
    {
        AddressablesUnitAssetSource _source;
        UnitLoader _loader;
        Camera _camera;
        readonly List<UnitView> _units = new List<UnitView>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return UnitPlayData.LoadSource(s => _source = s);
            _loader = new UnitLoader(_source);
            _camera = new GameObject("Camera").AddComponent<Camera>();
            _camera.transform.rotation = Quaternion.Euler(50f, 0f, 0f);
            UnitSystem.Camera = _camera;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var u in _units)
                if (u != null) UnityEngine.Object.Destroy(u.gameObject);
            _units.Clear();
            UnitSystem.Camera = null;
            if (_camera != null) UnityEngine.Object.Destroy(_camera.gameObject);
            yield return null;
            Assert.That(_source.HeldCount, Is.EqualTo(0), "destroyed units release everything");
            _source.Dispose();
        }

        UnitView Spawn(in UnitLook look)
        {
            var view = UnitView.Create(_loader, null);
            view.SetLook(look);
            _units.Add(view);
            return view;
        }

        IEnumerator WaitLoaded()
        {
            for (int i = 0; i < 600; i++)
            {
                bool all = true;
                foreach (var u in _units) all &= u.Loaded;
                if (all) yield break;
                yield return null;
            }
            Assert.Fail("the units did not load in time");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator UnitView_ReferenceSet_PlaysEveryAnimation()
        {
            foreach (var look in UnitPlayData.ReferenceLooks()) Spawn(look);
            yield return WaitLoaded();

            foreach (PcAnimation animation in Enum.GetValues(typeof(PcAnimation)))
            {
                foreach (var u in _units) u.Play(animation);
                yield return null;
                yield return null;
                foreach (var u in _units)
                {
                    Assert.That(u.Frame, Is.GreaterThanOrEqualTo(0), $"{u.Look.JobId} {animation}");
                    Assert.That(u.ActorOf(PartSlot.Body).HasAction, Is.True, $"{u.Look.JobId} {animation}: body falls back");
                }
            }
            var kafra = _units[6];
            kafra.Play(PcAnimation.Walk);
            yield return null;
            Assert.That(kafra.AnimationNumber, Is.EqualTo((int)MonsterAnimation.Walk), "mapped to the unit's family");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator UnitSystem_TicksOncePerFrame_WithAStrayDriver()
        {
            Spawn(UnitPlayData.Novice());
            yield return WaitLoaded();
            var stray = UnitSystemDriver.Create(); // as a driver left over from an earlier play session
            Assert.That(stray.gameObject.hideFlags & HideFlags.DontSave, Is.EqualTo(HideFlags.None), "leaves with play mode");
            yield return null;
            int ticks = UnitSystem.TickCount, frames = Time.frameCount;
            for (int i = 0; i < 10; i++) yield return null;
            Assert.That(UnitSystem.TickCount - ticks, Is.EqualTo(Time.frameCount - frames));
            Assert.That(stray == null, Is.True, "the stray one is gone");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator UnitView_DeadHolds_AttackOnceReportsItsEnd()
        {
            var u = Spawn(UnitPlayData.Swordsman());
            yield return WaitLoaded();
            int ended = 0;
            u.AnimationEnded += _ => ended++;

            u.Speed = 0.02f; // fifty times faster
            u.Play(PcAnimation.Dead, PlayMode.OnceHold);
            for (int i = 0; i < 30; i++) yield return null;
            Assert.That(ended, Is.EqualTo(0));
            int count = u.ActorOf(PartSlot.Body).FrameCountOf((int)PcAnimation.Dead, 0);
            Assert.That(u.Frame, Is.EqualTo(count - 1), "the last frame stays");

            u.Play(PcAnimation.Attack3, PlayMode.OnceEnd);
            for (int i = 0; i < 60; i++) yield return null;
            Assert.That(ended, Is.EqualTo(1), "reported once");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator UnitView_PivotTakesTheFullCameraRotation_OnlyWhenItChanged()
        {
            var u = Spawn(UnitPlayData.Novice());
            yield return WaitLoaded();
            yield return null;
            Assert.That(Quaternion.Angle(u.Pivot.rotation, _camera.transform.rotation), Is.LessThan(0.01f));

            _camera.transform.rotation = Quaternion.Euler(62f, 140f, 0f);
            yield return null;
            yield return null;
            Assert.That(Quaternion.Angle(u.Pivot.rotation, _camera.transform.rotation), Is.LessThan(0.01f), "turn and tilt");

            var sentinel = Quaternion.Euler(1f, 2f, 3f);
            u.Pivot.rotation = sentinel;
            yield return null;
            yield return null;
            Assert.That(Quaternion.Angle(u.Pivot.rotation, sentinel), Is.LessThan(0.01f), "not written while the camera stands still");
            _camera.transform.Rotate(0f, 0.01f, 0f, Space.World);
            yield return null;
            yield return null;
            Assert.That(Quaternion.Angle(u.Pivot.rotation, _camera.transform.rotation), Is.LessThan(0.01f), "even for a very slow turn");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator UnitView_HeadAndBodyTurnEveryFrame_Doridori()
        {
            var u = Spawn(UnitPlayData.Swordsman());
            yield return WaitLoaded();
            u.Play(PcAnimation.Idle);
            for (int i = 0; i < 48; i++)
            {
                u.HeadFacing = (HeadFacing)(i % 3);
                u.Facing = (UninarokDirection)(i % 8);
                UnitSystem.Tick(0.016f);
                int direction = UnitFacing.Shown(u.Facing, UnitSystem.Sector);
                Assert.That(u.Frame, Is.EqualTo(i % 3), "the body's frame is the head facing");
                Assert.That(u.ShownDirection, Is.EqualTo(direction));
                foreach (var slot in new[] { PartSlot.Body, PartSlot.Head, PartSlot.HeadTop, PartSlot.HeadBottom, PartSlot.Garment, PartSlot.Weapon, PartSlot.Shield })
                    Assert.That(u.ActorOf(slot).Direction, Is.EqualTo(direction), slot.ToString());
                Assert.That(u.ActorOf(PartSlot.Head).CurrentFrame, Is.EqualTo(i % 3));
            }
        }

        // T6.3 (FR-6)
        [UnityTest, Timeout(120000)]
        public IEnumerator UnitSystem_AllPartsSwitchSectorSameFrame()
        {
            foreach (var look in UnitPlayData.ReferenceLooks()) Spawn(look);
            yield return WaitLoaded();
            foreach (var u in _units) u.Play(PcAnimation.Walk);

            int switches = 0, last = -1;
            for (float yaw = 0f; yaw < 100f; yaw += 0.5f)
            {
                _camera.transform.rotation = Quaternion.Euler(50f, yaw, 0f);
                UnitSystem.Tick(0.016f);
                int sector = UnitFacing.CameraSector(yaw);
                Assert.That(UnitSystem.Sector, Is.EqualTo(sector), $"yaw {yaw}");
                if (sector != last) switches++;
                last = sector;
                int expected = UnitFacing.Shown(UninarokDirection.South, sector);
                foreach (var u in _units)
                    for (int s = (int)PartSlot.Body; s <= (int)PartSlot.Shield; s++)
                    {
                        var actor = u.ActorOf((PartSlot)s);
                        if (actor != null && actor.Data != null) Assert.That(actor.Direction, Is.EqualTo(expected), $"{u.Look.JobId} {(PartSlot)s} at yaw {yaw}");
                    }
                if (yaw % 10f == 0f) yield return null;
            }
            Assert.That(switches, Is.EqualTo(3));
        }

        // T5.1 (FR-5): parts keep the body's frame while walking and turning.
        [UnityTest, Timeout(120000)]
        public IEnumerator UnitView_Walking_EveryPartShowsTheBodysFrame()
        {
            var u = Spawn(UnitPlayData.Swordsman());
            yield return WaitLoaded();
            u.Play(PcAnimation.Walk);
            for (int i = 0; i < 2000; i++)
            {
                u.Facing = (UninarokDirection)(i / 13 % 8);
                int before = u.Frame;
                UnitSystem.Tick(0.0167f);
                foreach (var slot in new[] { PartSlot.Head, PartSlot.Garment, PartSlot.Weapon, PartSlot.Shield })
                {
                    var actor = u.ActorOf(slot);
                    int count = actor.FrameCountOf((int)PcAnimation.Walk, u.ShownDirection);
                    if (count > 0) Assert.That(actor.CurrentFrame, Is.EqualTo(u.Frame % count), $"{slot} at step {i}");
                }
            }
        }

        // T5.4 (FR-11)
        [UnityTest, Timeout(120000)]
        public IEnumerator UnitShadow_ScalesOfTheAcceptance()
        {
            var poring = Spawn(UnitLook.ForJob(1002));
            var novice = Spawn(UnitPlayData.Novice());
            var fabre = Spawn(UnitLook.ForJob(1007));
            var warp = Spawn(UnitLook.ForJob(45));
            var plant = Spawn(UnitLook.ForJob(1078));
            yield return WaitLoadedShadow(poring, novice, fabre, plant);
            yield return null;
            Assert.That((poring.Shadow.Scale, poring.Shadow.Visible), Is.EqualTo((1f, true)));
            Assert.That((novice.Shadow.Scale, novice.Shadow.Visible), Is.EqualTo((1f, true)));
            Assert.That((fabre.Shadow.Scale, fabre.Shadow.Visible), Is.EqualTo((0.5f, true)));
            Assert.That(plant.Shadow.Visible, Is.False);
            Assert.That(warp.Shadow.Visible, Is.False);
            Assert.That(poring.Shadow.Renderer.transform.up.y, Is.EqualTo(0f).Within(1e-3f), "flat on the ground");
            novice.Play(PcAnimation.Sit);
            yield return null;
            yield return null;
            Assert.That(novice.Shadow.Visible, Is.False, "no shadow while sitting");
        }

        IEnumerator WaitLoadedShadow(params UnitView[] views)
        {
            for (int i = 0; i < 600; i++)
            {
                bool all = true;
                foreach (var v in views) all &= v.Loaded;
                if (all) yield break;
                yield return null;
            }
            Assert.Fail("the units did not load in time");
        }
    }
}
