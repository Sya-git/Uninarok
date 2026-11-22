using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Uninarok.CameraRig.Tests
{
    // TASKS-0009 T6.2 (FR-12; PLAN-0009 4.7): every control through simulated devices.
    public class RoCameraInputTests : InputTestFixture
    {
        static readonly Vector2 Screen = new Vector2(1000f, 500f);

        RoCameraSettings _settings;
        RoCamera _camera;
        RoCameraInput _input;
        Mouse _mouse;
        Keyboard _keyboard;
        double _now;

        public override void Setup()
        {
            base.Setup();
            _mouse = InputSystem.AddDevice<Mouse>();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _settings = ScriptableObject.CreateInstance<RoCameraSettings>();
            _settings.DefaultPitch = 55f;
            _settings.DefaultDistance = 30f;
            _camera = RoCameraRig.Create(_settings);
            _camera.ResetNow();
            _input = _camera.gameObject.AddComponent<RoCameraInput>();
            _input.Actions = Object.Instantiate(AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions"));
            _input.Bind(); // OnEnable does not run in edit mode for a plain MonoBehaviour
            _now = 100.0;
        }

        public override void TearDown()
        {
            var actions = _input.Actions;
            _input.Unbind();
            Object.DestroyImmediate(_camera.gameObject);
            Object.DestroyImmediate(actions);
            Object.DestroyImmediate(_settings);
            base.TearDown();
        }

        void Frame(double seconds = 0.016)
        {
            _now += seconds;
            _input.Step(_now, Screen);
        }

        void PressRight(Vector2 at)
        {
            Set(_mouse.position, at);
            Press(_mouse.rightButton);
            Frame();
        }

        void Click(Vector2 at, double after = 0.1)
        {
            _now += after;
            PressRight(at);
            Release(_mouse.rightButton);
            Frame();
        }

        void Drag(Vector2 by)
        {
            // Position and delta move together, as a real mouse reports them.
            var state = new MouseState { position = _mouse.position.ReadValue() + by, delta = by }.WithButton(MouseButton.Right);
            InputSystem.QueueStateEvent(_mouse, state);
            InputSystem.Update();
            Frame();
        }

        [Test]
        public void RightDrag_Turns()
        {
            PressRight(new Vector2(500, 250));
            Drag(new Vector2(250, 40));
            Assert.That(_camera.WantedYaw, Is.EqualTo(180f).Within(0.01f), "720° per screen width");
            Assert.That(_camera.WantedPitch, Is.EqualTo(55f), "no tilt");
            Drag(new Vector2(-125, 0));
            Assert.That(_camera.WantedYaw, Is.EqualTo(90f).Within(0.01f));
        }

        [Test]
        public void NoDrag_NoTurn()
        {
            Set(_mouse.position, new Vector2(500, 250));
            Set(_mouse.delta, new Vector2(100, 0));
            Frame();
            Assert.That(_camera.WantedYaw, Is.EqualTo(0f));
        }

        [Test]
        public void ShiftRightDrag_Tilts()
        {
            Press(_keyboard.leftShiftKey);
            PressRight(new Vector2(500, 250));
            Drag(new Vector2(30, -10));
            Assert.That(_camera.WantedPitch, Is.EqualTo(61f).Within(0.01f), "300° per screen height, down tilts down");
            Assert.That(_camera.WantedYaw, Is.EqualTo(0f));
        }

        [Test]
        public void Wheel_Zooms_OneNotchIsOneStep()
        {
            Set(_mouse.scroll, new Vector2(0f, 1f));
            Frame();
            Assert.That(_camera.WantedDistance, Is.EqualTo(30f - _settings.WheelStep).Within(0.01f), "up moves nearer");
            Set(_mouse.scroll, new Vector2(0f, -240f));
            Frame();
            Assert.That(_camera.WantedDistance, Is.EqualTo(30f + _settings.WheelStep).Within(0.01f), "two notches of 120");
        }

        [Test]
        public void CtrlRightDrag_Zooms()
        {
            Press(_keyboard.leftCtrlKey);
            PressRight(new Vector2(500, 250));
            Drag(new Vector2(0, -50));
            Assert.That(_camera.WantedDistance, Is.EqualTo(30f + 0.1f * _settings.CtrlDragZoomPerScreenHeight).Within(0.01f));
            Assert.That(_camera.WantedYaw, Is.EqualTo(0f));
        }

        [Test]
        public void RoCamera_DoubleClickVariants()
        {
            void Move()
            {
                _camera.ResetNow();
                _camera.Turn(100f);
                _camera.Tilt(8f);
                _camera.Zoom(-5f);
            }

            Move();
            Click(new Vector2(300, 200), 5);
            Assert.That(_camera.WantedYaw, Is.EqualTo(100f), "one click does nothing");
            Click(new Vector2(304, 200));
            Assert.That(_camera.WantedYaw, Is.EqualTo(0f).Within(0.01f), "double click: turn");
            Assert.That((_camera.WantedPitch, _camera.WantedDistance), Is.EqualTo((63f, 25f)));

            Move();
            Press(_keyboard.leftShiftKey);
            Click(new Vector2(300, 200), 5);
            Click(new Vector2(300, 200));
            Assert.That(_camera.WantedPitch, Is.EqualTo(55f), "shift: tilt");
            Assert.That((_camera.WantedYaw, _camera.WantedDistance), Is.EqualTo((100f, 25f)));
            Release(_keyboard.leftShiftKey);

            Move();
            Press(_keyboard.leftCtrlKey);
            Click(new Vector2(300, 200), 5);
            Click(new Vector2(300, 200));
            Assert.That(_camera.WantedDistance, Is.EqualTo(30f), "ctrl: distance");
            Assert.That((_camera.WantedYaw, _camera.WantedPitch), Is.EqualTo((100f, 63f)));
            Release(_keyboard.leftCtrlKey);

            Move();
            Click(new Vector2(300, 200), 5);
            Click(new Vector2(300, 200), 0.6);
            Assert.That(_camera.WantedYaw, Is.EqualTo(100f), "too slow");
            Click(new Vector2(330, 200));
            Assert.That(_camera.WantedYaw, Is.EqualTo(100f), "too far apart");
        }

        [Test]
        public void ABlockedPointer_NeitherTurnsNorZooms()
        {
            _input.Blocked = p => p.x < 200f;
            PressRight(new Vector2(100, 250));
            Drag(new Vector2(50, 0));
            Assert.That(_camera.WantedYaw, Is.EqualTo(0f));
            Set(_mouse.scroll, new Vector2(0f, 1f));
            Frame();
            Assert.That(_camera.WantedDistance, Is.EqualTo(30f));
        }
    }
}
