using System;
using System.Collections.Generic;
using Uninarok.Map;
using UnityEngine;

namespace Uninarok.Effects
{
    /// <summary>
    /// The effect player of one world (the game's through <c>EffectSystem</c>, or a preview's): pooled instance slots with
    /// generations, placement, the one player clock and lifetime (SPEC-0012 FR-4, FR-6, FR-9; plan 4.5, D14, D19, D29).
    /// <para>Clock: each <see cref="Tick"/>, <c>Age += Δt × TimeScale × Speed</c> for every kind. A one-shot ends on the tick
    /// its clock reaches its length; a loop wraps. <see cref="Stop"/> returns the instance to the pool in the same call.</para>
    /// <para>An instance whose asset is still loading is active, draws nothing and starts on the tick its asset is there
    /// (polled, no callback: D29). Main thread only. No managed allocation in <see cref="Tick"/>, nor in a play after the
    /// slot's first use.</para>
    /// </summary>
    public sealed class EffectRuntime : IDisposable
    {
        readonly EffectCatalog catalog;
        readonly EffectSettings settings;
        readonly Transform root;
        readonly EffectInstance[] slots;
        readonly int[] free;              // stack of free slot indices
        int freeCount;
        int activeCount;
        int tickNumber;
        float timeScale = 1f;
        bool warnedFull, disposed;
        readonly EffectMaterials materials;
        readonly HashSet<EffectAsset> blendWarned = new HashSet<EffectAsset>();

        public EffectRuntime(EffectCatalog catalog, EffectSettings settings, Transform root, int capacity = 512)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.settings = settings != null ? settings : throw new ArgumentNullException(nameof(settings));
            this.root = root != null ? root : throw new ArgumentNullException(nameof(root));
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity), "capacity must be at least 1");
            materials = new EffectMaterials(settings);

            slots = new EffectInstance[capacity];
            free = new int[capacity];
            for (int i = 0; i < capacity; i++)
            {
                slots[i] = new EffectInstance(i);
                free[i] = capacity - 1 - i;       // slot 0 on top
            }
            freeCount = capacity;
        }

        public EffectCatalog Catalog => catalog;
        public EffectSettings Settings => settings;
        public int Capacity => slots.Length;
        internal EffectMaterials Materials => materials;

        /// <summary>One warning per asset for an unknown blend code (plan 4.5 Blend). T5.8 folds this into <c>Warned</c>.</summary>
        internal void WarnBlendOnce(EffectAsset asset)
        {
            if (asset == null || !blendWarned.Add(asset)) return;
            Debug.LogWarning($"[Effects] Effect '{asset.Path}': a key has an unknown blend code (outside 1..11); SrcAlpha / OneMinusSrcAlpha is used.");
        }

        /// <summary>Whole-runtime time scale (preview speed, pause at 0); default 1. Negative or non-finite → 0.</summary>
        public float TimeScale
        {
            get => timeScale;
            set => timeScale = Sanitize(value);
        }

        /// <summary>Active instances, waiting ones included.</summary>
        public int ActiveCount => activeCount;

        // ---- play ----

        /// <summary>Plays at a world point. <c>default</c> when the pool is full (one warning).</summary>
        public EffectHandle Play(EffectRef effect, Vector3 point, bool loop = false)
        {
            var inst = Acquire(effect, loop, EffectPlace.Point, point, null);
            return inst != null ? inst.Handle : default;
        }

        /// <summary>Follows the unit's position (not its rotation). A unit destroyed later leaves the instance at its last
        /// position, ending normally (plan 5). A null unit plays nothing and returns <c>default</c>.</summary>
        public EffectHandle PlayOn(EffectRef effect, Transform unit, bool loop = false)
        {
            if (unit == null) return default;
            var inst = Acquire(effect, loop, EffectPlace.Unit, unit.position, unit);
            return inst != null ? inst.Handle : default;
        }

        /// <summary>Plays at <see cref="MapData.CellCenter"/> of (x, y). A null map plays nothing and returns <c>default</c>.</summary>
        public EffectHandle PlayAtCell(EffectRef effect, MapData map, int x, int y, bool loop = false)
        {
            if (map == null) return default;
            var inst = Acquire(effect, loop, EffectPlace.Cell, map.CellCenter(x, y), null);
            return inst != null ? inst.Handle : default;
        }

        EffectInstance Acquire(EffectRef effect, bool loop, EffectPlace place, Vector3 position, Transform unit)
        {
            ThrowIfDisposed();
            if (freeCount == 0)
            {
                if (!warnedFull)
                {
                    warnedFull = true;
                    Debug.LogWarning($"[Effects] Effect runtime: all {slots.Length} instance slots are in use; further plays are skipped.");
                }
                return null;
            }

            var inst = slots[free[--freeCount]];
            activeCount++;
            inst.Entry = effect;
            inst.Asset = null;
            inst.Loop = loop;
            inst.Speed = 1f;
            inst.Age = 0f;
            inst.Cycle = 0;
            inst.Delta = 0f;
            inst.Seeked = false;
            inst.Length = 0f;
            inst.FrameRate = 0f;
            inst.Part = null;
            inst.Place = place;
            inst.Position = position;
            inst.Unit = unit;
            inst.BornTick = tickNumber;
            inst.Seed = Seed(inst.Slot, inst.Generation, effect.Index);
            inst.State = EffectInstanceState.Waiting;

            if (inst.Object == null)
            {
                inst.Object = new GameObject("Effect");     // first use of the slot (warm-up)
                inst.Object.transform.SetParent(root, false);
            }
            var t = inst.Object.transform;
            t.SetPositionAndRotation(position, Quaternion.identity);
            inst.Object.SetActive(true);

            catalog.RequestAsset(effect);                     // no callback: polled (D29); sync sources complete here
            TryBind(inst);
            return inst;
        }

        /// <summary>Binds when the asset is loaded or has failed (placeholder rule); stays waiting while loading.</summary>
        void TryBind(EffectInstance inst)
        {
            EffectAsset asset = null;
            if (!catalog.TryGetAsset(inst.Entry, out asset))
            {
                var state = catalog.GetState(inst.Entry);
                if (state == EffectAssetState.Loading) return;
                if (state == EffectAssetState.NotLoaded)
                {
                    catalog.RequestAsset(inst.Entry);         // released meanwhile: ask again
                    if (!catalog.TryGetAsset(inst.Entry, out asset) && catalog.GetState(inst.Entry) != EffectAssetState.Failed)
                        return;
                }
            }

            inst.Asset = asset;                               // null: failed, placeholder rule
            inst.Part = CreatePart(inst);
            float partLength = inst.Part != null ? inst.Part.Bind(inst) : 0f;
            inst.Length = LengthOf(asset, partLength, settings);
            inst.FrameRate = asset != null && asset.Kind == EffectKind.Str && asset.Fps > 0f && !float.IsInfinity(asset.Fps)
                ? asset.Fps
                : settings.AuthoredFrameRate;
            inst.State = EffectInstanceState.Playing;
            Normalize(inst);
        }

        /// <summary>The part that draws this instance's kind; STR and the authored kinds exist (T5.7-T5.8 add the rest; others only keep time).</summary>
        IEffectPart CreatePart(EffectInstance inst)
        {
            var asset = inst.Asset;
            if (asset == null) return null;
            switch (asset.Kind)
            {
                case EffectKind.Str:
                    return inst.StrPart ?? (inst.StrPart = new StrPart(this));
                case EffectKind.Quad:
                case EffectKind.Cylinder:
                case EffectKind.Procedural:
                    return inst.AuthoredPart ?? (inst.AuthoredPart = new AuthoredPart(this));
            }
            return null;
        }

        /// <summary>
        /// Length of one cycle (D29): the part's own when positive and finite; else STR FrameCount / Fps, Quad / Cylinder /
        /// Model <c>Length</c>; Sprite without a part, Procedural, a failed load or any invalid length → PlaceholderSeconds
        /// (itself → 1 s if invalid).
        /// </summary>
        internal static float LengthOf(EffectAsset asset, float partLength, EffectSettings settings)
        {
            if (Positive(partLength)) return partLength;
            float length = 0f;
            if (asset != null)
            {
                switch (asset.Kind)
                {
                    case EffectKind.Str: length = EffectSampler.Length(asset); break;
                    case EffectKind.Quad:
                    case EffectKind.Cylinder:
                    case EffectKind.Model: length = asset.Length; break;
                }
            }
            if (Positive(length)) return length;
            return Positive(settings.PlaceholderSeconds) ? settings.PlaceholderSeconds : 1f;
        }

        static bool Positive(float v) => v > 0f && !float.IsInfinity(v);

        // ---- stop and query ----

        /// <summary>Ends the instance at once: inactive in the same call, back in the pool, nothing of it drawn next frame.</summary>
        public void Stop(EffectHandle h)
        {
            if (TryGet(h, out var inst)) Release(inst);
        }

        public void StopAll()
        {
            for (int i = 0; i < slots.Length; i++)
                if (slots[i].State != EffectInstanceState.Free) Release(slots[i]);
        }

        public bool IsActive(EffectHandle h) => TryGet(h, out _);

        /// <summary>FR-4 play-speed multiplier of one instance, any kind; default 1; negative or non-finite → 0.</summary>
        public void SetSpeed(EffectHandle h, float speed)
        {
            if (TryGet(h, out var inst)) inst.Speed = Sanitize(speed);
        }

        /// <summary>The instance's speed; 0 when the handle is not active.</summary>
        public float SpeedOf(EffectHandle h) => TryGet(h, out var inst) ? inst.Speed : 0f;

        /// <summary>
        /// Sets <c>Age</c> directly (exact, no simulation; cursors reset): a one-shot is clamped to [0, length], a loop wraps
        /// with its cycle index. Before the asset is there the value is kept and applied at bind. NaN is ignored.
        /// </summary>
        public void Seek(EffectHandle h, float seconds)
        {
            if (!TryGet(h, out var inst) || float.IsNaN(seconds)) return;
            inst.Age = seconds < 0f ? 0f : seconds;
            inst.Cycle = 0;
            inst.Seeked = true;
            if (inst.State == EffectInstanceState.Playing) Normalize(inst);
        }

        /// <summary>The instance's fractional frame in its current cycle (<c>Age × fps</c>: STR fps, else the authored frame
        /// rate); -1 when the handle is not active.</summary>
        public float FrameOf(EffectHandle h)
        {
            if (!TryGet(h, out var inst)) return -1f;
            float rate = inst.State == EffectInstanceState.Playing ? inst.FrameRate : 0f;
            return inst.Age * rate;
        }

        /// <summary>The instance behind an active handle (tests, parts, dev tools); null otherwise.</summary>
        internal EffectInstance InstanceOf(EffectHandle h) => TryGet(h, out var inst) ? inst : null;

        bool TryGet(EffectHandle h, out EffectInstance inst)
        {
            inst = null;
            if (h.Generation == 0 || (uint)h.Slot >= (uint)slots.Length) return false;
            var s = slots[h.Slot];
            if (s.State == EffectInstanceState.Free || s.Generation != h.Generation) return false;
            inst = s;
            return true;
        }

        void Release(EffectInstance inst)
        {
            var part = inst.Part;
            inst.Part = null;
            inst.State = EffectInstanceState.Free;     // inactive before the part's callback can observe it
            part?.Release(inst);
            if (inst.Object != null) inst.Object.SetActive(false);
            inst.Asset = null;
            inst.Unit = null;
            inst.Generation = inst.Generation == int.MaxValue ? 1 : inst.Generation + 1;
            free[freeCount++] = inst.Slot;
            activeCount--;
        }

        // ---- clock ----

        /// <summary>Advances every active instance by <paramref name="deltaSeconds"/> (wall seconds); negative or non-finite → 0.</summary>
        public void Tick(float deltaSeconds)
        {
            ThrowIfDisposed();
            tickNumber++;
            float scaled = Sanitize(deltaSeconds) * timeScale;
            for (int i = 0; i < slots.Length; i++)
            {
                var inst = slots[i];
                if (inst.State == EffectInstanceState.Free || inst.BornTick == tickNumber) continue;

                if (inst.Place == EffectPlace.Unit && inst.Unit != null)
                {
                    var p = inst.Unit.position;
                    if (p != inst.Position)
                    {
                        inst.Position = p;
                        if (inst.Object != null) inst.Object.transform.position = p;
                    }
                }

                if (inst.State == EffectInstanceState.Waiting)
                {
                    TryBind(inst);
                    if (inst.State != EffectInstanceState.Playing) continue;
                }

                float step = scaled * inst.Speed;
                if (!(step >= 0f) || float.IsInfinity(step)) step = 0f;
                inst.Delta = step;
                inst.Age += step;

                if (!inst.Loop)
                {
                    if (step > 0f && inst.Age >= inst.Length) { Release(inst); continue; }
                }
                else
                {
                    Normalize(inst);
                }

                if (inst.Part != null)
                {
                    inst.Part.Tick(inst);
                    if (inst.State == EffectInstanceState.Free) continue;   // stopped from inside the part
                }
                inst.Seeked = false;
            }
        }

        /// <summary>One-shot: Age in [0, Length]; loop: Age in [0, Length) with completed cycles counted (D29).</summary>
        static void Normalize(EffectInstance inst)
        {
            float len = inst.Length;
            if (!inst.Loop)
            {
                if (inst.Age > len) inst.Age = len;
                return;
            }
            if (inst.Age < len) return;
            double cycles = Math.Floor((double)inst.Age / len);
            inst.Age = (float)(inst.Age - cycles * len);
            if (!(inst.Age >= 0f) || inst.Age >= len) inst.Age = 0f;   // float edge at the boundary
            long c = inst.Cycle + (long)cycles;
            inst.Cycle = c > int.MaxValue ? int.MaxValue : (int)c;
        }

        static float Sanitize(float v) => v > 0f && !float.IsInfinity(v) ? v : 0f;

        /// <summary>D19 instance seed: slot, generation and entry index mixed (murmur3 finalizer); no string work per play.</summary>
        static uint Seed(int slot, int generation, int entry)
        {
            uint h = (uint)slot * 0x9E3779B1u ^ (uint)generation * 0x85EBCA77u ^ (uint)entry * 0xC2B2AE3Du;
            h ^= h >> 16; h *= 0x85EBCA6Bu;
            h ^= h >> 13; h *= 0xC2B2AE35u;
            h ^= h >> 16;
            return h;
        }

        void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(EffectRuntime));
        }

        /// <summary>Stops everything and destroys the pooled objects (preview close, domain reload, tests).</summary>
        public void Dispose()
        {
            if (disposed) return;
            StopAll();
            disposed = true;
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i].DisposeWriter();
                var o = slots[i].Object;
                slots[i].Object = null;
                if (o == null) continue;
                if (Application.isPlaying) UnityEngine.Object.Destroy(o);
                else UnityEngine.Object.DestroyImmediate(o);
            }
            materials.Dispose();
        }
    }
}
