using UnityEngine;
using UnityEngine.InputSystem;

namespace Uninarok.CameraRig
{
    /// <summary>
    /// RO's camera controls (PLAN-0009 4.7, OQ-1; SPEC-0009 FR-12) from the <c>Camera</c> action map: right-drag turns,
    /// shift + right-drag tilts, the wheel and ctrl + right-drag zoom, a double right-click resets the turn (with shift the
    /// tilt, with ctrl the distance). It only reads input and calls <see cref="RoCamera"/>.
    /// </summary>
    [RequireComponent(typeof(RoCamera))]
    [DisallowMultipleComponent]
    public sealed class RoCameraInput : MonoBehaviour
    {
        public const string MapName = "Camera";

        /// <summary><c>InputSystem_Actions</c>.</summary>
        public InputActionAsset Actions;

        RoCamera _camera;
        InputActionMap _map;
        InputAction _drag, _delta, _position, _zoom, _tiltModifier, _zoomModifier;
        double _lastPress = double.NegativeInfinity;
        Vector2 _lastPressAt;
        bool _blockedDrag;

        /// <summary>When it returns true, presses and the wheel belong to something else (a panel under the pointer); a drag already going on goes on.</summary>
        public System.Func<Vector2, bool> Blocked { get; set; }

        void OnEnable() => Bind();

        void OnDisable() => Unbind();

        internal void Bind()
        {
            _camera = GetComponent<RoCamera>();
            _map = Actions != null ? Actions.FindActionMap(MapName) : null;
            if (_map == null) return;
            _drag = _map.FindAction("Drag", true);
            _delta = _map.FindAction("Delta", true);
            _position = _map.FindAction("Position", true);
            _zoom = _map.FindAction("Zoom", true);
            _tiltModifier = _map.FindAction("TiltModifier", true);
            _zoomModifier = _map.FindAction("ZoomModifier", true);
            _map.Enable();
        }

        internal void Unbind()
        {
            _map?.Disable();
            _map = null;
        }

        void Update() => Step(Time.unscaledTimeAsDouble, new Vector2(Screen.width, Screen.height));

        internal void Step(double now, Vector2 screen)
        {
            if (_map == null || _camera == null || _camera.Settings == null || screen.x <= 0f || screen.y <= 0f) return;
            var s = _camera.Settings;
            var position = _position.ReadValue<Vector2>();
            bool blocked = Blocked != null && Blocked(position);
            bool tilt = _tiltModifier.IsPressed(), zoom = _zoomModifier.IsPressed();

            if (_drag.WasPressedThisFrame())
            {
                _blockedDrag = blocked;
                if (!blocked)
                {
                    if (now - _lastPress < s.DoubleClickSeconds && (position - _lastPressAt).magnitude < s.DoubleClickPixels)
                    {
                        if (tilt) _camera.ResetTilt();
                        else if (zoom) _camera.ResetZoom();
                        else _camera.ResetTurn();
                        _lastPress = double.NegativeInfinity; // a third press starts over
                    }
                    else
                    {
                        _lastPress = now;
                        _lastPressAt = position;
                    }
                }
            }
            else if (_drag.IsPressed() && !_blockedDrag)
            {
                var delta = _delta.ReadValue<Vector2>();
                if (tilt) _camera.Tilt(-delta.y / screen.y * s.PitchDegreesPerScreenHeight);
                else if (zoom) _camera.Zoom(-delta.y / screen.y * s.CtrlDragZoomPerScreenHeight);
                else _camera.Turn(delta.x / screen.x * s.TurnDegreesPerScreenWidth);
            }

            float wheel = _zoom.ReadValue<float>();
            if (wheel != 0f && !blocked) _camera.Zoom(-Notches(wheel) * s.WheelStep);
        }

        // One notch is 120 on Windows drivers and 1 where the Input System normalises it.
        static float Notches(float wheel) => Mathf.Abs(wheel) >= 20f ? wheel / 120f : wheel;
    }
}
