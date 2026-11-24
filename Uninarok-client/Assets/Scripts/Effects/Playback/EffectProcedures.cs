using UnityEngine;

namespace Uninarok.Effects
{
    /// <summary>
    /// A registered procedural effect (SPEC-0012 FR-4, plan 4.2, D20): writes up to <see cref="MaxQuads"/> quads through
    /// <see cref="EffectQuadSpan"/> from the entry's data, the cycle position t in [0, 1], the instance seed and the cycle index.
    /// Keep no state: the same arguments give the same picture (a seek or a test sees what the player saw).
    /// </summary>
    public interface IEffectProcedure
    {
        int MaxQuads { get; }
        void Write(AuthoredData d, float t, uint seed, int cycle, ref EffectQuadSpan quads);
    }

    /// <summary>
    /// The procedure registry (plan D20). Empty in SPEC-0012: the must-list needs no procedure, so every procedural entry plays
    /// the placeholder with one warning. A procedure is added by a case in <see cref="Find"/> (a method, not a static field,
    /// so no static state needs cleanup across domain reloads).
    /// </summary>
    public static class EffectProcedures
    {
        public static bool TryGet(string name, out IEffectProcedure procedure)
        {
            procedure = string.IsNullOrEmpty(name) ? null : Find(name);
            return procedure != null;
        }

        static IEffectProcedure Find(string name)
        {
            return null;       // none built in (D20)
        }
    }

    /// <summary>
    /// A procedure's view of its submesh: a fixed number of quads (<see cref="Capacity"/> = the procedure's MaxQuads) written in
    /// order. The submesh has one material, taken from the first <see cref="Quad"/> call of the frame; later calls with
    /// another texture, blend or facing draw with that first material (plan 4.5: one material per authored entry). Quads
    /// the procedure does not write are hidden by the part after <c>Write</c>. Main thread only.
    /// </summary>
    public ref struct EffectQuadSpan
    {
        readonly EffectMeshWriter writer;
        readonly int submesh;
        readonly EffectRuntime runtime;
        readonly EffectAsset asset;
        int written;
        bool materialSet;

        internal EffectQuadSpan(EffectMeshWriter writer, int submesh, int capacity, EffectRuntime runtime, EffectAsset asset)
        {
            this.writer = writer; this.submesh = submesh; Capacity = capacity; this.runtime = runtime; this.asset = asset;
            written = 0; materialSet = false;
        }

        public int Capacity { get; }
        public int Written => written;

        /// <summary>Writes the next quad (offset in units, size in cells, as <see cref="AuthoredSampler.SampleQuad"/>); ignored when full.</summary>
        public void Quad(in AuthoredQuadSample q, EffectFacing facing, int texture, byte src, byte dst)
        {
            if (written >= Capacity) return;
            if (!materialSet)
            {
                materialSet = true;
                var textures = asset != null ? asset.Textures : null;
                Texture2D tex = textures != null && (uint)texture < (uint)textures.Length ? textures[texture] : null;
                var material = runtime.Materials.Get(tex, src, dst, facing, false, out bool known);
                if (!known) runtime.WarnBlendOnce(asset);
                writer.SetMaterial(submesh, material);
            }
            AuthoredPart.WriteQuad(writer, submesh, written++, q, facing);
        }

        internal void HideRest()
        {
            for (int i = written; i < Capacity; i++) writer.HideQuad(submesh, i);
        }
    }
}
