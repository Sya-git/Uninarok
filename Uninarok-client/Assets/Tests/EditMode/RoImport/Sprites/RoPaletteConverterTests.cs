using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Source;
using Uninarok.RoImport.Editor.Pipeline;
using Uninarok.RoImport.Editor.Sprites;
using Uninarok.RoImport.Editor.Writing;

namespace Uninarok.RoImport.Tests.Sprites
{
    // TASKS-0007 T5.3 (FR-7, plan 4.4, 4.7): a PAL becomes a 256 × 1 point-filtered texture with entry 0 transparent.
    public class RoPaletteConverterTests
    {
        const string Body = "palette/body/novice_male_1.pal";

        RoOutputRoots _roots;
        string _temp, _data;

        [SetUp]
        public void SetUp()
        {
            _roots = RoImportTestRoot.Create();
            _temp = Path.Combine(Path.GetTempPath(), "RoImportTests", Guid.NewGuid().ToString("N"));
            _data = Path.Combine(_temp, "data");
            Write(Body, SpriteFileWriter.Pal(SpriteTestData.Palette(7)));
            Write("palette/bad/short.pal", new byte[100]);
        }

        [TearDown]
        public void TearDown()
        {
            RoImportTestRoot.Delete();
            if (Directory.Exists(_temp)) Directory.Delete(_temp, recursive: true);
        }

        void Write(string relative, byte[] bytes)
        {
            string path = Path.Combine(_data, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, bytes);
        }

        RoImportContext Context() => new RoImportContext(new RoFolderSource(_data), _roots, Path.Combine(_temp, "ledger"));

        [Test]
        public void APalette_Is256By1_PointClampNoMipmapsUncompressed_WithEntry0Transparent()
        {
            var result = new RoPaletteConverter().Convert(Context(), RoPath.Parse(Body));

            Assert.AreEqual(RoStatus.Created, result.Status, result.Failure?.ToString());
            var texture = (Texture2D)result.Value;
            string path = RoSpriteOutputPaths.Palette(_roots, RoPath.Parse(Body));
            Assert.AreEqual(path, AssetDatabase.GetAssetPath(texture));
            StringAssert.EndsWith("/Art/palette/body/novice_male_1.png", path);
            Assert.AreEqual((256, 1), (texture.width, texture.height));
            Assert.AreEqual((FilterMode.Point, TextureWrapMode.Clamp, 1), (texture.filterMode, texture.wrapMode, texture.mipmapCount));
            Assert.AreEqual(TextureFormat.RGBA32, texture.format);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.IsTrue(importer.sRGBTexture);

            Assert.IsTrue(new RoPngCodec().TryDecode(File.ReadAllBytes(RoImportTestRoot.FullPath(path)), out var image));
            Assert.AreEqual(0, image.GetPixel(0, 0).a, "entry 0 is transparent");
            var expected = SpriteTestData.Palette(7).Colors[200];
            var actual = image.GetPixel(200, 0);
            Assert.AreEqual((expected.r, expected.g, expected.b, (byte)255), (actual.r, actual.g, actual.b, actual.a));
        }

        [Test]
        public void ASecondRun_IsSkipped()
        {
            var first = Context();
            new RoPaletteConverter().Convert(first, RoPath.Parse(Body));
            first.SaveLedgers();

            Assert.AreEqual(RoStatus.Skipped, new RoPaletteConverter().Convert(Context(), RoPath.Parse(Body)).Status);
        }

        [Test]
        public void AShortFile_IsOneIssue_AndNoOutput()
        {
            var ctx = Context();
            var result = new RoPaletteConverter().Convert(ctx, RoPath.Parse("palette/bad/short.pal"));

            Assert.AreEqual(RoStatus.Failed, result.Status);
            Assert.AreEqual(RoIssueCode.CorruptFile, result.Failure.Code);
            Assert.IsFalse(File.Exists(RoImportTestRoot.FullPath(RoSpriteOutputPaths.Palette(_roots, RoPath.Parse("palette/bad/short.pal")))));
        }

        [Test]
        public void TheBatch_ConvertsSpritesAndPalettes_ByKind()
        {
            Write("sprite/item/apple.spr", SpriteFileWriter.Spr(SpriteTestData.Static().spr));
            Write("sprite/item/apple.act", SpriteFileWriter.Act(SpriteTestData.Static().act));
            var source = new RoFolderSource(_data);
            var jobs = Uninarok.RoImport.Editor.Ui.RoImportJobs.Expand("sprite/\npalette/body/", RoJobKind.Sprite, source).Jobs;
            jobs.AddRange(Uninarok.RoImport.Editor.Ui.RoImportJobs.Expand("palette/body/", RoJobKind.Palette, source).Jobs);
            Assert.AreEqual(2, jobs.Count, "one ACT and one PAL; the SPR is not a job");

            var report = RoBatch.Run(jobs, new RoImportOptions { Roots = _roots, Source = source, LedgerFolder = Path.Combine(_temp, "ledger"), ShowProgress = false });

            Assert.AreEqual((2, 0), (report.Created, report.Failed), report.Summarize());
        }
    }
}
