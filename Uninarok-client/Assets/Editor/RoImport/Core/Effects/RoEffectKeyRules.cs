using System;
using UnityEngine;
using Uninarok.RoImport.Core.Imaging;
using Uninarok.RoImport.Core.Paths;

namespace Uninarok.RoImport.Core.Effects
{
    /// <summary>Which color an effect texture's background is keyed out by (PLAN-0012 4.3).</summary>
    public enum RoEffectKey : byte { None, Pink, Black, PinkAndBlack }

    /// <summary>
    /// The colour key rules of the effect textures (PLAN-0012 4.3, D6): defaults by source kind, tolerances and per-texture
    /// overrides. Built in code (tests) or loaded from <c>Rules/effect-textures.json</c> (<see cref="FromJson"/>).
    /// </summary>
    public sealed class RoEffectKeyRules
    {
        /// <summary>BMP, 24-bit TGA, JPG.</summary>
        public RoEffectKey DefaultOpaque;

        /// <summary>32-bit TGA, PNG with alpha.</summary>
        public RoEffectKey DefaultWithAlpha;

        /// <summary>Per channel.</summary>
        public byte PinkTolerance = 10, BlackTolerance;

        /// <summary>Per texture; beats the defaults.</summary>
        public (RoPath Path, RoEffectKey Key)[] Overrides;

        /// <summary>The key of one texture: its override, else the default for its source kind.</summary>
        public RoEffectKey For(RoPath texture, bool sourceHadAlpha)
        {
            if (Overrides != null)
                foreach (var o in Overrides)
                    if (o.Path == texture) return o.Key;
            return sourceHadAlpha ? DefaultWithAlpha : DefaultOpaque;
        }

        /// <summary>The key and tolerances of one texture, ready for <see cref="RoColorKey.Apply(RoImage, RoKeySet)"/>.</summary>
        public RoKeySet KeySetFor(RoPath texture, bool sourceHadAlpha) =>
            new RoKeySet(For(texture, sourceHadAlpha), PinkTolerance, BlackTolerance);

        /// <summary>
        /// Reads rules from JSON text (the shape of <c>effect-textures.json</c>): key names as strings, tolerances as numbers,
        /// <c>Overrides</c> as a list of <c>{Path, Key}</c>. A missing field keeps its default; an unknown key name or an
        /// invalid path throws <see cref="FormatException"/>.
        /// </summary>
        public static RoEffectKeyRules FromJson(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            var model = JsonUtility.FromJson<Model>(json);
            if (model == null) throw new FormatException("effect key rules: empty JSON");

            var rules = new RoEffectKeyRules
            {
                DefaultOpaque = ParseKey(model.DefaultOpaque, nameof(model.DefaultOpaque)),
                DefaultWithAlpha = ParseKey(model.DefaultWithAlpha, nameof(model.DefaultWithAlpha)),
                PinkTolerance = ToTolerance(model.PinkTolerance, nameof(model.PinkTolerance)),
                BlackTolerance = ToTolerance(model.BlackTolerance, nameof(model.BlackTolerance)),
            };
            int n = model.Overrides?.Length ?? 0;
            rules.Overrides = new (RoPath, RoEffectKey)[n];
            for (int i = 0; i < n; i++)
            {
                if (!RoPath.TryParse(model.Overrides[i].Path, out RoPath path))
                    throw new FormatException("effect key rules: invalid override path '" + model.Overrides[i].Path + "'");
                rules.Overrides[i] = (path, ParseKey(model.Overrides[i].Key, "Overrides[" + i + "].Key"));
            }
            return rules;
        }

        static RoEffectKey ParseKey(string name, string field)
        {
            if (string.IsNullOrEmpty(name)) return RoEffectKey.None;
            if (Enum.TryParse(name, false, out RoEffectKey key) && Enum.IsDefined(typeof(RoEffectKey), key)) return key;
            throw new FormatException("effect key rules: unknown key '" + name + "' in " + field);
        }

        static byte ToTolerance(int value, string field)
        {
            if (value < 0 || value > 255) throw new FormatException("effect key rules: " + field + " out of range 0..255");
            return (byte)value;
        }

        [Serializable]
        class Model
        {
            public string DefaultOpaque;
            public string DefaultWithAlpha;
            public int PinkTolerance = 10;
            public int BlackTolerance;
            public OverrideModel[] Overrides;
        }

        [Serializable]
        class OverrideModel
        {
            public string Path;
            public string Key;
        }
    }
}
