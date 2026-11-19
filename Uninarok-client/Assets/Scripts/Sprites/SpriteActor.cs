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

        SpriteRenderer[] _renderers = Array.Empty<SpriteRenderer>();
        int _animation, _direction;
        bool _hasAction;
        int _unique = -1;
        float _elapsedMs;
        long _ticks;          // whole frame times elapsed; −1 before the first frame is entered
        int _frame = -1;      // frame shown, −1 when nothing is
        bool _dirty = true;   // renderers must be written on the next tick
        int _applyCount;

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
                if (layer.Slot < 0 || layer.Slot >= sprites.Length || sprites[layer.Slot] == null)
                {
                    r.enabled = false;   // −1: draws nothing (FR-5)
                    continue;
                }
                r.sprite = sprites[layer.Slot];
                r.flipX = layer.Mirror;
                r.color = layer.Tint;
                var t = r.transform;
                t.localPosition = new Vector3(layer.OffsetX / ppu, -layer.OffsetY / ppu, -i * LayerDepthStep);
                t.localRotation = Quaternion.Euler(0f, 0f, -layer.Rotation);
                t.localScale = new Vector3(layer.ScaleX, layer.ScaleY, 1f);
                r.enabled = true;
            }
            HideFrom(used);
        }

        void HideFrom(int first)
        {
            _dirty = false;
            for (int i = first; i < _renderers.Length; i++)
                if (_renderers[i].enabled) _renderers[i].enabled = false;
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
                r.sortingOrder = i;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                if (Material != null) r.sharedMaterial = Material;
                r.enabled = false;
                renderers[i] = r;
            }
            _renderers = renderers;
            if (Palette != null) SpritePalette.Write(this);
        }
    }
}
