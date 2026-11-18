using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Uninarok.RoImport.Core.Materials;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Source;
using Uninarok.RoImport.Editor.Materials;
using Uninarok.RoImport.Editor.Pipeline;

namespace Uninarok.RoImport.Tests
{
    // SPEC-0002 FR-8, FR-10 (a missing texture shows a visible placeholder, not a crash), FR-16 (identical bytes on rerun).
    // Plan 4.12. TASKS T5.4.
    public class RoPlaceholdersTests
    {
        RoOutputRoots _roots;
        string _ledgerFolder;

        [SetUp]
        public void SetUp()
        {
            _roots = RoImportTestRoot.Create();
            _ledgerFolder = Path.Combine(Path.GetTempPath(), "RoImportTests", Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            RoImportTestRoot.Delete();
            if (Directory.Exists(_ledgerFolder)) Directory.Delete(_ledgerFolder, recursive: true);
        }

        RoImportContext Context() => new RoImportContext(new RoFolderSource(RoFolderSourceTests.FixtureRoot), _roots, _ledgerFolder);

        string TexturePath => RoOutputPaths.Placeholder(_roots, RoPlaceholders.TextureName);

        [Test]
        public void IsNotCreatedBeforeItIsNeeded()
        {
            Context();
            Assert.IsFalse(File.Exists(RoImportTestRoot.FullPath(TexturePath)));
        }

        [Test]
        public void TwoRequests_CreateOneAsset()
        {
            var ctx = Context();

            var first = RoPlaceholders.MissingTexture(ctx);
            var second = RoPlaceholders.MissingTexture(ctx);

            Assert.AreEqual(RoStatus.Created, first.Status);
            Assert.IsTrue(first.Value == second.Value);
            Assert.AreEqual(1, Directory.GetFiles(RoImportTestRoot.FullPath(_roots.Art + "/_placeholders"), "*.png").Length);
            Assert.AreEqual(TexturePath, AssetDatabase.GetAssetPath(first.Value));
        }

        [Test]
        public void Rerun_IsSkipped_WithIdenticalBytes_AndNoRewrite()
        {
            RoPlaceholders.MissingTexture(Context());
            string disk = RoImportTestRoot.FullPath(TexturePath);
            byte[] bytes = File.ReadAllBytes(disk);
            var old = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(disk, old);

            var again = RoPlaceholders.MissingTexture(Context());

            Assert.AreEqual(RoStatus.Skipped, again.Status);
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(disk));
            Assert.AreEqual(old, File.GetLastWriteTimeUtc(disk));
        }

        [Test]
        public void Image_IsAMagentaAndBlackChecker()
        {
            var image = RoPlaceholders.CheckerImage();

            Assert.AreEqual(16, image.Width);
            Assert.AreEqual(16, image.Height);
            Assert.AreEqual(new Color32(255, 0, 255, 255), image.GetPixel(0, 0));
            Assert.AreEqual(new Color32(0, 0, 0, 255), image.GetPixel(4, 0));
            Assert.AreEqual(new Color32(0, 0, 0, 255), image.GetPixel(0, 4));
            Assert.AreEqual(new Color32(255, 0, 255, 255), image.GetPixel(4, 4));
        }

        [Test]
        public void MissingMaterial_ShowsTheChecker_AndIsShared()
        {
            var ctx = Context();

            var material = RoPlaceholders.MissingMaterial(ctx);

            Assert.AreNotEqual(RoStatus.Failed, material.Status);
            Assert.IsTrue(material.Value.GetTexture("_BaseMap") == RoPlaceholders.MissingTexture(ctx).Value);
            Assert.IsTrue(material.Value == RoPlaceholders.MissingMaterial(ctx).Value);
        }

        [Test]
        public void MaterialOfAMissingTexture_UsesThePlaceholder_WithOneWarning()
        {
            var ctx = Context();
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[RoImport\] TextureMissing texture/nope\.bmp \(referenced by model/a\.rsm\)"));

            var result = ctx.Material(new RoMaterialKey(RoPath.Parse("texture/nope.bmp"), RoSurface.Cutout, false), "model/a.rsm");
            ctx.Material(new RoMaterialKey(RoPath.Parse("texture/nope.bmp"), RoSurface.Opaque, true), "model/b.rsm");

            Assert.AreNotEqual(RoStatus.Failed, result.Status, "the model still gets a material");
            Assert.IsTrue(result.Value.GetTexture("_BaseMap") == RoPlaceholders.MissingTexture(ctx).Value);
            Assert.AreEqual(RoOutputPaths.Material(_roots, new RoMaterialKey(RoPath.Parse("texture/nope.bmp"), RoSurface.Cutout, false)),
                AssetDatabase.GetAssetPath(result.Value), "the material keeps its own path, so it is fixed in place when the texture appears");
            Assert.AreEqual("", ctx.Ledger(RoMaterialFactory.Category).Find(RoPath.Parse("texture/nope.bmp__cutout")).SourceHash,
                "recorded without a hash: stale once the real texture exists");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Placeholder_IsLedgerOwned()
        {
            var ctx = Context();
            RoPlaceholders.MissingTexture(ctx);

            Assert.AreEqual("_placeholders/missing_texture", ctx.Ledger(RoPlaceholders.Category).OwnerOf(TexturePath));
        }
    }
}
