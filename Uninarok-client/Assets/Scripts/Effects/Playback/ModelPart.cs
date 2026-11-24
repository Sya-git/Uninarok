using UnityEngine;

namespace Uninarok.Effects
{
    /// <summary>
    /// Draws a Model entry (SPEC-0012 FR-4, FR-9; plan 4.5 "Parts"): the entry's SPEC-0003 prefab instantiated once per pooled slot
    /// as a child of the instance object and enabled and disabled with the instance. A rebind to the same prefab reuses the
    /// instance; only a different prefab destroys it and instantiates the new one. The cycle length is the asset's <c>Length</c>
    /// (the runtime's rule when that is not positive).
    /// </summary>
    internal sealed class ModelPart : IEffectPart
    {
        GameObject model;
        GameObject source;
        bool bound;

        /// <summary>False when the last bind could not draw (the caller shows the placeholder, T5.8).</summary>
        public bool CanDraw => bound;

        /// <summary>Why the last bind could not draw; null when it can.</summary>
        public string Problem { get; private set; }

        /// <summary>The slot's prefab instance (null before the first bind); tests and dev tools.</summary>
        internal GameObject Model => model;

        public float Bind(EffectInstance instance)
        {
            bound = false;
            Problem = null;
            if (model != null) model.SetActive(false);
            var asset = instance.Asset;
            if (asset == null) { Problem = "has no asset"; return 0f; }
            if (asset.Model == null) { Problem = "has no model prefab"; return 0f; }

            if (model == null || !ReferenceEquals(source, asset.Model))
            {
                if (model != null) Object.Destroy(model);
                model = Object.Instantiate(asset.Model, instance.Object.transform, false);
                source = asset.Model;
            }
            model.SetActive(true);
            bound = true;
            return asset.Length;
        }

        public void Tick(EffectInstance instance) { }

        public void Release(EffectInstance instance)
        {
            bound = false;
            if (model != null) model.SetActive(false);
        }
    }
}
