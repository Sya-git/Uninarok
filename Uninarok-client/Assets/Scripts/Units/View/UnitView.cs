using System;
using UnityEngine;
using UnityEngine.Rendering;
using Uninarok.Map;
using Uninarok.Networking.Data;
using Uninarok.Sprites;

namespace Uninarok.Units
{
    /// <summary>
    /// One unit in the scene (PLAN-0009 4.6; SPEC-0009 FR-5 to FR-11): its look, its facing and its animation, shown by one
    /// driven <see cref="SpriteActor"/> per part under a pivot that faces the camera fully. It has no <c>Update</c>:
    /// <see cref="UnitSystem"/> steps every enabled view in one loop (tools step one alone). Parts are composed and written
    /// only when something they depend on changed; nothing is allocated per frame.
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class UnitView : MonoBehaviour
    {
        /// <summary>Sorting orders per part: room for its layers.</summary>
        const int OrdersPerPart = 64;
        /// <summary>Step toward the camera between parts, so the order also holds where sorting orders do not decide.</summary>
        const float PartDepthStep = 0.002f;

        /// <summary>The material of every part (<c>sprite_unit.mat</c>, depth bias); null keeps Unity's default.</summary>
        public Material Material;

        UnitLoader _loader;
        Transform _pivot;
        UnitShadow _shadow;
        readonly SpriteActor[] _actors = new SpriteActor[UnitParts.SlotCount];
        readonly Texture2D[] _palettes = new Texture2D[UnitParts.SlotCount];
        readonly UnitParts _parts = new UnitParts();
        readonly PartPlacement[] _placements = new PartPlacement[UnitComposer.MaxParts];
        readonly int[] _partFrames = new int[UnitParts.SlotCount];
        int _placementCount;
        UnitLook _look;
        bool _hasLook;
        LoadReport _report;

        UnitClock _clock;
        // What was asked for, in the family it was asked in; the number played is mapped to the look's family.
        bool _askedPlayer = true;
        int _asked;

        // What the renderers show.
        int _shownVersion = -1, _shownAnimation = -1, _shownDirection = -1, _shownFrame = -1, _shownTicks = -1;
        HeadFacing _shownHead;
        bool _animatedIdle;
        bool _turnPending = true;
        int _hiddenSlots, _shownHidden;

        internal int SystemIndex = -1;
        /// <summary>How many times the parts were composed (tests).</summary>
        internal int ComposeCount { get; private set; }
        internal UnitParts Parts => _parts;
        internal Transform Pivot => _pivot;
        internal UnitShadow Shadow => _shadow;

        public UnitLook Look => _look;
        /// <summary>World facing (network direction); it can change every frame.</summary>
        public UninarokDirection Facing { get; set; } = UninarokDirection.South;
        /// <summary>The head relative to the body; shown while idle or sitting (FR-8).</summary>
        public HeadFacing HeadFacing { get; set; }
        /// <summary>Multiplies the body's frame time (walk and attack speed, later specs).</summary>
        public float Speed
        {
            get => _clock.Speed > 0f ? _clock.Speed : 1f;
            set => _clock.Speed = value;
        }
        /// <summary>Show the unit's shadow when it has one.</summary>
        public bool ShadowEnabled { get; set; } = true;
        /// <summary>Apply the riding edits of the composition tables (PLAN-0009 D17).</summary>
        public bool RidingEdits
        {
            get => _ridingEdits;
            set
            {
                if (value == _ridingEdits) return;
                _ridingEdits = value;
                _shownVersion = -1; // composed again on the next step
            }
        }
        bool _ridingEdits;

        /// <summary>The animation number played, in the look's family.</summary>
        public int AnimationNumber => _clock.Animation;
        public PlayMode PlayMode => _clock.Mode;
        /// <summary>The body's frame shown, −1 when nothing is.</summary>
        public int Frame => _shownFrame;
        /// <summary>The action direction shown, −1 when nothing is.</summary>
        public int ShownDirection => _shownDirection;
        /// <summary>True when the asked look is loaded and shown.</summary>
        public bool Loaded => _hasLook && _shownVersion == _parts.Version && _parts.Version > 0 && _parts.Look == _look;
        /// <summary>How the parts of the last applied look were found; null before.</summary>
        public LoadReport Report => _report;

        /// <summary>A <see cref="PlayMode.OnceEnd"/> animation showed its last frame for its full time.</summary>
        public event Action<UnitView> AnimationEnded;
        /// <summary>The body entered a frame with an event: <c>atk</c> or a sound name.</summary>
        public event Action<UnitView, string> FrameEvent;

        /// <summary>A new unit under <paramref name="parent"/>; it shows nothing until <see cref="SetLook"/>.</summary>
        public static UnitView Create(UnitLoader loader, Material material, Transform parent = null, string name = "Unit")
        {
            var go = new GameObject(name);
            go.SetActive(false);
            if (parent != null) go.transform.SetParent(parent, false);
            var view = go.AddComponent<UnitView>();
            view.Material = material;
            view._loader = loader;
            view.EnsureRoot();
            go.SetActive(true);
            return view;
        }

        /// <summary>The loader of this unit; set before the first <see cref="SetLook"/> when the unit was not made by <see cref="Create"/>.</summary>
        public UnitLoader Loader
        {
            get => _loader;
            set => _loader = value;
        }

        void OnEnable() => UnitSystem.Register(this);

        void OnDisable() => UnitSystem.Unregister(this);

        void OnDestroy()
        {
            _loader?.Release(_parts);
            AnimationEnded = null;
            FrameEvent = null;
        }

        // ---- look ----

        /// <summary>
        /// Changes the look (FR-9): only the assets change. The animation, its frame, the facing and the position stay. Until the
        /// new parts are loaded the unit keeps the old ones. A missing part is one warning.
        /// </summary>
        public void SetLook(in UnitLook look)
        {
            if (_loader == null) throw new InvalidOperationException("the unit has no loader");
            if (_hasLook && look == _look) return;
            _look = look;
            _hasLook = true;
            _loader.Load(look, _parts, OnLoaded);
        }

        void OnLoaded(UnitParts parts, LoadReport report)
        {
            _report = report;
            if (Application.isPlaying)
                foreach (var line in report.Lines)
                    if (!line.Found) Debug.LogWarning($"[Units] {name}: {line.Slot} left out: {line.Text}", this);
            EnsureRoot();
            for (int i = 0; i < UnitParts.SlotCount; i++)
            {
                var animation = parts.Animations[i];
                if (animation == null)
                {
                    if (_actors[i] != null)
                    {
                        _actors[i].Hide();
                        _actors[i].Data = null;
                    }
                    continue;
                }
                var actor = EnsureActor((PartSlot)i);
                actor.Data = animation;
                actor.Missing = i == (int)PartSlot.Body ? SpriteMissingAction.Fallback : SpriteMissingAction.DrawNothing;
                if (_palettes[i] == parts.Palettes[i] && actor.LayerRenderers.Count > 0) continue;
                _palettes[i] = parts.Palettes[i];
                SpritePalette.Apply(actor, parts.Palettes[i]);
            }
            _pivot.localScale = new Vector3(parts.Scale, parts.Scale, parts.Scale);
            _shadow.Apply(parts);
            // The same animation in the new look's family, without restarting.
            _clock.Animation = Mapped();
            _animatedIdle = false;
        }

        // ---- animation ----

        public void Play(PcAnimation animation, PlayMode mode = PlayMode.Loop) => Play(true, (int)animation, mode);

        public void Play(MonsterAnimation animation, PlayMode mode = PlayMode.Loop) => Play(false, (int)animation, mode);

        void Play(bool player, int animation, PlayMode mode)
        {
            _askedPlayer = player;
            _asked = animation;
            _clock.Play(Mapped(), mode);
        }

        bool IsPlayer => _hasLook && _look.Kind == UnitKind.Player;

        int Mapped()
        {
            bool player = _parts.Version > 0 ? _parts.Look.Kind == UnitKind.Player : IsPlayer;
            if (player == _askedPlayer) return _asked;
            return player ? (int)UnitAnimations.ToPlayer((MonsterAnimation)_asked) : (int)UnitAnimations.ToUnit((PcAnimation)_asked);
        }

        /// <summary>Moves the animation to a frame (tools: step and scrub).</summary>
        public void SetFrame(int frame) => _clock.SetFrame(frame);

        // ---- placement ----

        /// <summary>Puts the unit's feet on the ground at a position (FR-10); outside the map the position's own height is kept.</summary>
        public void PlaceOn(MapData map, Vector3 position)
        {
            if (map != null && map.TryGetHeight(position, out float y)) position.y = y;
            transform.position = position;
        }

        // ---- the step ----

        /// <summary>
        /// One step of this unit (PLAN-0009 4.6): <paramref name="rotationChanged"/> is true when the camera's rotation is not the one
        /// of the last step. The sector is the caller's, the same for every unit of a frame (FR-6).
        /// </summary>
        internal void Step(float deltaMs, int cameraSector, in Quaternion cameraRotation, float cameraYaw, bool rotationChanged)
        {
            if (rotationChanged || _turnPending)
            {
                _turnPending = false;
                if (_pivot != null)
                {
                    _pivot.rotation = cameraRotation;
                    _shadow.Turn(cameraYaw);
                }
            }

            var body = _parts.Animations[(int)PartSlot.Body];
            int direction = UnitFacing.Shown(Facing, cameraSector);
            if (body == null || !body.TryGetAction(_clock.Animation, direction, SpriteMissingAction.Fallback, out int unique))
            {
                if (_shownVersion != _parts.Version) ShowNothing();
                return;
            }

            var action = body.Actions[unique];
            bool headFrames = _parts.Look.Kind == UnitKind.Player && UnitAnimations.IsHeadFrame(_clock.Animation);
            int head = headFrames ? (int)HeadFacing : -1;
            int frame = _clock.Advance(deltaMs, action, head, out bool ended);

            if (_shownVersion != _parts.Version || frame != _shownFrame || direction != _shownDirection || _clock.Animation != _shownAnimation
                || HeadFacing != _shownHead || _hiddenSlots != _shownHidden || (_animatedIdle && headFrames && _clock.Ticks != _shownTicks))
                Compose(direction, frame, head);

            bool shadow = ShadowEnabled && _shadow.Scale > 0f && !HidesShadow();
            if (_shadow.Visible != shadow) _shadow.Visible = shadow;

            if (ended) AnimationEnded?.Invoke(this);
        }

        // Sitting and dead players show no shadow (composition-reference.md 2.9).
        bool HidesShadow() => _parts.Look.Kind == UnitKind.Player
            ? _clock.Animation == (int)PcAnimation.Sit || _clock.Animation == (int)PcAnimation.Dead
            : _clock.Animation == (int)MonsterAnimation.Die;

        void Compose(int direction, int frame, int head)
        {
            UnitSystem.ComposeMarker.Begin();
            ComposeCount++;
            int animation = _clock.Animation;
            var input = new ComposeInput
            {
                Player = _parts.Look.Kind == UnitKind.Player,
                Job = _parts.Look.JobId,
                Sex = _parts.Look.Sex,
                Race = _parts.Race,
                Riding = _parts.Riding,
                RidingEdits = RidingEdits,
                Animation = animation,
                Direction = direction,
                Frame = frame,
                Body = true,
                Garment = _parts.Has(PartSlot.Garment),
                GarmentId = _parts.Look.Garment,
                Weapon = _parts.Has(PartSlot.Weapon),
                WeaponTrail = _parts.Has(PartSlot.WeaponTrail),
                Shield = _parts.Has(PartSlot.Shield),
            };
            input.HasBodyAnchor = TryAnchor(PartSlot.Body, animation, direction, frame, out input.BodyAnchor);
            _partFrames[(int)PartSlot.Body] = frame;
            _animatedIdle = false;
            if (input.Player)
            {
                Attached(PartSlot.Head, 0, ref input.Head, animation, direction, frame, head);
                Attached(PartSlot.HeadBottom, _parts.Look.HeadBottom, ref input.HeadBottom, animation, direction, frame, head);
                Attached(PartSlot.HeadMid, _parts.Look.HeadMid, ref input.HeadMid, animation, direction, frame, head);
                Attached(PartSlot.HeadTop, _parts.Look.HeadTop, ref input.HeadTop, animation, direction, frame, head);
                PartFrame(PartSlot.Garment, animation, direction, frame, head);
                PartFrame(PartSlot.Weapon, animation, direction, frame, head);
                PartFrame(PartSlot.WeaponTrail, animation, direction, frame, head);
                PartFrame(PartSlot.Shield, animation, direction, frame, head);
            }

            _placementCount = UnitComposer.Compose(input, _loader?.Source.Tables, _placements);
            Write(animation, direction);

            _shownVersion = _parts.Version;
            _shownAnimation = animation;
            _shownDirection = direction;
            _shownFrame = frame;
            _shownHead = HeadFacing;
            _shownTicks = _clock.Ticks;
            _shownHidden = _hiddenSlots;
            UnitSystem.ComposeMarker.End();
        }

        // Every part is written in the same step: parts never switch direction or frame one before the others (FR-5, FR-6).
        void Write(int animation, int direction)
        {
            float ppu = _parts.Animations[(int)PartSlot.Body].PixelsPerUnit;
            int shown = 0;
            for (int i = 0; i < _placementCount; i++)
            {
                ref var p = ref _placements[i];
                int slot = (int)p.Slot;
                var actor = _actors[slot];
                if (actor == null) continue;
                shown |= 1 << slot;
                if ((_hiddenSlots & (1 << slot)) != 0)
                {
                    actor.Hide();
                    continue;
                }
                actor.transform.localPosition = new Vector3(p.OffsetPx.x / ppu, -p.OffsetPx.y / ppu, -p.Order * PartDepthStep);
                actor.SortingBase = p.Order * OrdersPerPart;
                actor.Tint = p.Tint;
                actor.ShowFrame(animation, direction, _partFrames[slot]);
            }
            for (int i = 0; i < _actors.Length; i++)
                if (_actors[i] != null && (shown & (1 << i)) == 0) _actors[i].Hide();
        }

        void Attached(PartSlot slot, int viewId, ref AttachedPart part, int animation, int direction, int frame, int head)
        {
            part.Present = _parts.Has(slot);
            if (!part.Present) return;
            part.ViewId = viewId;
            part.HasAnchor = TryAnchor(slot, animation, direction, PartFrame(slot, animation, direction, frame, head), out part.Anchor);
        }

        int PartFrame(PartSlot slot, int animation, int direction, int frame, int head)
        {
            var data = _parts.Animations[(int)slot];
            int shown = frame;
            if (data != null && data.TryGetAction(animation, direction, SpriteMissingAction.DrawNothing, out int unique))
            {
                int count = data.Actions[unique].FrameCount;
                shown = UnitClock.PartFrame(frame, count, head, _clock.Ticks);
                if (head >= 0 && count > 3) _animatedIdle = true;
            }
            _partFrames[(int)slot] = shown;
            return shown;
        }

        bool TryAnchor(PartSlot slot, int animation, int direction, int frame, out Vector2Int anchor)
        {
            anchor = default;
            var data = _parts.Animations[(int)slot];
            var policy = slot == PartSlot.Body ? SpriteMissingAction.Fallback : SpriteMissingAction.DrawNothing;
            if (data == null || !data.TryGetAction(animation, direction, policy, out int unique)) return false;
            var action = data.Actions[unique];
            if (action.FrameCount <= 0) return false;
            var f = data.Frames[action.FirstFrame + frame % action.FrameCount];
            if (f.AnchorCount <= 0) return false;
            anchor = data.Anchors[f.FirstAnchor];
            return true;
        }

        void ShowNothing()
        {
            _shownVersion = _parts.Version;
            _shownFrame = _shownDirection = _shownAnimation = -1;
            _placementCount = 0;
            for (int i = 0; i < _actors.Length; i++)
                if (_actors[i] != null) _actors[i].Hide();
            if (_shadow != null) _shadow.Visible = false;
        }

        // ---- tools ----

        /// <summary>The placements of the frame shown, back to front (the composer window).</summary>
        internal ReadOnlySpan<PartPlacement> Placements => new ReadOnlySpan<PartPlacement>(_placements, 0, _placementCount);

        internal SpriteActor ActorOf(PartSlot slot) => _actors[(int)slot];

        /// <summary>Hides a part without changing the look (the composer window).</summary>
        internal void SetPartHidden(PartSlot slot, bool hidden)
        {
            if (hidden) _hiddenSlots |= 1 << (int)slot;
            else _hiddenSlots &= ~(1 << (int)slot);
        }

        internal bool IsPartHidden(PartSlot slot) => (_hiddenSlots & (1 << (int)slot)) != 0;

        internal UnitClock Clock => _clock;

        // ---- objects ----

        void EnsureRoot()
        {
            if (_pivot != null) return;
            var pivot = new GameObject("Pivot") { layer = gameObject.layer, hideFlags = gameObject.hideFlags };
            _pivot = pivot.transform;
            _pivot.SetParent(transform, false);
            // The unit sorts as a whole against other units; inside it the order is the composer's.
            pivot.AddComponent<SortingGroup>();
            _shadow = new UnitShadow(transform, Material);
            _turnPending = true;
        }

        SpriteActor EnsureActor(PartSlot slot)
        {
            int i = (int)slot;
            if (_actors[i] != null) return _actors[i];
            var go = new GameObject(slot.ToString()) { layer = gameObject.layer, hideFlags = gameObject.hideFlags };
            go.SetActive(false);
            go.transform.SetParent(_pivot, false);
            var actor = go.AddComponent<SpriteActor>();
            actor.Driven = true;
            actor.Billboard = false;
            actor.Material = Material;
            go.SetActive(true);
            if (slot == PartSlot.Body) actor.FrameEvent += OnBodyEvent;
            return _actors[i] = actor;
        }

        void OnBodyEvent(SpriteActor _, string name) => FrameEvent?.Invoke(this, name);
    }
}
