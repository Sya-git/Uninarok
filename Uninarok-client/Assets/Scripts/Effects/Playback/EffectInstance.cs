using UnityEngine;

namespace Uninarok.Effects
{
    /// <summary>
    /// What draws one instance (SPEC-0012 plan 4.5 "Parts"): <c>StrPart</c>, <c>AuthoredPart</c>, <c>SpritePart</c>,
    /// <c>ModelPart</c>, <c>PlaceholderPart</c> . The runtime owns the clock; a part only reads it. Main thread only.
    /// </summary>
    internal interface IEffectPart
    {
        /// <summary>
        /// Binds to the instance's loaded asset (warm-up work allowed: components, mesh layout). Returns the length of one
        /// cycle in effect seconds when the part knows it (the sprite action length), or a value ≤ 0 for the runtime's rule (D29).
        /// </summary>
        float Bind(EffectInstance instance);

        /// <summary>False when the last <see cref="Bind"/> could not draw; the runtime then plays the placeholder instead (FR-4, D20).</summary>
        bool CanDraw { get; }

        /// <summary>Why the last bind could not draw; null when it can.</summary>
        string Problem { get; }

        /// <summary>Draws the instance at <see cref="EffectInstance.Age"/>, after the clock moved; no allocation.</summary>
        void Tick(EffectInstance instance);

        /// <summary>Stops drawing at once (the instance is going back to the pool); nothing of it may draw in the next frame.</summary>
        void Release(EffectInstance instance);
    }

    internal enum EffectInstanceState : byte
    {
        Free = 0,
        /// <summary>Active, its asset still loading: draws nothing, clock stopped (D29).</summary>
        Waiting,
        Playing,
    }

    /// <summary>
    /// One pooled instance slot of an <see cref="EffectRuntime"/>. Its fields are the runtime's; parts read them.
    /// The <see cref="Object"/> (under the runtime's root) is created on the slot's first play and kept.
    /// </summary>
    internal sealed class EffectInstance
    {
        public readonly int Slot;
        public int Generation = 1;
        public EffectInstanceState State;

        public EffectRef Entry;
        /// <summary>The loaded asset; null while waiting and for a failed load (placeholder rule).</summary>
        public EffectAsset Asset;
        public bool Loop;
        public float Speed = 1f;

        /// <summary>Effect seconds into the current cycle: [0, Length) for a loop, [0, Length] for a one-shot (D29).</summary>
        public float Age;
        /// <summary>Completed cycles of a loop (authored cycle index).</summary>
        public int Cycle;
        /// <summary>Effect seconds the clock moved in the last tick (sprite step).</summary>
        public float Delta;
        /// <summary>Set by <see cref="EffectRuntime.Seek"/>: the part resets its cursors on its next tick, then clears it.</summary>
        public bool Seeked;
        /// <summary>Length of one cycle in effect seconds, set at bind (always positive and finite).</summary>
        public float Length;
        /// <summary>Frames per effect second for <see cref="EffectRuntime.FrameOf"/>: the STR fps, else the authored frame rate.</summary>
        public float FrameRate;
        /// <summary>D19: slot, generation and entry hashed, drawn once per play.</summary>
        public uint Seed;

        public EffectPlace Place;
        public Vector3 Position;
        public Transform Unit;
        public IEffectPart Part;
        internal int BornTick;

        public GameObject Object;
        /// <summary>The instance's one mesh writer (MeshFilter and MeshRenderer on <see cref="Object"/>), made at the slot's
        /// first drawing bind and kept; parts of every drawing kind share it.</summary>
        EffectMeshWriter writer;
        /// <summary>This slot's pooled <c>StrPart</c> (kept between plays).</summary>
        public StrPart StrPart;
        /// <summary>This slot's pooled <c>AuthoredPart</c> (Quad, Cylinder, Procedural; kept between plays).</summary>
        public AuthoredPart AuthoredPart;
        /// <summary>This slot's pooled <c>SpritePart</c> (and its <c>SpriteActor</c>; kept between plays).</summary>
        public SpritePart SpritePart;
        /// <summary>This slot's pooled <c>ModelPart</c> (and its prefab instance; kept between plays).</summary>
        public ModelPart ModelPart;
        /// <summary>This slot's pooled <c>PlaceholderPart</c> (kept between plays).</summary>
        public PlaceholderPart PlaceholderPart;

        public EffectInstance(int slot) { Slot = slot; }

        public EffectMeshWriter GetWriter()
        {
            if (writer == null)
            {
                var filter = Object.GetComponent<MeshFilter>();
                if (filter == null) filter = Object.AddComponent<MeshFilter>();
                var renderer = Object.GetComponent<MeshRenderer>();
                if (renderer == null) renderer = Object.AddComponent<MeshRenderer>();
                writer = new EffectMeshWriter(filter, renderer);
            }
            return writer;
        }

        public void DisposeWriter()
        {
            writer?.Dispose();
            writer = null;
        }

        public EffectHandle Handle => new EffectHandle(Slot, Generation);
    }
}
