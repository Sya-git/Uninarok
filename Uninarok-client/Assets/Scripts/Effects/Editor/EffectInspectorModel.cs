using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Uninarok.RoImport.Core.Effects;

namespace Uninarok.Effects.Editor
{
    /// <summary>What the inspector needs besides the asset itself (all optional); filled by the window or the asset editor.</summary>
    public sealed class EffectInspectorInput
    {
        /// <summary>Frames per second of the authored kinds (<see cref="EffectSettings.AuthoredFrameRate"/>).</summary>
        public float AuthoredFrameRate = 60f;
        /// <summary>The texture shown where a texture is missing; a layer texture that is null or this one is marked.</summary>
        public Texture2D Placeholder;
        /// <summary>Importer userData of a texture (editor: <c>AssetImporter.GetAtPath(...).userData</c>); null = none.</summary>
        public Func<Texture2D, string> UserDataOf;
        /// <summary>Check lines about this effect (see <see cref="EffectInspectorModel.CheckLinesFor"/>).</summary>
        public IEnumerable<string> CheckLines;
        /// <summary>Import report lines the ledger run kept for this effect.</summary>
        public IEnumerable<string> LedgerLines;
        /// <summary>Messages the runtime raised through <see cref="EffectRuntime.Warned"/> for this effect.</summary>
        public IEnumerable<string> RuntimeLines;
        /// <summary>Speed multiplier of the previewed instance.</summary>
        public float Speed = 1f;
    }

    /// <summary>
    /// The data of the effect inspector (SPEC-0012 FR-2, FR-11, plan 4.11), gathered without any GUI so that tests can read
    /// it: header (key, kind, fps, length in frames and seconds, key count), per layer key count and the distinct blend
    /// pairs, the authored values with a sampled plot of each curve, per texture its applied key and missing marker, the
    /// warnings and the Doubtful lines.
    /// </summary>
    public sealed class EffectInspectorModel
    {
        public const int PlotSamples = 64;

        public struct BlendPair : IEquatable<BlendPair>
        {
            public byte Src, Dst;
            public bool Equals(BlendPair o) => Src == o.Src && Dst == o.Dst;
            public override bool Equals(object o) => o is BlendPair p && Equals(p);
            public override int GetHashCode() => Src * 256 + Dst;
            public override string ToString() => EffectInspectorModel.BlendName(Src) + " / " + EffectInspectorModel.BlendName(Dst);
        }

        public sealed class LayerInfo
        {
            public int Index, KeyCount, TextureCount;
            /// <summary>Distinct (source, destination) pairs of the layer's keys, in order of first use (blend is per key, FR-2).</summary>
            public List<BlendPair> BlendPairs = new List<BlendPair>();
        }

        public sealed class CurveInfo
        {
            public string Name;
            public bool Authored;               // false: the field is null (the runtime uses its constant default)
            public float[] Samples;             // PlotSamples values over t in [0, 1]; empty when not authored
            public float Min, Max;
        }

        public sealed class TextureInfo
        {
            public int Index;
            public string Name;
            public Texture2D Texture;
            /// <summary>The colour key the importer applied (<c>key=</c> in userData); null when none is recorded.</summary>
            public string AppliedKey;
            /// <summary>True when the texture is missing or is the placeholder.</summary>
            public bool MissingOrPlaceholder;
        }

        public string Path;
        public EffectKind Kind;
        public float Fps;
        public int FrameCount;
        public float LengthSeconds;
        public int KeyCount;
        public float Speed;
        public List<LayerInfo> Layers = new List<LayerInfo>();
        public List<KeyValuePair<string, string>> AuthoredValues = new List<KeyValuePair<string, string>>();
        public List<CurveInfo> Curves = new List<CurveInfo>();
        public List<TextureInfo> Textures = new List<TextureInfo>();
        public List<string> Warnings = new List<string>();
        public List<string> Doubtful = new List<string>();

        /// <summary>Name of a Direct3D blend code with the code, e.g. <c>SrcAlpha(5)</c>; <c>?(n)</c> outside 1..11.</summary>
        public static string BlendName(byte code)
        {
            // the file's Direct3D factor, not the Unity factor it plays with (7, 8, 11 read destination alpha 1, plan D33)
            EffectBlend.ToUnity(code, out bool known);
            return (known ? D3DBlendNames[code - 1] : "?") + "(" + code.ToString(CultureInfo.InvariantCulture) + ")";
        }

        static readonly string[] D3DBlendNames =
        {
            "Zero", "One", "SrcColor", "OneMinusSrcColor", "SrcAlpha", "OneMinusSrcAlpha",
            "DstAlpha", "OneMinusDstAlpha", "DstColor", "OneMinusDstColor", "SrcAlphaSaturate",
        };

        /// <summary>The <c>key=</c> value of an importer userData string (tokens split by ';', ',', space or line end), or null.</summary>
        public static string AppliedKeyOf(string userData)
        {
            if (string.IsNullOrEmpty(userData)) return null;
            foreach (string token in userData.Split(new[] { ';', ',', ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries))
                if (token.StartsWith("key=", StringComparison.Ordinal)) return token.Substring(4);
            return null;
        }

        /// <summary>The report's missing lines that refer to <paramref name="key"/> (the effect's own file or a file it references).</summary>
        public static List<string> CheckLinesFor(RoEffectDataReport report, string key)
        {
            var lines = new List<string>();
            if (report == null) return lines;
            foreach (var m in report.Missing)
                if (string.Equals(m.Referrer, key, StringComparison.Ordinal) || string.Equals(m.File, key, StringComparison.Ordinal))
                    lines.Add("missing " + m.File + " (referenced by " + m.Referrer + ")");
            return lines;
        }

        public static EffectInspectorModel Build(EffectAsset asset, EffectInspectorInput input = null)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            input = input ?? new EffectInspectorInput();
            var m = new EffectInspectorModel { Path = asset.Path, Kind = asset.Kind, Speed = input.Speed };

            if (asset.Kind == EffectKind.Str)
            {
                m.Fps = asset.Fps;
                m.FrameCount = asset.FrameCount;
                m.LengthSeconds = asset.Fps > 0f ? asset.FrameCount / asset.Fps : 0f;
            }
            else
            {
                m.Fps = input.AuthoredFrameRate;
                m.LengthSeconds = asset.Length;
                m.FrameCount = Mathf.RoundToInt(asset.Length * input.AuthoredFrameRate);
            }
            m.KeyCount = asset.Keys?.Length ?? 0;

            AddLayers(m, asset);
            bool authoredKind = asset.Kind == EffectKind.Quad || asset.Kind == EffectKind.Cylinder || asset.Kind == EffectKind.Procedural;
            if (authoredKind && asset.Authored != null) AddAuthored(m, asset.Authored);
            AddTextures(m, asset, input);
            AddLines(m.Warnings, "check: ", input.CheckLines);
            AddLines(m.Warnings, "ledger: ", input.LedgerLines);
            AddLines(m.Warnings, "runtime: ", input.RuntimeLines);
            if (asset.Doubtful != null) m.Doubtful.AddRange(asset.Doubtful);
            return m;
        }

        static void AddLines(List<string> to, string prefix, IEnumerable<string> lines)
        {
            if (lines == null) return;
            foreach (string l in lines) to.Add(prefix + l);
        }

        static void AddLayers(EffectInspectorModel m, EffectAsset asset)
        {
            if (asset.Layers == null) return;
            for (int i = 0; i < asset.Layers.Length; i++)
            {
                var l = asset.Layers[i];
                var info = new LayerInfo { Index = i, KeyCount = l.KeyCount, TextureCount = l.TextureCount };
                for (int k = l.FirstKey; k < l.FirstKey + l.KeyCount && asset.Keys != null && k < asset.Keys.Length; k++)
                {
                    var key = asset.Keys[k];
                    var pair = new BlendPair { Src = key.SrcBlend, Dst = key.DstBlend };
                    if (!info.BlendPairs.Contains(pair)) info.BlendPairs.Add(pair);
                }
                m.Layers.Add(info);
            }
        }

        static void AddAuthored(EffectInspectorModel m, AuthoredData d)
        {
            var inv = CultureInfo.InvariantCulture;
            void V(string name, string value) => m.AuthoredValues.Add(new KeyValuePair<string, string>(name, value));
            string F(float f) => f.ToString("0.###", inv);
            string V3(Vector3 v) => "(" + F(v.x) + ", " + F(v.y) + ", " + F(v.z) + ")";

            V("Facing", d.Facing.ToString());
            V("Blend", new BlendPair { Src = d.SrcBlend, Dst = d.DstBlend }.ToString());
            V("Tint", ColorUtility.ToHtmlStringRGBA(d.Tint));
            V("MaxAlpha", F(d.MaxAlpha));
            V("Texture", d.Texture.ToString(inv));
            switch (m.Kind)
            {
                case EffectKind.Quad:
                    V("SizePixels", F(d.SizePixels));
                    V("StartMin", V3(d.StartMin)); V("StartMax", V3(d.StartMax));
                    V("EndMin", V3(d.EndMin)); V("EndMax", V3(d.EndMax));
                    break;
                case EffectKind.Cylinder:
                    V("BottomRadius", F(d.BottomRadius)); V("TopRadius", F(d.TopRadius)); V("Height", F(d.Height));
                    V("Sides", d.Sides.ToString(inv)); V("RepeatU", F(d.RepeatU));
                    break;
                case EffectKind.Procedural:
                    V("Procedure", d.Procedure ?? "");
                    break;
            }

            AddCurve(m, "Alpha", d.Alpha);
            AddCurve(m, "Rotation", d.Rotation);
            if (m.Kind == EffectKind.Quad) { AddCurve(m, "SizeScale", d.SizeScale); AddCurve(m, "Motion", d.Motion); }
            if (m.Kind == EffectKind.Cylinder) { AddCurve(m, "RadiusScale", d.RadiusScale); AddCurve(m, "HeightScale", d.HeightScale); }
        }

        static void AddCurve(EffectInspectorModel m, string name, AnimationCurve curve)
        {
            var c = new CurveInfo { Name = name, Authored = curve != null && curve.length > 0, Samples = Array.Empty<float>() };
            if (c.Authored)
            {
                c.Samples = new float[PlotSamples];
                c.Min = float.MaxValue; c.Max = float.MinValue;
                for (int i = 0; i < PlotSamples; i++)
                {
                    float v = curve.Evaluate(i / (float)(PlotSamples - 1));
                    c.Samples[i] = v;
                    if (v < c.Min) c.Min = v;
                    if (v > c.Max) c.Max = v;
                }
            }
            m.Curves.Add(c);
        }

        static void AddTextures(EffectInspectorModel m, EffectAsset asset, EffectInspectorInput input)
        {
            int n = Math.Max(asset.TextureNames?.Length ?? 0, asset.Textures?.Length ?? 0);
            for (int i = 0; i < n; i++)
            {
                var tex = asset.Textures != null && i < asset.Textures.Length ? asset.Textures[i] : null;
                var info = new TextureInfo
                {
                    Index = i,
                    Name = asset.TextureNames != null && i < asset.TextureNames.Length ? asset.TextureNames[i] : null,
                    Texture = tex,
                    MissingOrPlaceholder = tex == null || (input.Placeholder != null && tex == input.Placeholder),
                    AppliedKey = tex != null && input.UserDataOf != null ? AppliedKeyOf(input.UserDataOf(tex)) : null,
                };
                m.Textures.Add(info);
            }
        }
    }
}
