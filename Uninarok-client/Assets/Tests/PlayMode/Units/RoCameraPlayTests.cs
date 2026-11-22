using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Uninarok.CameraRig;

namespace Uninarok.Units.PlayTests
{
    // TASKS-0009 T6.3, T6.5 (FR-6, FR-12; PLAN-0009 4.7, §5, D13): the real brain, once per frame.
    public class RoCameraPlayTests
    {
        const string PrefabPath = "Assets/Scripts/CameraRig/RoCameraRig.prefab";

        RoCamera _camera;
        UnitView _unit;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null, PrefabPath);
            _camera = Object.Instantiate(prefab).GetComponent<RoCamera>();
            _unit = new GameObject("unit").AddComponent<UnitView>(); // starts the unit system; it shows nothing
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(_camera.gameObject);
            Object.Destroy(_unit.gameObject);
            yield return null;
            Assert.That(UnitSystem.Camera == null, Is.True, "the rig takes its camera back");
        }

        [UnityTest]
        public IEnumerator UnitSystem_ReadsTheSectorOfTheRenderedCamera()
        {
            Assert.That(UnitSystem.Camera, Is.SameAs(_camera.OutputCamera));
            int seen = 0, last = -1;
            for (int i = 0; i < 240; i++)
            {
                _camera.Turn(2.5f);
                yield return null;
                // Nothing moves the camera between a frame's LateUpdate and here: this is the rotation it rendered with.
                int rendered = UnitFacing.CameraSector(UnitSystem.YawOf(_camera.OutputCamera.transform.rotation));
                Assert.That(UnitSystem.Sector, Is.EqualTo(rendered), $"frame {i}");
                Assert.That(_camera.Sector, Is.EqualTo(rendered));
                if (rendered != last) seen++;
                last = rendered;
            }
            Assert.That(seen, Is.GreaterThan(4), "the turn crossed several sectors");
        }

        [UnityTest]
        public IEnumerator RoCamera_SnapToTarget_FirstFrameAtTarget_KeepsTurnTiltZoom()
        {
            _camera.Turn(130f);
            _camera.Tilt(9f);
            _camera.Zoom(-10f);
            for (int i = 0; i < 400 && Mathf.Abs(_camera.WantedYaw - 130f) + Mathf.Abs(Mathf.DeltaAngle(_camera.Yaw, 130f)) > 0.02f; i++) yield return null;
            for (int i = 0; i < 60; i++) yield return null;

            var output = _camera.OutputCamera;
            var rotation = output.transform.rotation;
            float distance = Vector3.Distance(output.transform.position, _camera.Target);
            _camera.Target += new Vector3(140f, 6f, -140f); // about 200 units
            _camera.SnapToTarget();
            yield return null;
            var view = output.WorldToViewportPoint(_camera.Target);
            Assert.That(view.x, Is.EqualTo(0.5f).Within(0.002f));
            Assert.That(view.y, Is.EqualTo(0.5f).Within(0.002f));
            Assert.That(view.z, Is.EqualTo(distance).Within(0.1f), "zoom kept");
            Assert.That(Quaternion.Angle(output.transform.rotation, rotation), Is.LessThan(0.1f), "turn and tilt kept");
        }

        [UnityTest]
        public IEnumerator RoCamera_MovingTheTarget_MovesTheCameraWithIt()
        {
            var before = _camera.OutputCamera.transform.position;
            _camera.Target += new Vector3(5f, 0f, 3f);
            yield return null;
            yield return null;
            Assert.That(Vector3.Distance(_camera.OutputCamera.transform.position, before), Is.LessThan(5.8f), "eases, does not jump");
            float end = Time.time + 2f;
            while (Time.time < end) yield return null;
            Assert.That(Vector3.Distance(_camera.OutputCamera.transform.position - before, new Vector3(5f, 0f, 3f)), Is.LessThan(0.05f));
        }
    }
}
