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
    /// SPEC-0011 FR-2, FR-8, FR-10 (PLAN-0011 4.10): a press on a monster attacks it (continuous with ctrl, <c>/noctrl</c>, or
    /// when held for <c>HoldAttackMs</c>), a press on an item picks it up; over a player, an NPC or an item the ground rules
    /// stay; while the own character is dead nothing is sent.
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
        InputAction _walk, _turnModifier, _sit, _position, _attackModifier;
        CellPicker _picker;
        readonly UnitPicker _units = new UnitPicker();

        // A press that began on a monster: it never walks; held long enough it attacks without end.
        bool _onMonster;
        uint _pressedTarget;
        double _pressedAt;

        bool _holding;
        double _askedAt;
        int _askedX, _askedY;

        /// <summary>Off while something else has the keyboard (a text field): nothing is sent and no cell is marked.</summary>
        public bool InputEnabled { get; set; } = true;

        /// <summary>When it returns true, the mouse belongs to something else (a panel under the pointer).</summary>
        public Func<Vector2, bool> Blocked { get; set; }

        internal Func<OwnCharacter> Own { get; set; }
        internal Func<OwnCombat> Combat { get; set; }

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
            Combat ??= () => World != null && World.Session != null ? World.Session.Combat : null;
            _actions = Actions != null ? Actions.FindActionMap(MapName) : null;
            if (_actions == null) return;
            _walk = _actions.FindAction("Walk", true);
            _turnModifier = _actions.FindAction("TurnModifier", true);
            _sit = _actions.FindAction("Sit", true);
            _position = _actions.FindAction("Position", true);
            _attackModifier = _actions.FindAction("AttackModifier", true);
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
            _onMonster = false;
        }

        void Update()
        {
            bool over = TryHover(out int x, out int y, out uint unit);
            Step(Time.unscaledTimeAsDouble, over, x, y, unit);
        }

        bool TryHover(out int x, out int y, out uint unit)
        {
            x = y = -1;
            unit = 0;
            if (_actions == null || _picker == null || !InputEnabled) return false;
            var camera = UnitSystem.Camera != null ? UnitSystem.Camera : Camera.main;
            if (camera == null) return false;
            var position = _position.ReadValue<Vector2>();
            if (!camera.pixelRect.Contains(position) || (Blocked != null && Blocked(position))) return false;
            if (World != null && World.Session != null)
            {
                if (World.Combat != null) _units.MinPx = World.Combat.PickMinPx;
                _units.TryPick(World.Presenter, World.Session.Model, camera, position, out unit);
            }
            return _picker.TryPick(camera.ScreenPointToRay(position), out x, out y);
        }

        /// <summary>
        /// One frame: the cell under the mouse (if any), the unit or item under it (0: none; PLAN-0011 4.10) and the actions.
        /// </summary>
        internal void Step(double now, bool over, int x, int y, uint unit = 0)
        {
            var own = Own?.Invoke();
            if (_actions == null || own == null || !InputEnabled || !own.TryGet(out var model, out _))
            {
                _holding = _onMonster = false;
                Mark(false, false, x, y, false);
                return;
            }
            // FR-8: while dead nothing is sent; the camera and the dev tools keep working.
            if (!own.CanAct)
            {
                _holding = _onMonster = false;
                Mark(false, false, x, y, false);
                return;
            }

            bool monster = false, item = false;
            if (unit != 0 && model.TryGetSlot(unit, out int slot))
            {
                monster = model[slot].Kind == EntityKind.Monster;
                item = model[slot].IsItem;
            }
            var combat = Combat?.Invoke();

            if (_sit.WasPressedThisFrame()) own.ToggleSit();

            if (_walk.WasPressedThisFrame())
            {
                _holding = _onMonster = false;
                if (over && _turnModifier.IsPressed()) own.TurnTowards(x, y);
                else if (monster && combat != null)
                {
                    _onMonster = true;
                    _pressedTarget = unit;
                    _pressedAt = now;
                    combat.Attack(unit, _attackModifier.IsPressed());
                }
                else if (item && combat != null) combat.PickUp(unit);
                else if (over)
                {
                    _holding = true;
                    Ask(own, now, x, y);
                }
            }
            else if (!_walk.IsPressed()) _holding = _onMonster = false;
            else if (_onMonster)
            {
                // Held on a monster: after HoldAttackMs the attack goes on without end. Letting go does not stop it (T0.3). In range
                // the single blow often came back already and ended the single attack: that one too (D38); another action did not.
                if (combat != null && (now - _pressedAt) * 1000.0 >= HoldAttackMs)
                {
                    _onMonster = false;
                    bool ours = combat.TargetId == _pressedTarget || combat.SingleDoneTarget == _pressedTarget;
                    if (ours && !combat.Continuous) combat.Attack(_pressedTarget, true);
                }
            }
            else if (_holding && over && (x != _askedX || y != _askedY || (now - _askedAt) * 1000.0 >= RepeatMs) && !own.IsWalkingTo(x, y)) Ask(own, now, x, y);

            if (over) Mark(true, model.Grid.IsPassable(x, y), x, y, monster);
            else Mark(false, false, x, y, monster);
        }

        int RepeatMs => World != null && World.Settings != null ? World.Settings.HoldRepeatMs : 500;
        int HoldAttackMs => World != null && World.Combat != null ? World.Combat.HoldAttackMs : 300;

        void Ask(OwnCharacter own, double now, int x, int y)
        {
            _askedAt = now;
            _askedX = x;
            _askedY = y;
            own.RequestWalk(x, y);
        }

        // The cell when it is walkable, the forbidden cursor when it is not (FR-11); nothing when the mouse is over no cell.
        // Over a monster: the attack cursor and no cell (SPEC-0011 FR-2).
        void Mark(bool over, bool walkable, int x, int y, bool monster)
        {
            if (Cursor != null)
            {
                if (over && walkable && !monster) Cursor.Show(x, y);
                else Cursor.Hide();
            }
            if (Mouse != null)
                Mouse.Kind = monster ? MouseCursorKind.Attack : over && !walkable ? MouseCursorKind.NoWalk : MouseCursorKind.Default;
        }
    }
}
