using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Uninarok.RoImport.Core.Imaging;
using Uninarok.RoImport.Core.Materials;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Source;
using Uninarok.RoImport.Editor.Materials;
using Uninarok.RoImport.Editor.Pipeline;
using Uninarok.RoImport.Editor.Writing;

namespace Uninarok.RoImport.Tests
{
    // SPEC-0002 FR-9: materials are lit, shared and de-duplicated; a two-sided face renders from both sides.
    // Plan 4.6 (property table), D10, D11. Writes under Assets/__RoImportTests. TASKS T5.1.
    [Ignore("pending T5.3")]
    public class RoMaterialTests
    {
        const string Texture = "texture/a.bmp";

        RoOutputRoots _roots;
        string _ledgerFolder;

        [SetUp]
        public void SetUp()
        {
            _roots = RoImportTestRoot.Create();
            _ledgerFolder = Path.Combine(Path.GetTempPath(), "RoImportTests", Guid.NewGuid().ToString("N"));
            // The converted texture the materials use (the texture converter, T6.2, produces it in a real run).
            var image = new RoImage(2, 2);
            for (int i = 0; i < 4; i++) image.SetPixel(i % 2, i / 2, Color.white);
            new RoAssetWriter(_roots).WriteBytes(RoOutputPaths.Texture(_roots, RoPath.Parse(Texture)), new RoPngCodec().Encode(image));
        }

        [TearDown]
        public void TearDown()
        {
            RoImportTestRoot.Delete();
            if (Directory.Exists(_ledgerFolder)) Directory.Delete(_ledgerFolder, recursive: true);
        }

        RoImportContext Context() =>
            new RoImportContext(new RoFolderSource(RoFolderSourceTests.FixtureRoot), _roots, _ledgerFolder) { Png = new RoPngCodec() };

        static RoMaterialKey Key(RoSurface surface = RoSurface.Opaque, bool twoSided = false, string texture = Texture) =>
            new RoMaterialKey(RoPath.Parse(texture), surface, twoSided);

        Material Get(RoImportContext ctx, RoMaterialKey key)
        {
            var result = RoMaterialFactory.GetOrCreate(ctx, key);
            Assert.AreNotEqual(RoStatus.Failed, result.Status, result.Failure?.ToString());
            return result.Value;
        }

        int MaterialFiles() =>
            Directory.Exists(RoImportTestRoot.FullPath(_roots.Art + "/_materials"))
                ? Directory.GetFiles(RoImportTestRoot.FullPath(_roots.Art + "/_materials"), "*.mat", SearchOption.AllDirectories).Length
                : 0;

        [Test]
        public void OneKeyRequested1000Times_YieldsOneMaterialAsset()
        {
            var ctx = Context();
            var first = Get(ctx, Key(RoSurface.Cutout));

            for (int i = 0; i < 999; i++)
                Assert.IsTrue(first == Get(ctx, Key(RoSurface.Cutout)));

            Assert.AreEqual(1, MaterialFiles());
            Assert.AreEqual(RoOutputPaths.Material(_roots, Key(RoSurface.Cutout)), AssetDatabase.GetAssetPath(first));
        }

        [Test]
        public void KeysDifferingInSurfaceOrSidedness_GiveDifferentMaterials()
        {
            var ctx = Context();

            var materials = new[]
            {
                Get(ctx, Key(RoSurface.Opaque)), Get(ctx, Key(RoSurface.Cutout)), Get(ctx, Key(RoSurface.Blend)),
                Get(ctx, Key(RoSurface.Opaque, true)),
            };

            Assert.AreEqual(4, materials.Select(AssetDatabase.GetAssetPath).Distinct().Count());
            Assert.AreEqual(4, MaterialFiles());
        }

        [Test]
        public void SecondRun_IsSkipped_AndRewritesNoFile([Values] RoSurface surface, [Values] bool twoSided)
        {
            Assert.AreEqual(RoStatus.Created, RoMaterialFactory.GetOrCreate(Context(), Key(surface, twoSided)).Status);
            AssetDatabase.SaveAssets();
            string file = RoImportTestRoot.FullPath(RoOutputPaths.Material(_roots, Key(surface, twoSided)));
            var old = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(file, old);

            var again = RoMaterialFactory.GetOrCreate(Context(), Key(surface, twoSided));   // a new run: no memo
            AssetDatabase.SaveAssets();

            Assert.AreEqual(RoStatus.Skipped, again.Status);
            Assert.AreEqual(old, File.GetLastWriteTimeUtc(file), "URP's save-time tags and passes are set by the factory (plan 4.6)");
        }

        [Test]
        public void EveryMaterial_IsLitUrp_WithTheTextureAndNoSpecular([Values] RoSurface surface)
        {
            var m = Get(Context(), Key(surface));

            Assert.AreEqual("Universal Render Pipeline/Lit", m.shader.name);
            Assert.IsTrue(m.GetTexture("_BaseMap") == AssetDatabase.LoadAssetAtPath<Texture2D>(RoOutputPaths.Texture(_roots, RoPath.Parse(Texture))));
            Assert.AreEqual(Color.white, m.GetColor("_BaseColor"));
            Assert.AreEqual(0f, m.GetFloat("_Metallic"));
            Assert.AreEqual(0f, m.GetFloat("_Smoothness"));
            Assert.IsTrue(m.IsKeywordEnabled("_SPECULARHIGHLIGHTS_OFF"));
            Assert.IsTrue(m.IsKeywordEnabled("_ENVIRONMENTREFLECTIONS_OFF"));
        }

        // Plan 4.6 table: _Surface, _AlphaClip, _Cutoff, blend factors, _ZWrite, queue, keywords.
        [Test]
        public void Opaque_MatchesThePropertyTable()
        {
            var m = Get(Context(), Key(RoSurface.Opaque));

            Assert.AreEqual(0f, m.GetFloat("_Surface"));
            Assert.AreEqual(0f, m.GetFloat("_AlphaClip"));
            Assert.AreEqual((float)BlendMode.One, m.GetFloat("_SrcBlend"));
            Assert.AreEqual((float)BlendMode.Zero, m.GetFloat("_DstBlend"));
            Assert.AreEqual(1f, m.GetFloat("_ZWrite"));
            Assert.AreEqual(2000, m.renderQueue);
            Assert.IsFalse(m.IsKeywordEnabled("_ALPHATEST_ON"));
            Assert.IsFalse(m.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"));
        }

        [Test]
        public void Cutout_MatchesThePropertyTable()
        {
            var m = Get(Context(), Key(RoSurface.Cutout));

            Assert.AreEqual(0f, m.GetFloat("_Surface"));
            Assert.AreEqual(1f, m.GetFloat("_AlphaClip"));
            Assert.AreEqual(0.5f, m.GetFloat("_Cutoff"));
            Assert.AreEqual((float)BlendMode.One, m.GetFloat("_SrcBlend"));
            Assert.AreEqual((float)BlendMode.Zero, m.GetFloat("_DstBlend"));
            Assert.AreEqual(1f, m.GetFloat("_ZWrite"));
            Assert.AreEqual(2450, m.renderQueue);
            Assert.IsTrue(m.IsKeywordEnabled("_ALPHATEST_ON"));
            Assert.IsFalse(m.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"));
        }

        [Test]
        public void Blend_MatchesThePropertyTable()
        {
            var m = Get(Context(), Key(RoSurface.Blend));

            Assert.AreEqual(1f, m.GetFloat("_Surface"));
            Assert.AreEqual(0f, m.GetFloat("_AlphaClip"));
            Assert.AreEqual((float)BlendMode.SrcAlpha, m.GetFloat("_SrcBlend"));
            Assert.AreEqual((float)BlendMode.OneMinusSrcAlpha, m.GetFloat("_DstBlend"));
            Assert.AreEqual(0f, m.GetFloat("_ZWrite"));
            Assert.AreEqual(3000, m.renderQueue);
            Assert.IsFalse(m.IsKeywordEnabled("_ALPHATEST_ON"));
            Assert.IsTrue(m.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"));
        }

        [Test]
        public void TwoSided_HasCullOff_OneSided_HasCullBack([Values] RoSurface surface)
        {
            var ctx = Context();

            Assert.AreEqual((float)CullMode.Off, Get(ctx, Key(surface, twoSided: true)).GetFloat("_Cull"));
            Assert.AreEqual((float)CullMode.Back, Get(ctx, Key(surface, twoSided: false)).GetFloat("_Cull"));
        }

        [TestCase(RoTextureAlpha.None, RoSurface.Opaque)]
        [TestCase(RoTextureAlpha.Keyed, RoSurface.Cutout)]
        [TestCase(RoTextureAlpha.Partial, RoSurface.Blend)]
        public void SurfaceFor_DerivesTheSurfaceFromTheTexture(RoTextureAlpha alpha, RoSurface surface)
        {
            Assert.AreEqual(surface, RoMaterialFactory.SurfaceFor(alpha));
        }
    }
}
