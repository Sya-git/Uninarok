using System.Linq;
using NUnit.Framework;
using UnityEditor;
using Uninarok.Effects.Editor;
using Uninarok.RoImport.Core.Effects;

namespace Uninarok.Effects.Tests
{
    /// <summary>
    /// FR-2, FR-11 (T8.3): the inspector model of the real imported STR EffectAssets of the 10 reference files equals the
    /// T0.1 record (docs/specs/0012-effects/str-reference.md section 4). The expected values are constants copied from that
    /// record; no RO file is read. The record counts layer 0 (empty in all ten files), and so does the model (one LayerInfo
    /// per asset layer), so the layer count and the per-layer lists are compared as they stand.
    /// </summary>
    public sealed class EffectInspectorReferenceTests
    {
        const float Fps = 60f;

        struct Row
        {
            public string Key;
            public int Frames, DistinctTextures;
            public int[] Keys, Textures;
        }

        static int[] Ones(int n) => Enumerable.Repeat(1, n).ToArray();

        static readonly Row[] Rows =
        {
            new Row { Key = "texture/effect/firewall.str", Frames = 40, DistinctTextures = 4,
                Keys = new[] { 0, 11, 11, 11, 13, 13, 13, 23, 23, 27, 41 }, Textures = new[] { 0, 2, 2, 2, 2, 2, 2, 2, 2, 2, 1 } },
            new Row { Key = "texture/effect/cross.str", Frames = 121, DistinctTextures = 2,
                Keys = new[] { 0, 45, 45, 34, 34, 17, 17, 15, 15, 21, 5 }, Textures = new[] { 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 } },
            new Row { Key = "texture/effect/angelus.str", Frames = 236, DistinctTextures = 14,
                Keys = new[] { 0, 30, 119, 119, 3, 3, 119, 119, 3, 27, 43 }, Textures = new[] { 0, 31, 1, 1, 1, 1, 1, 1, 1, 1, 1 } },
            new Row { Key = "texture/effect/cure.str", Frames = 107, DistinctTextures = 5,
                Keys = new[] { 0, 36, 26, 26, 73, 5, 3, 33, 33, 51, 51, 51 }, Textures = new[] { 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 } },
            new Row { Key = "texture/effect/freeze.str", Frames = 60, DistinctTextures = 3,
                Keys = new[] { 0, 26, 26, 11, 8, 14, 14, 14 }, Textures = new[] { 0, 1, 1, 1, 1, 1, 1, 1 } },
            new Row { Key = "texture/effect/bs_refinesuccess.str", Frames = 100, DistinctTextures = 6,
                Keys = new[] { 0, 33, 18, 15, 21, 12, 12, 12, 12, 11, 11, 3, 3, 3, 3, 7, 3 },
                Textures = new[] { 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 } },
            new Row { Key = "texture/effect/dust.str", Frames = 120, DistinctTextures = 7,
                Keys = new[] { 0, 11, 11, 11, 11, 5, 11, 5, 11, 5, 11, 5, 11, 5, 11, 5, 11, 5, 11, 5, 11, 11, 11, 5, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 5, 5, 5, 5, 5, 3, 7, 7, 7 },
                Textures = new[] { 0 }.Concat(Ones(45)).ToArray() },
            new Row { Key = "texture/effect/new_splash/new_splash.str", Frames = 38, DistinctTextures = 20,
                Keys = new[] { 0, 3 }, Textures = new[] { 0, 20 } },
            new Row { Key = "texture/effect/mineffect/new_earthgrave/new_earthgrave_bottom/new_earthgrave_bottom.str", Frames = 48, DistinctTextures = 1,
                Keys = new[] { 0, 5, 5 }, Textures = new[] { 0, 1, 1 } },
            // "sprite/<Korean 'effect' folder>/jong_mini.str", the folder written as escapes
            new Row { Key = "sprite/이팩트/jong_mini.str", Frames = 100, DistinctTextures = 10,
                Keys = new[] { 0, 21, 5, 5, 38, 38 }, Textures = new[] { 0, 31, 1, 1, 1, 1 } },
        };

        static System.Collections.Generic.IEnumerable<TestCaseData> Cases() =>
            Rows.Select(r => new TestCaseData(r).SetName("ReferenceFile_" + System.IO.Path.GetFileName(r.Key)));

        [TestCaseSource(nameof(Cases))]
        public void ModelOfTheImportedAsset_EqualsTheT01Record_FR2(object boxed)
        {
            var row = (Row)boxed;
            string assetPath = "Assets/Art/Imported/effect/" + row.Key + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<EffectAsset>(assetPath);
            Assert.That(asset, Is.Not.Null, "imported EffectAsset at " + assetPath);

            var m = EffectInspectorModel.Build(asset, new EffectInspectorInput());
            Assert.That(m.Path, Is.EqualTo(row.Key));
            Assert.That(m.Kind, Is.EqualTo(EffectKind.Str));
            Assert.That(m.Fps, Is.EqualTo(Fps));
            Assert.That(m.FrameCount, Is.EqualTo(row.Frames));
            Assert.That(m.LengthSeconds, Is.EqualTo(row.Frames / Fps).Within(1e-5f));
            Assert.That(m.Layers.Count, Is.EqualTo(row.Keys.Length), "layers (record counts the empty layer 0)");
            Assert.That(m.Layers.Select(l => l.KeyCount), Is.EqualTo(row.Keys), "keys per layer");
            Assert.That(m.Layers.Select(l => l.TextureCount), Is.EqualTo(row.Textures), "textures per layer");
            Assert.That(m.KeyCount, Is.EqualTo(row.Keys.Sum()), "key count");
            Assert.That(m.Textures.Count, Is.EqualTo(row.Textures.Sum()), "texture entries (flat, by layer)");
            Assert.That(m.Textures.Select(t => (t.Name ?? "").ToLowerInvariant()).Distinct().Count(), Is.EqualTo(row.DistinctTextures), "distinct textures");
        }
    }
}
