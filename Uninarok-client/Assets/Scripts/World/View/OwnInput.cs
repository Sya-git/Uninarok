using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Uninarok.Map;
using Uninarok.Units;

namespace Uninarok.World.View
{
    /// <summary>
    /// The player's mouse and keys for the own character (SPEC-0010 FR-9, FR-11, FR-13, FR-19; PLAN-0010 4.11) from the
    /// <c>World</c> action map: the left button walks, held it keeps asking, with shift it turns; Insert sits and stands. It only
    /// reads input, picks the cell and calls <see cref="OwnCharacter"/>. The camera has the right button and the wheel.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OwnInput : MonoBehaviour
    {
        public const string MapName = "World";

        public WorldBootstrap World;
        public CellCursor Cursor;
        public MouseCursor Mouse;
        /// <summary><c>WorldInputActions</c>.</summary>
        public InputActionAsset Actions;

        InputActionMap _actions;
        InputAction _walk, _turnModifier, _sit, _position;
        CellPicker _picker;

        bool _holding;
        double _askedAt;
        int _askedX, _askedY;

        /// <summary>Off while something else has the keyboard (a text field): nothing is sent and no cell is marked.</summary>
        public bool InputEnabled { get; set; } = true;

        /// <summary>When it returns true, the mouse belongs to something else (a panel under the pointer).</summary>
        public Func<Vector2, bool> Blocked { get; set; }

        internal Func<OwnCharacter> Own { get; set; }

        void OnEnable()
        {
            Bind();
            if (World == null) return;
            World.MapChanged += OnMapChanged;
            OnMapChanged(World.Map);
        }

        void OnDisable()
        {
            if (World != null) World.MapChanged -= OnMapChanged;
            OnMapChanged(null);
            Unbind();
        }

        internal void Bind()
        {
            Own ??= () => World != null && World.Session != null ? World.Session.Own : null;
            _actions = Actions != null ? Actions.FindActionMap(MapName) : null;
            if (_actions == null) return;
            _walk = _actions.FindAction("Walk", true);
            _turnModifier = _actions.FindAction("TurnModifier", true);
            _sit = _actions.FindAction("Sit", true);
            _position = _actions.FindAction("Position", true);
            _actions.Enable();
        }

        internal void Unbind()
        {
            _actions?.Disable();
            _actions = null;
        }

        void OnMapChanged(MapData map)
        {
            _picker = map != null ? new CellPicker(map) : null;
            if (Cursor != null) Cursor.Map = map;
            _holding = false;
        }

        void Update()
        {
            bool over = TryHover(out int x, out int y);
            Step(Time.unscaledTimeAsDouble, over, x, y);
        }

        bool TryHover(out int x, out int y)
        {
            x = y = -1;
            if (_actions == null || _picker == null || !InputEnabled) return false;
            var camera = UnitSystem.Camera != null ? UnitSystem.Camera : Camera.main;
            if (camera == null) return false;
            var position = _position.ReadValue<Vector2>();
            if (!camera.pixelRect.Contains(position) || (Blocked != null && Blocked(position))) return false;
            return _picker.TryPick(camera.ScreenPointToRay(position), out x, out y);
        }

        /// <summary>One frame: the cell under the mouse (if any) and the actions.</summary>
        internal void Step(double now, bool over, int x, int y)
        {
            var own = Own?.Invoke();
            if (_actions == null || own == null || !InputEnabled)
            {
                _holding = false;
                Mark(false, false, x, y);
                return;
            }

            if (_sit.WasPressedThisFrame()) own.ToggleSit();

            if (_walk.WasPressedThisFrame())
            {
                _holding = false;
                if (over && _turnModifier.IsPressed()) own.TurnTowards(x, y);
                else if (over)
                {
                    _holding = true;
                    Ask(own, now, x, y);
                }
            }
            else if (!_walk.IsPressed()) _holding = false;
            else if (_holding && over && (x != _askedX || y != _askedY || (now - _askedAt) * 1000.0 >= RepeatMs) && !own.IsWalkingTo(x, y)) Ask(own, now, x, y);

            if (over && own.TryGet(out var model, out _)) Mark(true, model.Grid.IsPassable(x, y), x, y);
            else Mark(false, false, x, y);
        }

        int RepeatMs => World != null && World.Settings != null ? World.Settings.HoldRepeatMs : 500;

        void Ask(OwnCharacter own, double now, int x, int y)
        {
            _askedAt = now;
            _askedX = x;
            _askedY = y;
            own.RequestWalk(x, y);
        }

        // The cell when it is walkable, the forbidden cursor when it is not (FR-11); nothing when the mouse is over no cell.
        void Mark(bool over, bool walkable, int x, int y)
        {
            if (Cursor != null)
            {
                if (over && walkable) Cursor.Show(x, y);
                else Cursor.Hide();
            }
            if (Mouse != null) Mouse.Kind = over && !walkable ? MouseCursorKind.NoWalk : MouseCursorKind.Default;
        }
    }
}
