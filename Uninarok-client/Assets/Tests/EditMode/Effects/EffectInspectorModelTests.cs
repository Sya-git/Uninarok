using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Uninarok.Effects.Editor;
using Uninarok.RoImport.Core.Effects;

namespace Uninarok.Effects.Tests
{
    /// <summary>FR-2, FR-11: the data of the effect inspector, gathered without a window.</summary>
    public sealed class EffectInspectorModelTests
    {
        const string SparkleKey = "texture/effect/_authored/warp_portal/sparkle.quad";

        // The firewall.str row of the T0.1 record (evidence/str-survey.txt): fps 60, 40 frames, 11 layers.
        static readonly int[] FirewallKeys = { 0, 11, 11, 11, 13, 13, 13, 23, 23, 27, 41 };
        static readonly int[] FirewallTextures = { 0, 2, 2, 2, 2, 2, 2, 2, 2, 2, 1 };

        static EffectAsset FirewallLike(out Texture2D placeholder)
        {
            var a = ScriptableObject.CreateInstance<EffectAsset>();
            a.hideFlags = HideFlags.HideAndDontSave;
            a.Path = "texture/effect/firewall.str";
            a.Kind = EffectKind.Str;
            a.Fps = 60f; a.FrameCount = 40;
            var layers = new EffectAsset.LayerData[FirewallKeys.Length];
            var keys = new System.Collections.Generic.List<EffectAsset.KeyData>();
            int tex = 0;
            for (int l = 0; l < layers.Length; l++)
            {
                layers[l] = new EffectAsset.LayerData { FirstKey = keys.Count, KeyCount = FirewallKeys[l], FirstTexture = tex, TextureCount = FirewallTextures[l] };
                for (int k = 0; k < FirewallKeys[l]; k++)
                    keys.Add(new EffectAsset.KeyData { Frame = k, SrcBlend = 5, DstBlend = (byte)(l == 4 && k == 0 ? 2 : 7) });   // layer 4 uses a second pair
                tex += FirewallTextures[l];
            }
            a.Layers = layers;
            a.Keys = keys.ToArray();
            a.TextureNames = Enumerable.Range(0, tex).Select(i => "t" + (i % 4) + ".bmp").ToArray();
            var ph = new Texture2D(2, 2) { hideFlags = HideFlags.HideAndDontSave };
            var real = new Texture2D(2, 2) { hideFlags = HideFlags.HideAndDontSave };
            placeholder = ph;
            a.Textures = Enumerable.Range(0, tex).Select(i => i == 0 ? null : (i == 1 ? ph : real)).ToArray();
            return a;
        }

        [Test]
        public void SyntheticStrAsset_CountsMatchTheT01Row_FR2()
        {
            var asset = FirewallLike(out var placeholder);
            try
            {
                var m = EffectInspectorModel.Build(asset, new EffectInspectorInput { Placeholder = placeholder });
                Assert.That(m.Kind, Is.EqualTo(EffectKind.Str));
                Assert.That(m.Fps, Is.EqualTo(60f));
                Assert.That(m.FrameCount, Is.EqualTo(40));
                Assert.That(m.LengthSeconds, Is.EqualTo(40f / 60f).Within(1e-6f));
                Assert.That(m.Layers.Select(l => l.KeyCount), Is.EqualTo(FirewallKeys));
                Assert.That(m.Layers.Select(l => l.TextureCount), Is.EqualTo(FirewallTextures));
                Assert.That(m.KeyCount, Is.EqualTo(FirewallKeys.Sum()));
                Assert.That(m.Textures.Count, Is.EqualTo(FirewallTextures.Sum()));
                Assert.That(m.Layers[1].BlendPairs.Count, Is.EqualTo(1));
                Assert.That(m.Layers[4].BlendPairs.Count, Is.EqualTo(2), "distinct pairs per layer");
                Assert.That(m.Layers[1].BlendPairs[0].ToString(), Is.EqualTo("SrcAlpha(5) / DstAlpha(7)"));
                Assert.That(m.Textures.Select(t => t.Name).Distinct().Count(), Is.EqualTo(4), "distinct textures");
                Assert.That(m.Textures[0].MissingOrPlaceholder, Is.True, "null texture");
                Assert.That(m.Textures[1].MissingOrPlaceholder, Is.True, "placeholder texture");
                Assert.That(m.Textures[2].MissingOrPlaceholder, Is.False);
            }
            finally { Object.DestroyImmediate(asset); Object.DestroyImmediate(placeholder); }
        }

        [Test]
        public void AppliedKey_IsReadFromUserData_FR2()
        {
            Assert.That(EffectInspectorModel.AppliedKeyOf("alpha=Keyed;key=Pink"), Is.EqualTo("Pink"));
            Assert.That(EffectInspectorModel.AppliedKeyOf("key=Black"), Is.EqualTo("Black"));
            Assert.That(EffectInspectorModel.AppliedKeyOf("alpha=None"), Is.Null);
            Assert.That(EffectInspectorModel.AppliedKeyOf(null), Is.Null);

            var asset = FirewallLike(out var placeholder);
            try
            {
                var m = EffectInspectorModel.Build(asset, new EffectInspectorInput { Placeholder = placeholder, UserDataOf = t => "key=Pink" });
                Assert.That(m.Textures[2].AppliedKey, Is.EqualTo("Pink"));
                Assert.That(m.Textures[0].AppliedKey, Is.Null, "no texture, no key");
            }
            finally { Object.DestroyImmediate(asset); Object.DestroyImmediate(placeholder); }
        }

        [Test]
        public void Warnings_CollectCheckLedgerAndRuntimeLines_AndDoubtful_FR11()
        {
            var asset = FirewallLike(out var placeholder);
            try
            {
                asset.Doubtful = new[] { "SizePixels: lead" };
                var report = new RoEffectDataReport
                {
                    Missing = new[] { ("a.bmp", asset.Path), ("b.bmp", "other.str") },
                };
                var m = EffectInspectorModel.Build(asset, new EffectInspectorInput
                {
                    CheckLines = EffectInspectorModel.CheckLinesFor(report, asset.Path),
                    LedgerLines = new[] { "ledger line" },
                    RuntimeLines = new[] { "runtime line" },
                });
                Assert.That(m.Warnings, Is.EqualTo(new[]
                {
                    "check: missing a.bmp (referenced by texture/effect/firewall.str)", "ledger: ledger line", "runtime: runtime line",
                }));
                Assert.That(m.Doubtful, Is.EqualTo(new[] { "SizePixels: lead" }));
            }
            finally { Object.DestroyImmediate(asset); Object.DestroyImmediate(placeholder); }
        }

        [Test]
        public void SparkleQuad_CurvesAreSampledForPlotting_FR11()
        {
            var asset = LoadByKey(SparkleKey);
            Assert.That(asset, Is.Not.Null);
            var m = EffectInspectorModel.Build(asset, new EffectInspectorInput { AuthoredFrameRate = 60f, Speed = 2f });
            Assert.That(m.Kind, Is.EqualTo(EffectKind.Quad));
            Assert.That(m.Speed, Is.EqualTo(2f));
            Assert.That(m.LengthSeconds, Is.EqualTo(asset.Length));
            Assert.That(m.FrameCount, Is.EqualTo(Mathf.RoundToInt(asset.Length * 60f)));
            Assert.That(m.AuthoredValues.Select(kv => kv.Key), Does.Contain("SizePixels"));
            var authored = m.Curves.Where(c => c.Authored).ToArray();
            Assert.That(authored, Is.Not.Empty, "the sparkle authors at least one curve");
            foreach (var c in authored)
            {
                Assert.That(c.Samples.Length, Is.EqualTo(EffectInspectorModel.PlotSamples), c.Name);
                Assert.That(c.Samples.All(v => !float.IsNaN(v) && !float.IsInfinity(v)), Is.True, c.Name);
                Assert.That(c.Min, Is.LessThanOrEqualTo(c.Max));
            }
        }

        [Test]
        public void SpriteAsset_HasHeaderAndNoCurves_FR11()
        {
            var guid = AssetDatabase.FindAssets("t:EffectAsset")
                .Select(g => AssetDatabase.LoadAssetAtPath<EffectAsset>(AssetDatabase.GUIDToAssetPath(g)))
                .FirstOrDefault(a => a != null && a.Kind == EffectKind.Sprite);
            Assert.That(guid, Is.Not.Null, "an imported sprite effect entry exists");
            var m = EffectInspectorModel.Build(guid, new EffectInspectorInput());
            Assert.That(m.Kind, Is.EqualTo(EffectKind.Sprite));
            Assert.That(m.Path, Is.EqualTo(guid.Path));
            Assert.That(m.Curves, Is.Empty);
            Assert.That(m.Layers, Is.Empty);
        }

        static EffectAsset LoadByKey(string key) =>
            AssetDatabase.FindAssets("t:EffectAsset")
                .Select(g => AssetDatabase.LoadAssetAtPath<EffectAsset>(AssetDatabase.GUIDToAssetPath(g)))
                .FirstOrDefault(a => a != null && a.Path == key);
    }
}
