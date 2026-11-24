using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEngine;
using Uninarok.Effects;
using Uninarok.RoImport.Core.Effects;
using Uninarok.RoImport.Core.Source;

namespace Uninarok.RoImport.Tests.Effects
{
    /// <summary>
    /// SPEC-0012 FR-1, FR-5, FR-10 (PLAN-0012 4.9, D30, 6). The check only asks the source for names and existence, so
    /// the temp folders hold small invented files (never Ragnarok bytes); the assets are built in code and handed to the
    /// check through an in-code loader (no AssetDatabase).
    /// </summary>
    public class RoEffectDataCheckTests
    {
        const string Pending = "pending T6.2";
        const string SparkleKey = "texture/effect/_authored/warp_portal/sparkle.quad";
        const string OuterKey = "texture/effect/_authored/warp_portal/ring_outer.cylinder";
        const string InnerKey = "texture/effect/_authored/warp_portal/ring_inner.cylinder";

        string _temp;
        readonly List<UnityEngine.Object> _made = new List<UnityEngine.Object>();
        readonly Dictionary<string, EffectAsset> _byGuid = new Dictionary<string, EffectAsset>();

        [TearDown]
        public void TearDown()
        {
            if (_temp != null && Directory.Exists(_temp)) Directory.Delete(_temp, recursive: true);
            _temp = null;
            foreach (var o in _made) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            _made.Clear();
            _byGuid.Clear();
        }

        RoFolderSource Source(params string[] files)
        {
            _temp = Path.Combine(Path.GetTempPath(), "ro-effect-check-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_temp);
            foreach (var f in files)
            {
                string full = Path.Combine(_temp, f);
                Directory.CreateDirectory(Path.GetDirectoryName(full));
                File.WriteAllBytes(full, System.Text.Encoding.UTF8.GetBytes("test:" + f));
            }
            return new RoFolderSource(_temp);
        }

        EffectAsset Asset(string path, EffectKind kind, params string[] textureNames)
        {
            var a = ScriptableObject.CreateInstance<EffectAsset>();
            _made.Add(a);
            a.Path = path;
            a.Kind = kind;
            a.TextureNames = textureNames;
            return a;
        }

        /// <summary>Registers the asset in the index (a fake GUID) and the loader.</summary>
        EffectAsset Indexed(EffectAsset a)
        {
            _byGuid[Guid.NewGuid().ToString("N")] = a;
            return a;
        }

        EffectCatalogIndex Index()
        {
            var idx = ScriptableObject.CreateInstance<EffectCatalogIndex>();
            _made.Add(idx);
            var pairs = _byGuid.OrderBy(p => p.Value.Path, StringComparer.Ordinal).ToArray();
            idx.Keys = pairs.Select(p => p.Value.Path).ToArray();
            idx.Assets = pairs.Select(p => new EffectAssetRef { Guid = p.Key }).ToArray();
            idx.SourceMissing = new bool[pairs.Length];
            return idx;
        }

        RoEffectDataReport Run(IRoSource src, IEnumerable<EffectAsset> authored = null) =>
            RoEffectDataCheck.Run(src, Index(), r => _byGuid[r.Guid], authored);

        static string FolderHash(string root)
        {
            using (var sha = SHA256.Create())
            {
                var sb = new System.Text.StringBuilder();
                foreach (var f in Directory.GetFiles(root, "*", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.Ordinal))
                    sb.Append(f.Substring(root.Length)).Append(':')
                      .Append(string.Concat(sha.ComputeHash(File.ReadAllBytes(f)).Select(b => b.ToString("x2")))).Append('\n');
                return sb.ToString();
            }
        }

        [Test, Ignore(Pending)]
        public void Counts_PerKind_OnATempFolder()
        {
            // FR-1: .str anywhere; images under texture/effect/; .spr + .act under sprite/이팩트/; .lub under the effect tool folder.
            var src = Source(
                "texture/effect/a.str", "sprite/이팩트/e.str", "model/x/also_counted.str",
                "texture/effect/p1.tga", "texture/effect/sub/p2.bmp", "texture/effect/sub/p3.png", "texture/effect/readme.txt",
                "texture/other/not_counted.tga",
                "sprite/이팩트/e.spr", "sprite/이팩트/e.act", "sprite/이팩트/e2.spr", "sprite/other/not_counted.spr",
                "luafiles514/lua files/effecttool/t1.lub", "luafiles514/lua files/effecttool/t2.lub", "luafiles514/lua files/other/not_counted.lub");
            var report = Run(src);
            Assert.AreEqual(3, report.Str, "str");
            Assert.AreEqual(3, report.Textures, "images under texture/effect/");
            Assert.AreEqual(3, report.SprAct, "spr + act under sprite/이팩트/");
            Assert.AreEqual(2, report.ToolTables, "lub");
        }

        [Test, Ignore(Pending)]
        public void MissingTexture_NamedByTwoStrs_IsListedOnce_WithTheFirstReferrerInOrder()
        {
            var src = Source("texture/effect/b.str", "texture/effect/c.str", "texture/effect/ok.tga");
            Indexed(Asset("texture/effect/c.str", EffectKind.Str, "nope.tga", "ok.tga"));
            Indexed(Asset("texture/effect/b.str", EffectKind.Str, "nope.tga"));
            var report = Run(src);
            Assert.AreEqual(1, report.Missing.Count);
            Assert.AreEqual(("nope.tga", "texture/effect/b.str"), report.Missing[0]);
        }

        [Test, Ignore(Pending)]
        public void Missing_IsSortedOrdinally_ByFileThenReferrer()
        {
            var src = Source("texture/effect/b.str", "texture/effect/a.str");
            Indexed(Asset("texture/effect/b.str", EffectKind.Str, "zz.tga", "aa.tga"));
            Indexed(Asset("texture/effect/a.str", EffectKind.Str, "mm.tga"));
            var report = Run(src);
            CollectionAssert.AreEqual(new[] { "aa.tga", "mm.tga", "zz.tga" }, report.Missing.Select(m => m.File).ToArray());
        }

        [Test, Ignore(Pending)]
        public void DeletingOneStr_ListsItAsMissing_WithTheIndexAsReferrer()
        {
            // D30: computed from the source (TryGetFile), not from index.SourceMissing.
            var src = Source("texture/effect/a.str", "texture/effect/b.str");
            Indexed(Asset("texture/effect/a.str", EffectKind.Str));
            Indexed(Asset("texture/effect/b.str", EffectKind.Str));
            Assert.IsEmpty(Run(src).Missing);

            File.Delete(Path.Combine(_temp, "texture/effect/b.str"));
            src.Refresh();
            var report = Run(src);
            Assert.AreEqual(1, report.Missing.Count);
            Assert.AreEqual(("texture/effect/b.str", "(index)"), report.Missing[0]);
        }

        [Test, Ignore(Pending)]
        public void StrTexture_IsResolvedLikeTheLookup_OwnFolderThenRoot_BareNameTga()
        {
            var src = Source("texture/effect/sub/a.str", "texture/effect/sub/own.tga", "texture/effect/root.bmp");
            Indexed(Asset("texture/effect/sub/a.str", EffectKind.Str, "own.tga", "root", "own"));
            Assert.IsEmpty(Run(src).Missing);
        }

        [Test, Ignore(Pending)]
        public void SpriteKind_NeedsTheActAndTheSpr()
        {
            var src = Source("sprite/이팩트/has.act", "sprite/이팩트/has.spr", "sprite/이팩트/half.act");
            var has = Asset("texture/effect/_authored/has.sprite", EffectKind.Sprite);
            has.SpriteName = "has";
            var half = Asset("texture/effect/_authored/half.sprite", EffectKind.Sprite);
            half.SpriteName = "half";
            Indexed(has);
            Indexed(half);
            var report = Run(src);
            Assert.AreEqual(1, report.Missing.Count);
            Assert.AreEqual(("sprite/이팩트/half.spr", "texture/effect/_authored/half.sprite"), report.Missing[0]);
        }

        [Test, Ignore(Pending)]
        public void ModelKind_NeedsItsPathFile()
        {
            var src = Source("model/ok.rsm");
            Indexed(Asset("model/ok.rsm", EffectKind.Model));
            Indexed(Asset("model/gone.rsm", EffectKind.Model));
            var report = Run(src);
            Assert.AreEqual(("model/gone.rsm", "model/gone.rsm"), report.Missing.Single());
        }

        [Test, Ignore(Pending)]
        public void AuthoredAsset_TexturesResolveFromTheEffectRoot_AndNeedNoSourceFile()
        {
            var src = Source("texture/effect/glow.tga");
            var good = Asset("texture/effect/_authored/good.quad", EffectKind.Quad, "glow.tga");
            var bad = Asset("texture/effect/_authored/bad.quad", EffectKind.Quad, "nope.tga");
            var report = Run(src, new[] { good, bad });
            Assert.AreEqual(("nope.tga", "texture/effect/_authored/bad.quad"), report.Missing.Single());
        }

        [Test, Ignore(Pending)]
        public void AssetInTheIndexAndInAuthored_IsExaminedOnce()
        {
            var src = Source("texture/effect/ok.tga");
            var a = Indexed(Asset("texture/effect/_authored/dup.quad", EffectKind.Quad, "nope.tga"));
            var report = Run(src, new[] { a, a });
            Assert.AreEqual(1, report.Missing.Count);
        }

        [Test, Ignore(Pending)]
        public void AuthoredNull_IsAccepted()
        {
            var src = Source("texture/effect/ok.tga");
            Assert.DoesNotThrow(() => Run(src, null));
        }

        [Test, Ignore(Pending)]
        public void WarpPortal_ListsTheThreeKeys_PresentOnlyWhenFoundWithAllTextures()
        {
            var src = Source("texture/effect/ok.tga");
            Indexed(Asset(SparkleKey, EffectKind.Quad, "ok.tga"));
            Indexed(Asset(OuterKey, EffectKind.Cylinder, "nope.tga"));
            // ring_inner is not in the index at all
            var report = Run(src);
            CollectionAssert.AreEqual(new[] { (SparkleKey, true), (OuterKey, false), (InnerKey, false) }, report.WarpPortal.ToArray());
            string text = report.ToText();
            StringAssert.Contains("warp portal " + SparkleKey + ": present", text);
            StringAssert.Contains("warp portal " + OuterKey + ": missing", text);
            StringAssert.Contains("warp portal " + InnerKey + ": missing", text);
            Assert.AreEqual(1, report.Missing.Count, "the warp portal lines are not in Missing; only the unresolved texture is");
        }

        [Test, Ignore(Pending)]
        public void ToText_Order_IsCountsThenMissingThenWarpPortal_WithLfLineEnds()
        {
            var src = Source("texture/effect/a.str");
            Indexed(Asset("texture/effect/a.str", EffectKind.Str, "nope.tga"));
            string text = Run(src).ToText();
            Assert.IsFalse(text.Contains("\r"));
            int missing = text.IndexOf("nope.tga", StringComparison.Ordinal);
            int warp = text.IndexOf("warp portal", StringComparison.Ordinal);
            Assert.Greater(missing, 0);
            Assert.Greater(warp, missing);
        }

        [Test, Ignore(Pending)]
        public void TwoRuns_GiveEqualText()
        {
            var src = Source("texture/effect/a.str", "texture/effect/ok.tga");
            Indexed(Asset("texture/effect/a.str", EffectKind.Str, "nope.tga", "ok.tga", "also_nope.tga"));
            Indexed(Asset(SparkleKey, EffectKind.Quad, "ok.tga"));
            string first = Run(src).ToText();
            string second = Run(src).ToText();
            Assert.AreEqual(first, second);
            Assert.IsNotEmpty(first);
        }

        [Test, Ignore(Pending)]
        public void FolderHashes_AreUnchangedAfterARun_ReadOnly()
        {
            var src = Source("texture/effect/a.str", "texture/effect/ok.tga", "sprite/이팩트/e.spr");
            Indexed(Asset("texture/effect/a.str", EffectKind.Str, "nope.tga"));
            string before = FolderHash(_temp);
            Run(src).ToText();
            Assert.AreEqual(before, FolderHash(_temp));
        }
    }
}
