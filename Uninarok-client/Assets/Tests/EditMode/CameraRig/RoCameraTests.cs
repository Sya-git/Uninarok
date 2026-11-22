using System;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;
using Uninarok.Units;
using Object = UnityEngine.Object;

namespace Uninarok.CameraRig.Tests
{
    // TASKS-0009 T6.1 (FR-12; PLAN-0009 4.7, D8): the rig is stepped by hand and Cinemachine's state is read directly.
    public class RoCameraTests
    {
        RoCameraSettings _settings;
        RoCamera _camera;

        [SetUp]
        public void SetUp()
        {
            _settings = ScriptableObject.CreateInstance<RoCameraSettings>();
            _camera = RoCameraRig.Create(_settings);
            _camera.ResetNow();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_camera.gameObject);
            Object.DestroyImmediate(_settings);
        }

        void Run(float seconds, float fps = 60f)
        {
            int n = Mathf.RoundToInt(seconds * fps);
            for (int i = 0; i < n; i++) _camera.Step(1f / fps);
        }

        // Cinemachine's result for the current axes and target.
        CameraState State()
        {
            _camera.Camera.InternalUpdateCameraState(Vector3.up, -1f);
            return _camera.Camera.State;
        }

        [Test]
        public void RoCamera_Clamps_Eases_Resets()
        {
            _camera.Zoom(-100f);
            Assert.That(_camera.WantedDistance, Is.EqualTo(23f));
            Assert.That(_camera.Distance, Is.EqualTo(40f), "not yet moved");
            _camera.Step(1f / 60f);
            Assert.That(_camera.Distance, Is.LessThan(40f).And.GreaterThan(30f), "eases, does not jump");
            Run(1f);
            Assert.That(_camera.Distance, Is.EqualTo(23f).Within(0.01f));
            _camera.Zoom(1000f);
            Run(2f);
            Assert.That(_camera.Distance, Is.EqualTo(40f).Within(0.01f));

            _camera.Tilt(100f);
            Run(2f);
            Assert.That(_camera.Pitch, Is.EqualTo(65f).Within(0.01f));
            _camera.Tilt(-100f);
            Run(2f);
            Assert.That(_camera.Pitch, Is.EqualTo(50f).Within(0.01f));

            _camera.Turn(350f);
            Run(2f);
            Assert.That(_camera.Yaw, Is.EqualTo(350f).Within(0.05f));
            _camera.ResetTurn();
            Assert.That(_camera.WantedYaw - 350f, Is.EqualTo(10f).Within(0.1f), "the short way back");
            _camera.Zoom(-5f);
            _camera.Tilt(10f);
            _camera.ResetZoom();
            _camera.ResetTilt();
            Run(2f);
            Assert.That(Mathf.DeltaAngle(_camera.Yaw, 0f), Is.EqualTo(0f).Within(0.05f));
            Assert.That(_camera.Distance, Is.EqualTo(_settings.DefaultDistance).Within(0.01f));
            Assert.That(_camera.Pitch, Is.EqualTo(_settings.DefaultPitch).Within(0.01f));
        }

        [Test]
        public void RoCamera_TurnsFreelyAllTheWayRound()
        {
            for (int i = 0; i < 100; i++) _camera.Turn(90f);
            Run(3f);
            Assert.That(Mathf.DeltaAngle(_camera.Yaw, 0f), Is.EqualTo(0f).Within(0.1f), "25 turns");
            Assert.That(_camera.Orbit.HorizontalAxis.Value, Is.InRange(-180f, 180f));
        }

        // roBrowser moves min(1, 0.012 × dt ms) of the rest per frame (0.006 for the followed position). Both are exponential;
        // Cinemachine's does not depend on the frame rate. After a second they agree; in between roBrowser's step runs ahead
        // by its own frame-rate error (peaks measured here, PLAN-0009 4.7).
        [TestCase(30f, 0.092f)]
        [TestCase(60f, 0.042f)]
        [TestCase(144f, 0.018f)]
        public void RoCamera_DampingMatchesRoBrowser_At30_60_144Fps(float fps, float peak)
        {
            Compare(_settings.TurnDampTime, 0.012f, fps, peak);
            Compare(_settings.FollowDampTime, 0.006f, fps, peak);
        }

        static void Compare(float dampTime, float rate, float fps, float peak)
        {
            float ro = 1f, cinemachine = 1f, dt = 1f / fps, worst = 0f;
            for (float t = 0f; t < 1f; t += dt)
            {
                ro -= ro * Mathf.Min(1f, rate * dt * 1000f);
                cinemachine -= Damper.Damp(cinemachine, dampTime, dt);
                worst = Mathf.Max(worst, Mathf.Abs(ro - cinemachine));
            }
            Assert.That(Mathf.Abs(ro - cinemachine), Is.LessThan(0.02f), $"after 1 s at {fps} fps");
            Assert.That(worst, Is.LessThan(peak), $"on the way at {fps} fps");
        }

        [Test]
        public void RoCamera_TiltOrbitsVertically_CameraHeightGrowsWithTilt_WithinLimits()
        {
            _camera.Target = new Vector3(10f, 2f, 20f);
            _camera.SnapToTarget();
            float last = 0f;
            for (float tilt = 45f; tilt <= 70f; tilt += 5f)
            {
                _camera.ResetTilt();
                _camera.Tilt(tilt - _settings.DefaultPitch);
                Run(2f);
                var s = State();
                var position = s.GetFinalPosition();
                float expected = Mathf.Clamp(tilt, 50f, 65f);
                Assert.That(Vector3.Distance(position, _camera.Target), Is.EqualTo(40f).Within(0.05f), "on the sphere");
                Assert.That(position.y - 2f, Is.EqualTo(40f * Mathf.Sin(expected * Mathf.Deg2Rad)).Within(0.05f));
                Assert.That(position.y, Is.GreaterThanOrEqualTo(last - 0.05f), "higher with more tilt");
                last = position.y;
                Assert.That(Vector3.Angle(s.GetFinalOrientation() * Vector3.forward, _camera.Target - position), Is.LessThan(0.05f), "the target is in the centre");
                Assert.That(s.GetFinalOrientation().eulerAngles.x, Is.EqualTo(expected).Within(0.05f));
            }
        }

        [Test]
        public void RoCamera_YawIsTheCameraTurn_AndTheLensIsTheSettings()
        {
            foreach (float yaw in new[] { 0f, 22f, 23f, 90f, 180f, 270f })
            {
                _camera.ResetNow();
                _camera.Turn(yaw);
                Run(3f);
                var o = State().GetFinalOrientation();
                Assert.That(Mathf.DeltaAngle(UnitSystem.YawOf(o), yaw), Is.EqualTo(0f).Within(0.05f));
            }
            Assert.That(_camera.Camera.Lens.FieldOfView, Is.EqualTo(15f));
            Assert.That(_camera.Camera.Lens.NearClipPlane, Is.EqualTo(1f));
            Assert.That(_camera.Camera.Lens.FarClipPlane, Is.EqualTo(150f));
            Assert.That(_camera.Camera.Lens.Orthographic, Is.False);
        }

        [Test]
        public void RoCamera_FollowsItsTarget_WithoutTurning()
        {
            var unit = new GameObject("unit");
            _camera.Follow = unit.transform;
            _camera.SnapToTarget();
            var before = State().GetFinalOrientation();
            for (int i = 0; i < 120; i++)
            {
                unit.transform.position += new Vector3(0.1f, 0f, 0.05f);
                _camera.Step(1f / 60f);
                Assert.That(Quaternion.Angle(State().GetFinalOrientation(), before), Is.LessThan(0.05f), "the view does not swing while following");
            }
            Assert.That(Vector3.Distance(_camera.CameraTarget.position, unit.transform.position), Is.GreaterThan(0.1f), "eased");
            Run(3f);
            Assert.That(Vector3.Distance(_camera.CameraTarget.position, unit.transform.position), Is.LessThan(0.01f));
            Object.DestroyImmediate(unit);
        }

        // T6.5 (D13): the state after a snap, before any easing.
        [Test]
        public void RoCamera_SnapToTarget_IsThereAtOnce()
        {
            _camera.Turn(77f);
            _camera.Tilt(8f);
            _camera.Zoom(-9f);
            Run(3f);
            var (yaw, pitch, distance) = (_camera.Yaw, _camera.Pitch, _camera.Distance);
            var offset = State().GetFinalPosition() - _camera.CameraTarget.position;
            _camera.Target += new Vector3(150f, 5f, 130f);
            _camera.SnapToTarget();
            _camera.Step(1f / 60f);
            var s = State();
            Assert.That(Vector3.Distance(s.GetFinalPosition() - _camera.Target, offset), Is.LessThan(0.05f));
            Assert.That((_camera.Yaw, _camera.Pitch, _camera.Distance), Is.EqualTo((yaw, pitch, distance)));
        }

        [Test]
        public void RoCamera_Step_AllocatesNothing()
        {
            _camera.Step(0.01f);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 60; i++)
            {
                _camera.Turn(1f);
                _camera.Step(0.01f);
            }
            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
        }
    }
}
