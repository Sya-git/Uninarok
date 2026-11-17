using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Uninarok.RoImport.Core.Imaging;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Source;
using Uninarok.RoImport.Editor.Pipeline;
using Uninarok.RoImport.Editor.Writing;

namespace Uninarok.RoImport.Tests
{
    // SPEC-0002 FR-8 (transparency, usage settings, BMP/PNG/TGA, unsupported warns), FR-10 (one warning, placeholder),
    // FR-4 (mirrored paths, no collision by name), FR-5 (second run skipped). Plan 4.4, 4.5, 4.12. TASKS T6.1.
    [Ignore("pending T6.2")]
    public class RoTextureConverterTests
    {
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

        string LedgerFolder => Path.Combine(_temp, "ledger");

        RoImportContext Context(IRoSource source = null) =>
            new RoImportContext(source ?? new RoFolderSource(RoFolderSourceTests.FixtureRoot), _roots, LedgerFolder);

        string Output(string source) => RoOutputPaths.Texture(_roots, RoPath.Parse(source));

        TextureImporter Importer(string source) => (TextureImporter)AssetImporter.GetAtPath(Output(source));

        RoImage OutputPixels(string source)
        {
            Assert.IsTrue(new RoPngCodec().TryDecode(File.ReadAllBytes(RoImportTestRoot.FullPath(Output(source))), out var image));
            return image;
        }

        RoResult<Texture2D> Convert(RoImportContext ctx, string source, string referrer = null)
        {
            var result = ctx.Texture(RoPath.Parse(source), referrer);
            Assert.IsNotNull(result.Value, $"{source}: every request returns a texture, real or placeholder");
            return result;
        }

        static void AssertWorld(TextureImporter t, bool preserveCoverage)
        {
            Assert.AreEqual(TextureImporterType.Default, t.textureType);
            Assert.IsTrue(t.sRGBTexture);
            Assert.IsTrue(t.alphaIsTransparency);
            Assert.IsTrue(t.mipmapEnabled);
            Assert.AreEqual(preserveCoverage, t.mipMapsPreserveCoverage);
            Assert.AreEqual(TextureWrapMode.Repeat, t.wrapMode);
            Assert.AreEqual(FilterMode.Bilinear, t.filterMode);
            Assert.AreEqual(TextureImporterCompression.Compressed, t.textureCompression);
            Assert.IsFalse(t.isReadable);
        }

        static void AssertUi(TextureImporter t)
        {
            Assert.AreEqual(TextureImporterType.Sprite, t.textureType);
            Assert.AreEqual(SpriteImportMode.Single, t.spriteImportMode);
            Assert.IsTrue(t.sRGBTexture);
            Assert.IsTrue(t.alphaIsTransparency);
            Assert.IsFalse(t.mipmapEnabled);
            Assert.AreEqual(TextureWrapMode.Clamp, t.wrapMode);
            Assert.AreEqual(FilterMode.Point, t.filterMode);
            Assert.AreEqual(TextureImporterCompression.Uncompressed, t.textureCompression);
            Assert.IsFalse(t.isReadable);
        }

        // ---- Supported formats ----

        [Test]
        public void Bmp_BecomesAPngAtTheMirroredPath_WithWorldSettings_AndKeyedBledMagenta()
        {
            var result = Convert(Context(), "texture/a.bmp");

            Assert.AreEqual(RoStatus.Created, result.Status);
            Assert.AreEqual("Assets/__RoImportTests/Art/texture/a.png", AssetDatabase.GetAssetPath(result.Value));
            AssertWorld(Importer("texture/a.bmp"), preserveCoverage: true);

            var pixels = OutputPixels("texture/a.bmp");
            var keyed = pixels.GetPixel(0, 0);
            Assert.AreEqual(0, keyed.a, "the magenta is transparent");
            Assert.IsFalse(keyed.r > 200 && keyed.g < 50 && keyed.b > 200, $"and bled, not magenta: {keyed}");
            Assert.AreEqual(new Color32(255, 0, 0, 255), pixels.GetPixel(1, 0), "visible pixels unchanged");
        }

        [Test]
        public void PalettedBmp_Converts()
        {
            Assert.AreEqual(RoStatus.Created, Convert(Context(), "texture/KEY8.BMP").Status);
            Assert.AreEqual(255, OutputPixels("texture/key8.bmp").GetPixel(1, 1).a);
        }

        [Test]
        public void Tga_WithRealAlpha_ConvertsWithoutPreservedCoverage()
        {
            Assert.AreEqual(RoStatus.Created, Convert(Context(), "texture/alpha32.tga").Status);

            AssertWorld(Importer("texture/alpha32.tga"), preserveCoverage: false);
            Assert.AreEqual(128, OutputPixels("texture/alpha32.tga").GetPixel(1, 0).a);
        }

        [Test]
        public void Png_Converts()
        {
            Assert.AreEqual(RoStatus.Created, Convert(Context(), "texture/rgba.png").Status);

            AssertWorld(Importer("texture/rgba.png"), preserveCoverage: false);
            Assert.AreEqual(64, OutputPixels("texture/rgba.png").GetPixel(1, 0).a);
        }

        [Test]
        public void UiTexture_GetsUiSettings()
        {
            Assert.AreEqual(RoStatus.Created, Convert(Context(), "texture/유저인터페이스/button.bmp").Status);

            AssertUi(Importer("texture/유저인터페이스/button.bmp"));
        }

        [Test]
        public void KoreanFolderTexture_Converts()
        {
            Assert.AreEqual(RoStatus.Created, Convert(Context(), "texture/나무잡초꽃/leaf.bmp").Status);
            Assert.IsTrue(File.Exists(RoImportTestRoot.FullPath(Output("texture/나무잡초꽃/leaf.bmp"))));
        }

        [Test]
        public void Conversion_IsRecordedInTheTextureLedger()
        {
            var ctx = Context();
            Convert(ctx, "texture/a.bmp");
            ctx.Source.TryGetFile(RoPath.Parse("texture/a.bmp"), out var file);

            var entry = ctx.Ledger(RoTextureConverter.Category).Find(RoPath.Parse("texture/a.bmp"));

            Assert.AreEqual(file.ContentHash(), entry.SourceHash);
            Assert.AreEqual(RoTextureConverter.Rules, entry.Rules);
            CollectionAssert.AreEqual(new[] { Output("texture/a.bmp") }, entry.Outputs);
        }

        // ---- Repeat runs ----

        [Test]
        public void SecondRun_IsSkipped_ForEachFormat([Values("texture/a.bmp", "texture/alpha32.tga", "texture/rgba.png", "texture/유저인터페이스/button.bmp")] string source)
        {
            var first = Context();
            Convert(first, source);
            first.SaveLedgers();

            Assert.AreEqual(RoStatus.Skipped, Convert(Context(), source).Status);
        }

        [Test]
        public void SecondRequestInOneRun_IsFree_AndTheSameAsset()
        {
            var ctx = Context();
            var a = Convert(ctx, "texture/a.bmp");
            var b = Convert(ctx, "texture/a.bmp");

            Assert.AreEqual(RoStatus.Skipped, b.Status);
            Assert.IsTrue(a.Value == b.Value);
        }

        // ---- Problems ----

        [Test]
        public void UnsupportedFormat_GivesOneWarningNamingFileAndReferrer_AndThePlaceholder()
        {
            var ctx = Context();
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[RoImport\] UnsupportedTextureFormat texture/unsupported\.dds \(referenced by model/a\.rsm\)"));

            var result = Convert(ctx, "texture/unsupported.dds", "model/a.rsm");
            Convert(ctx, "texture/unsupported.dds", "model/b.rsm");

            Assert.AreEqual(RoStatus.Failed, result.Status);
            Assert.AreEqual(RoIssueCode.UnsupportedTextureFormat, result.Failure.Code);
            Assert.IsTrue(result.Value == RoPlaceholders.MissingTexture(ctx).Value);
            Assert.AreEqual(1, ctx.Report.Issues.Count(i => i.Code == RoIssueCode.UnsupportedTextureFormat));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void MissingTexture_GivesOneWarningNamingTheReferrer_AndThePlaceholder()
        {
            var ctx = Context();
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[RoImport\] TextureMissing texture/nope\.bmp \(referenced by prontera\.rsw\)"));

            var result = Convert(ctx, "texture/nope.bmp", "prontera.rsw");

            Assert.AreEqual(RoStatus.Failed, result.Status);
            Assert.AreEqual(RoIssueCode.TextureMissing, result.Failure.Code);
            Assert.IsTrue(result.Value == RoPlaceholders.MissingTexture(ctx).Value);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void CorruptBmp_GivesOneCorruptFileWarning_AndThePlaceholder()
        {
            var ctx = Context();
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[RoImport\] CorruptFile texture/corrupt\.bmp"));

            var result = Convert(ctx, "texture/corrupt.bmp");

            Assert.AreEqual(RoStatus.Failed, result.Status);
            Assert.AreEqual(RoIssueCode.CorruptFile, result.Failure.Code);
            Assert.IsTrue(result.Value == RoPlaceholders.MissingTexture(ctx).Value);
            Assert.IsFalse(File.Exists(RoImportTestRoot.FullPath(Output("texture/corrupt.bmp"))), "no output for a corrupt source");
            LogAssert.NoUnexpectedReceived();
        }

        // ---- Paths ----

        RoFolderSource TempSource(params string[] copies)
        {
            string root = Path.Combine(_temp, "data");
            byte[] bmp = File.ReadAllBytes(Path.Combine(RoFolderSourceTests.FixtureRoot, "texture", "a.bmp"));
            byte[] tga = File.ReadAllBytes(Path.Combine(RoFolderSourceTests.FixtureRoot, "texture", "alpha32.tga"));
            foreach (string copy in copies)
            {
                string disk = Path.Combine(root, copy.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(disk));
                File.WriteAllBytes(disk, copy.EndsWith(".tga") ? tga : bmp);
            }
            return new RoFolderSource(root);
        }

        [Test]
        public void SameNameInTwoFolders_GivesTwoOutputs_AndNoCollision()
        {
            var ctx = Context(TempSource("texture/x/a.bmp", "texture/y/a.bmp"));

            Assert.AreEqual(RoStatus.Created, Convert(ctx, "texture/x/a.bmp").Status);
            Assert.AreEqual(RoStatus.Created, Convert(ctx, "texture/y/a.bmp").Status);

            Assert.IsTrue(File.Exists(RoImportTestRoot.FullPath(Output("texture/x/a.bmp"))));
            Assert.IsTrue(File.Exists(RoImportTestRoot.FullPath(Output("texture/y/a.bmp"))));
            Assert.IsFalse(ctx.Report.Issues.Any(i => i.Code == RoIssueCode.OutputCollision));
        }

        [Test]
        public void TwoSourcesForOneOutput_TheSecondIsSkippedWithAnOutputCollision()
        {
            var ctx = Context(TempSource("texture/a.bmp", "texture/a.tga"));
            Convert(ctx, "texture/a.bmp");
            byte[] first = File.ReadAllBytes(RoImportTestRoot.FullPath(Output("texture/a.bmp")));
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[RoImport\] OutputCollision texture/a\.tga"));

            var second = Convert(ctx, "texture/a.tga");

            Assert.AreEqual(RoStatus.Failed, second.Status);
            Assert.AreEqual(RoIssueCode.OutputCollision, second.Failure.Code);
            CollectionAssert.AreEqual(first, File.ReadAllBytes(RoImportTestRoot.FullPath(Output("texture/a.bmp"))), "the first owner's output is untouched");
            LogAssert.NoUnexpectedReceived();
        }

        // ---- Pipeline types ----

        [Test]
        public void Registry_ReturnsTheTextureConverter_WhichConvertsByPath()
        {
            var converter = RoConverterRegistry.For(RoJobKind.Texture);

            Assert.IsInstanceOf<RoTextureConverter>(converter);
            Assert.AreEqual(RoJobKind.Texture, converter.Kind);
            var result = converter.Convert(Context(), RoPath.Parse("texture/a.bmp"));
            Assert.AreEqual(RoStatus.Created, result.Status);
            Assert.IsInstanceOf<Texture2D>(result.Value);
        }
    }
}
