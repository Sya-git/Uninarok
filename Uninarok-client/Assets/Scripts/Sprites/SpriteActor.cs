using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Uninarok.Sprites
{
    /// <summary>
    /// Plays one <see cref="SpriteAnimation"/> (PLAN-0007 4.6, SPEC-0007 FR-3, FR-5, FR-6). It owns one child
    /// <see cref="SpriteRenderer"/> per layer slot (<c>Layer_0…</c>) and has no <c>Update</c>: <see cref="SpriteSystem"/>
    /// advances every enabled actor in one loop and the actor writes its renderers only when its frame changes.
    /// Layer rules (plan 4.3): offset is the layer centre in pixels with Y down (Y is flipped here), mirror flips the image
    /// about its centre, scale applies before rotation, rotation is clockwise on screen in degrees, tint multiplies, later
    /// layers draw on top, and every action loops.
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class SpriteActor : MonoBehaviour
    {
        /// <summary>Depth step between layer slots, towards the camera, so later layers stay on top without sorting by depth.</summary>
        public const float LayerDepthStep = 0.0005f;

        public SpriteAnimation Data;
        /// <summary>Shared material of the layer renderers (<c>sprite_default.mat</c>); null keeps Unity's default sprite material.</summary>
        public Material Material;
        public SpriteMissingAction Missing = SpriteMissingAction.DrawNothing;
        /// <summary>Multiplies the frame time: 2 plays at half speed (walk speed, attack speed).</summary>
        public float Speed = 1f;
        /// <summary>Turn to the camera's yaw every time the camera turns.</summary>
        public bool Billboard = true;
        /// <summary>
        /// Something else decides the frame with <see cref="ShowFrame"/> (PLAN-0009 4.4, SPEC-0009 FR-5): <see cref="SpriteSystem"/>
        /// neither advances nor turns a driven actor.
        /// </summary>
        public bool Driven;

        /// <summary>What a layer renderer holds, so unchanged properties are not written again (SPEC-0009 FR-14).</summary>
        struct Written
        {
            public bool Enabled, Mirror;
            public short Slot;
            public Color32 Tint;
            public Vector3 Position;
            public float Rotation, ScaleX, ScaleY, ScaleZ;
            public SpriteAnimation Data;
        }

        SpriteRenderer[] _renderers = Array.Empty<SpriteRenderer>();
        Written[] _written = Array.Empty<Written>();
        int _animation, _direction;
        bool _hasAction;
        int _unique = -1;
        float _elapsedMs;
        long _ticks;          // whole frame times elapsed; −1 before the first frame is entered
        int _frame = -1;      // frame shown, −1 when nothing is
        bool _dirty = true;   // renderers must be written on the next tick
        int _applyCount;
        // Driven mode: what the last ShowFrame resolved.
        SpriteAnimation _shownData;
        bool _shownValid;     // Data was alive when it was set: the hot path does not ask again
        int _shownAnimation = -1, _shownDirection = -1;
        int _sortingBase;
        Color32 _tint = new Color32(255, 255, 255, 255);
        bool _grounded;
        float _groundHeight, _groundScale = 1f;

        internal int SystemIndex = -1;
        internal bool BillboardDirty = true;
        /// <summary>The palette set by <see cref="SpritePalette"/>, or null for the sprite's own colours (FR-7).</summary>
        internal Texture2D Palette;
        internal MaterialPropertyBlock PaletteBlock;

        public int Animation => _animation;
        public int Direction => _direction;
        /// <summary>The frame shown in the current action, or −1 when nothing is.</summary>
        public int CurrentFrame => _frame;
        /// <summary>False when the requested action is missing and the policy draws nothing (FR-5).</summary>
        public bool HasAction => _hasAction;
        /// <summary>The unique action played (index into <see cref="SpriteAnimation.Actions"/>), or −1.</summary>
        public int UniqueAction => _unique;
        /// <summary>Time into the current action in milliseconds: the shared clock a later composition can read.</summary>
        public float ElapsedMs => _elapsedMs;
        public IReadOnlyList<SpriteRenderer> LayerRenderers => _renderers;
        internal int ApplyCount => _applyCount;

        /// <summary>Fired once each time a frame with an event is entered: <c>atk</c> or a sound name (FR-3).</summary>
        public event Action<SpriteActor, string> FrameEvent;

        void OnEnable()
        {
            EnsureRenderers();
            _dirty = true;
            SpriteSystem.Register(this);
        }

        void OnDisable() => SpriteSystem.Unregister(this);

        /// <summary>
        /// Plays <paramref name="animation"/> in <paramref name="direction"/>. With <paramref name="restart"/> the action starts
        /// at frame 0; without it the time is kept, so a direction change keeps the frame.
        /// </summary>
        public void Play(int animation, int direction, bool restart = true)
        {
            EnsureRenderers();
            _animation = animation;
            _direction = ((direction % SpriteAnimation.Directions) + SpriteAnimation.Directions) % SpriteAnimation.Directions;
            _hasAction = Data != null && Data.TryGetAction(animation, _direction, Missing, out _unique);
            if (!_hasAction) _unique = -1;

            if (restart || !_hasAction)
            {
                _elapsedMs = 0f;
                _ticks = -1;
                _frame = -1;
            }
            else
            {
                // Keep the time; the frame index follows it in the new action without firing its event again.
                var action = Data.Actions[_unique];
                float frameMs = FrameMs(action);
                _ticks = (long)(_elapsedMs / frameMs);
                _frame = action.FrameCount > 0 ? (int)(_ticks % action.FrameCount) : -1;
            }
            _dirty = true;
        }

        /// <summary>
        /// Advances this actor alone by <paramref name="deltaSeconds"/> (tools and the editor preview, PLAN-0007 D15). In the
        /// game every enabled actor is advanced by <see cref="SpriteSystem"/>; do not call both for one actor.
        /// </summary>
        public void Step(float deltaSeconds) => Advance(deltaSeconds * 1000f);

        /// <summary>The anchor <paramref name="index"/> of the frame shown, in the actor's local space (FR-6).</summary>
        public bool TryGetAnchor(int index, out Vector3 localPosition)
        {
            localPosition = default;
            if (!_hasAction || _frame < 0 || index < 0) return false;
            var frame = Data.Frames[Data.Actions[_unique].FirstFrame + _frame];
            if (index >= frame.AnchorCount) return false;
            var a = Data.Anchors[frame.FirstAnchor + index];
            float ppu = Data.PixelsPerUnit;
            localPosition = new Vector3(a.x / ppu, -a.y / ppu, 0f);
            return true;
        }

        /// <summary>
        /// Driven mode (PLAN-0009 4.4; SPEC-0009 FR-5, FR-14): shows <paramref name="frame"/> (wrapped to the action's count) of an
        /// action. Renderers are written only when the (unique action, frame) changes, and the frame's event fires when a frame
        /// is entered; turning keeps the frame and fires nothing. False when the action is missing (nothing is drawn). No allocation
        /// once the renderers exist.
        /// </summary>
        public bool ShowFrame(int animation, int direction, int frame)
        {
            direction &= SpriteAnimation.Directions - 1;
            bool dataChanged = !ReferenceEquals(_shownData, Data);
            if (dataChanged)
            {
                _shownData = Data;
                _shownValid = Data != null;
                EnsureRenderers();
                _dirty = true;
            }
            bool restarted = dataChanged || animation != _shownAnimation;
            if (restarted || direction != _shownDirection)
            {
                _shownAnimation = _animation = animation;
                _shownDirection = _direction = direction;
                int before = _unique;
                _hasAction = _shownValid && Data.TryGetAction(animation, direction, Missing, out _unique);
                if (!_hasAction) _unique = -1;
                if (_unique != before) _dirty = true;
            }

            if (!_hasAction || Data.Actions[_unique].FrameCount <= 0)
            {
                _frame = -1;
                if (_dirty) HideFrom(0);
                return false;
            }

            var action = Data.Actions[_unique];
            frame %= action.FrameCount;
            if (frame < 0) frame += action.FrameCount;
            bool entered = restarted || frame != _frame;
            if (frame != _frame || _dirty)
            {
                _frame = frame;
                Apply(action);
            }
            if (entered) Raise(action, frame);
            return true;
        }

        /// <summary>The frames of the action <see cref="ShowFrame"/> would show, 0 when it is missing.</summary>
        public int FrameCountOf(int animation, int direction) =>
            Data != null && Data.TryGetAction(animation, direction & (SpriteAnimation.Directions - 1), Missing, out int unique) ? Data.Actions[unique].FrameCount : 0;

        /// <summary>Added to a layer's height before it goes into its z scale, so that it stays positive; also in <c>RoSprite.shader</c>.</summary>
        public const float HeightOffset = 16f;

        /// <summary>
        /// The actor's height above its ground point in world units, along the camera's up axis, and the display scale above it
        /// (PLAN-0009 4.10, D28; SPEC-0009 FR-16). Off until it is called.
        /// </summary>
        public void SetGround(float height, float scale)
        {
            if (_grounded && height == _groundHeight && scale == _groundScale) return;
            _grounded = true;
            _groundHeight = height;
            _groundScale = scale;
            _dirty = true;
        }

        /// <summary>The z scale that carries a height (world units) under a display scale (PLAN-0009 D28).</summary>
        public static float GroundScaleZ(float height, float scale) => scale > 0f ? (height + HeightOffset) / scale : 1f;

        /// <summary>The sorting order of the first layer; the others follow. Written only when it changes.</summary>
        public int SortingBase
        {
            get => _sortingBase;
            set
            {
                if (value == _sortingBase) return;
                _sortingBase = value;
                for (int i = 0; i < _renderers.Length; i++) _renderers[i].sortingOrder = value + i;
            }
        }

        /// <summary>Multiplies every layer's own tint.</summary>
        public Color32 Tint
        {
            get => _tint;
            set
            {
                if (value.r == _tint.r && value.g == _tint.g && value.b == _tint.b && value.a == _tint.a) return;
                _tint = value;
                _dirty = true;
            }
        }

        /// <summary>Hides every layer until the next <see cref="ShowFrame"/> (driven mode).</summary>
        public void Hide()
        {
            _shownData = null;
            _shownValid = false;
            _shownAnimation = _shownDirection = -1;
            _hasAction = false;
            _unique = -1;
            _frame = -1;
            HideFrom(0);
        }

        float FrameMs(in SpriteAnimation.ActionData action)
        {
            float ms = action.FrameMs * Speed;
            return ms > 0.001f ? ms : 0.001f;
        }

        /// <summary>Advances the clock by <paramref name="deltaMs"/> and writes the renderers if the frame changed. No allocation.</summary>
        internal void Advance(float deltaMs)
        {
            if (!_hasAction)
            {
                if (_dirty) HideFrom(0);
                return;
            }

            var action = Data.Actions[_unique];
            int count = action.FrameCount;
            if (count <= 0)
            {
                if (_dirty) HideFrom(0);
                return;
            }

            float frameMs = FrameMs(action);
            _elapsedMs += deltaMs;
            long ticks = (long)(_elapsedMs / frameMs);
            long passed = ticks - _ticks;

            if (passed > 0)
            {
                // Events of every frame entered since the last tick, at most one loop's worth (FR-3).
                long first = ticks - Math.Min(passed, count) + 1;
                for (long t = first; t <= ticks; t++) Raise(action, (int)(t % count));
            }

            int frame = (int)(ticks % count);
            _ticks = ticks;
            if (ticks >= count)
            {
                // Keep the clock small so float precision never drifts on long loops.
                _elapsedMs -= count * frameMs;
                _ticks -= count;
            }

            if (frame != _frame || _dirty)
            {
                _frame = frame;
                Apply(action);
            }
        }

        void Raise(in SpriteAnimation.ActionData action, int frame)
        {
            if (FrameEvent == null) return;
            int id = Data.Frames[action.FirstFrame + frame].EventId;
            if (id >= 0 && id < Data.Events.Length) FrameEvent(this, Data.Events[id]);
        }

        void Apply(in SpriteAnimation.ActionData action)
        {
            _dirty = false;
            _applyCount++;
            var frame = Data.Frames[action.FirstFrame + _frame];
            float ppu = Data.PixelsPerUnit;
            var sprites = Data.Sprites;
            int used = Math.Min(frame.LayerCount, _renderers.Length);
            for (int i = 0; i < used; i++)
            {
                var layer = Data.Layers[frame.FirstLayer + i];
                var r = _renderers[i];
                ref var w = ref _written[i];
                if (layer.Slot < 0 || layer.Slot >= sprites.Length || ReferenceEquals(sprites[layer.Slot], null))
                {
                    if (w.Enabled) r.enabled = w.Enabled = false;   // −1: draws nothing (FR-5)
                    continue;
                }
                if (w.Slot != layer.Slot || !ReferenceEquals(w.Data, Data))
                {
                    w.Slot = layer.Slot;
                    w.Data = Data;
                    r.sprite = sprites[layer.Slot];
                }
                if (w.Mirror != layer.Mirror) r.flipX = w.Mirror = layer.Mirror;
                var tint = Multiply(layer.Tint, _tint);
                if (tint.r != w.Tint.r || tint.g != w.Tint.g || tint.b != w.Tint.b || tint.a != w.Tint.a) r.color = w.Tint = tint;
                var position = new Vector3(layer.OffsetX / ppu, -layer.OffsetY / ppu, -i * LayerDepthStep);
                // A flat sprite has no use for its z scale: it carries the layer's height to the shader (PLAN-0009 D28).
                float scaleZ = _grounded ? GroundScaleZ(_groundHeight + position.y * _groundScale, _groundScale) : 1f;
                bool moved = !position.Equals(w.Position), turned = layer.Rotation != w.Rotation;
                bool scaled = layer.ScaleX != w.ScaleX || layer.ScaleY != w.ScaleY || scaleZ != w.ScaleZ;
                if (moved || turned || scaled)
                {
                    var t = r.transform;
                    if (moved) t.localPosition = w.Position = position;
                    if (turned)
                    {
                        w.Rotation = layer.Rotation;
                        t.localRotation = Quaternion.Euler(0f, 0f, -layer.Rotation);
                    }
                    if (scaled)
                    {
                        w.ScaleX = layer.ScaleX;
                        w.ScaleY = layer.ScaleY;
                        w.ScaleZ = scaleZ;
                        t.localScale = new Vector3(layer.ScaleX, layer.ScaleY, scaleZ);
                    }
                }
                if (!w.Enabled) r.enabled = w.Enabled = true;
            }
            HideFrom(used);
        }

        static Color32 Multiply(Color32 a, Color32 b) =>
            b.r == 255 && b.g == 255 && b.b == 255 && b.a == 255
                ? a
                : new Color32((byte)(a.r * b.r / 255), (byte)(a.g * b.g / 255), (byte)(a.b * b.b / 255), (byte)(a.a * b.a / 255));

        void HideFrom(int first)
        {
            _dirty = false;
            for (int i = first; i < _renderers.Length; i++)
                if (_written[i].Enabled) _renderers[i].enabled = _written[i].Enabled = false;
        }

        /// <summary>Creates (or finds again after a reload) one renderer per layer slot the data needs.</summary>
        void EnsureRenderers()
        {
            int needed = Data != null ? Math.Max(0, Data.MaxLayers) : 0;
            if (_renderers.Length >= needed && (needed == 0 || _renderers[needed - 1] != null)) return;

            var renderers = new SpriteRenderer[Math.Max(needed, _renderers.Length)];
            for (int i = 0; i < renderers.Length; i++)
            {
                string name = "Layer_" + i;
                var child = transform.Find(name);
                if (child == null)
                {
                    child = new GameObject(name).transform;
                    child.SetParent(transform, false);
                }
                var r = child.GetComponent<SpriteRenderer>();
                if (r == null) r = child.gameObject.AddComponent<SpriteRenderer>();
                r.sortingOrder = _sortingBase + i;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                if (Material != null) r.sharedMaterial = Material;
                r.flipX = false;
                r.color = Color.white;
                r.enabled = false;
                renderers[i] = r;
            }
            _renderers = renderers;
            // What the renderers hold is not known (new, or found again after a reload): everything is written once.
            _written = new Written[renderers.Length];
            for (int i = 0; i < _written.Length; i++) _written[i] = new Written { Slot = -2, Tint = new Color32(255, 255, 255, 255), Rotation = float.NaN, ScaleX = float.NaN, Position = new Vector3(float.NaN, 0f, 0f) };
            if (Palette != null) SpritePalette.Write(this);
        }
    }
}
