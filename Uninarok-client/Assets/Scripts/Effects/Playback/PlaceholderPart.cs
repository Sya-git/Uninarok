using UnityEngine;

namespace Uninarok.Effects
{
    /// <summary>
    /// What plays when an effect cannot draw: an unknown path, a failed or missing asset, an unbuilt kind (a procedural entry
    /// with no registered procedure), a part whose <c>CanDraw</c> is false (SPEC-0012 FR-1, FR-4, FR-5; plan 4.4, 4.5, D20).
    /// One camera-facing quad, one cell wide and one cell tall, standing on the instance position, textured with
    /// <see cref="EffectSettings.PlaceholderTexture"/>. It is <see cref="EffectSettings.PlaceholderSeconds"/> long (the runtime
    /// loops it when the play is looped). The quad is written once at bind; a tick draws nothing more. Pooled with its slot.
    /// </summary>
    internal sealed class PlaceholderPart : IEffectPart
    {
        readonly EffectRuntime runtime;
        bool bound;

        public PlaceholderPart(EffectRuntime runtime) { this.runtime = runtime; }

        public bool CanDraw => bound;

        public string Problem => null;

        public float Bind(EffectInstance instance)
        {
            bound = false;
            var writer = instance.GetWriter();
            writer.BeginLayout();
            int sub = writer.AddQuads(1);
            float h = EffectUnits.GatCell * 0.5f;
            if (!writer.EndLayout(new Bounds(new Vector3(0f, h, 0f), new Vector3(EffectUnits.GatCell, EffectUnits.GatCell, EffectUnits.GatCell))) || sub < 0)
            {
                writer.Upload();
                return Seconds(runtime.Settings);
            }

            var material = runtime.Materials.Get(null, 5, 6, EffectFacing.Camera, false);   // null texture: the placeholder texture
            writer.SetMaterial(sub, material);
            writer.WriteQuad(sub, 0,
                new Vector3(-h, 2f * h, 0f), new Vector3(h, 2f * h, 0f), new Vector3(h, 0f, 0f), new Vector3(-h, 0f, 0f),
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Color32(255, 255, 255, 255));
            writer.Upload();
            bound = true;
            return Seconds(runtime.Settings);
        }

        public void Tick(EffectInstance instance) { }

        public void Release(EffectInstance instance) { bound = false; }

        /// <summary>The placeholder's length: <c>PlaceholderSeconds</c>, 1 s when that is not positive and finite.</summary>
        internal static float Seconds(EffectSettings settings)
        {
            float s = settings.PlaceholderSeconds;
            return s > 0f && !float.IsInfinity(s) ? s : 1f;
        }
    }
}
