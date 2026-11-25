using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Uninarok.RoImport.Core.Effects;
using Uninarok.RoImport.Core.Imaging;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Source;
using Uninarok.RoImport.Editor.Effects;
using Uninarok.RoImport.Editor.Pipeline;
using Uninarok.RoImport.Editor.Writing;

namespace Uninarok.RoImport.Tests.Effects
{
    /// <summary>
    /// SPEC-0012 FR-2 (PLAN-0012 4.3, D7): effect textures keep their extension, so <c>pok1.tga</c> and <c>pok1.bmp</c> are two
    /// outputs with no collision; the colour key is stored in the userData and a changed key re-converts only its texture.
    /// The temp source copies the fixture BMP and TGA of the world texture tests.
    /// </summary>
    public class RoEffectTextureConverterTests
    {
        const string Bmp = "texture/effect/pok1.bmp";
        const string Tga = "texture/effect/pok1.tga";

        RoOutputRoots _roots;
        string _temp;

        [SetUp]
        public void SetUp()
        {
            _roots = RoImportTestRoot.Create();
            _temp = Path.Combine(Path.GetTempPath(), "RoImportTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_temp);
        }

        [TearDown]
        public void TearDown()
        {
            RoImportTestRoot.Delete();
            if (Directory.Exists(_temp)) Directory.Delete(_temp, recursive: true);
        }

        RoFolderSource Source()
        {
            string root = Path.Combine(_temp, "data");
            string fixtures = Path.Combine(RoFolderSourceTests.FixtureRoot, "texture");
            Copy(Path.Combine(fixtures, "a.bmp"), root, Bmp);
            Copy(Path.Combine(fixtures, "alpha32.tga"), root, Tga);
            return new RoFolderSource(root);
        }

        static void Copy(string from, string root, string to)
        {
            string disk = Path.Combine(root, to.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(disk));
            File.Copy(from, disk);
        }

        RoImportContext Context(IRoSource source, RoEffectKeyRules rules) =>
            new RoImportContext(source, _roots, Path.Combine(_temp, "ledger")) { EffectKeyRules = rules };

        static RoEffectKeyRules Rules(RoEffectKey opaque, RoEffectKey withAlpha, params (string, RoEffectKey)[] overrides) =>
            new RoEffectKeyRules
            {
                DefaultOpaque = opaque,
                DefaultWithAlpha = withAlpha,
                PinkTolerance = 0,
                Overrides = overrides.Select(o => (RoPath.Parse(o.Item1), o.Item2)).ToArray(),
            };

        string Output(string source) => RoEffectOutputPaths.Texture(_roots, RoPath.Parse(source));

        static RoResult<Texture2D> Convert(RoImportContext ctx, string source) => ctx.EffectTexture(RoPath.Parse(source), "effect/x.str");

        [Test]
        public void BmpAndTgaOfOneName_GiveTwoOutputs_WithTheExtensionKept_AndNoCollision()
        {
            var ctx = Context(Source(), Rules(RoEffectKey.Pink, RoEffectKey.None));

            Assert.AreEqual(RoStatus.Created, Convert(ctx, Tga).Status);
            Assert.AreEqual(RoStatus.Created, Convert(ctx, Bmp).Status);

            Assert.AreEqual("Assets/__RoImportTests/Art/effect/texture/effect/pok1.tga.png", Output(Tga));
            Assert.AreEqual("Assets/__RoImportTests/Art/effect/texture/effect/pok1.bmp.png", Output(Bmp));
            Assert.IsTrue(File.Exists(RoImportTestRoot.FullPath(Output(Tga))));
            Assert.IsTrue(File.Exists(RoImportTestRoot.FullPath(Output(Bmp))));
            Assert.IsFalse(ctx.Report.Issues.Any(i => i.Code == RoIssueCode.OutputCollision));
            Assert.AreEqual(0, ctx.Report.Issues.Count());
        }

        [Test]
        public void Userdata_HoldsTheKey_AndTheSettingsAreClamp()
        {
            var ctx = Context(Source(), Rules(RoEffectKey.Pink, RoEffectKey.None));
            Convert(ctx, Bmp);
            Convert(ctx, Tga);

            var bmp = (TextureImporter)AssetImporter.GetAtPath(Output(Bmp));
            StringAssert.Contains("key=Pink", bmp.userData);
            Assert.AreEqual(TextureWrapMode.Clamp, bmp.wrapMode);
            Assert.AreEqual(RoEffectKey.Pink, RoEffectTextureConverter.KeyOf(Output(Bmp)));
            Assert.AreEqual(RoEffectKey.None, RoEffectTextureConverter.KeyOf(Output(Tga)), "a 32-bit TGA takes the with-alpha default");

            Assert.IsTrue(new RoPngCodec().TryDecode(File.ReadAllBytes(RoImportTestRoot.FullPath(Output(Bmp))), out var pixels));
            Assert.AreEqual(0, pixels.GetPixel(0, 0).a, "the pink is keyed out");
            Assert.AreEqual(255, pixels.GetPixel(1, 0).a);
        }

        [Test]
        public void SecondRun_IsSkipped()
        {
            var source = Source();
            var rules = Rules(RoEffectKey.Pink, RoEffectKey.None);
            var first = Context(source, rules);
            Convert(first, Bmp);
            Convert(first, Tga);
            first.SaveLedgers();

            var second = Context(source, rules);
            Assert.AreEqual(RoStatus.Skipped, Convert(second, Bmp).Status);
            Assert.AreEqual(RoStatus.Skipped, Convert(second, Tga).Status);
            Assert.AreEqual(RoStatus.Skipped, Convert(second, Bmp).Status, "memoised within the run");
        }

        [Test]
        public void ChangingOneTexturesKey_ConvertsOnlyThatTexture()
        {
            var source = Source();
            var first = Context(source, Rules(RoEffectKey.Pink, RoEffectKey.None));
            Convert(first, Bmp);
            Convert(first, Tga);
            first.SaveLedgers();

            var second = Context(source, Rules(RoEffectKey.Pink, RoEffectKey.None, (Bmp, RoEffectKey.None)));
            Assert.AreEqual(RoStatus.Updated, Convert(second, Bmp).Status);
            Assert.AreEqual(RoStatus.Skipped, Convert(second, Tga).Status);
            Assert.AreEqual(RoEffectKey.None, RoEffectTextureConverter.KeyOf(Output(Bmp)));
            Assert.IsTrue(new RoPngCodec().TryDecode(File.ReadAllBytes(RoImportTestRoot.FullPath(Output(Bmp))), out var pixels));
            Assert.AreEqual(255, pixels.GetPixel(0, 0).a, "no key: the pink stays");
        }

        [Test]
        public void ADefaultChange_ReconvertsOnlyTheTexturesItChanges()
        {
            var source = Source();
            var first = Context(source, Rules(RoEffectKey.Pink, RoEffectKey.None));
            Convert(first, Bmp);
            Convert(first, Tga);
            first.SaveLedgers();

            var second = Context(source, Rules(RoEffectKey.Black, RoEffectKey.None));   // the opaque BMP changes, the TGA does not
            Assert.AreEqual(RoStatus.Updated, Convert(second, Bmp).Status);
            Assert.AreEqual(RoStatus.Skipped, Convert(second, Tga).Status);
        }
    }
}
