using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using Uninarok.Effects;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Source;
using Uninarok.RoImport.Editor.Effects;
using Uninarok.RoImport.Editor.Pipeline;
using Uninarok.RoImport.Editor.Ui;
using RoImportApi = Uninarok.RoImport.Editor.Pipeline.RoImport;

namespace Uninarok.RoImport.Tests.Effects
{
    /// <summary>
    /// SPEC-0012 FR-2, FR-5, FR-10 (PLAN-0012 4.10): the whole <c>texture/effect/</c> STR folder imports into a throwaway root,
    /// one asset per STR, with no failure; two reference textures (a pink and a black background, str-reference.md section 10,
    /// "Reference textures for T3.12") come in with the background transparent (checked on the converted PNG); a second run
    /// converts nothing. Converts every effect texture the STRs use (about 18,000), so it is slow. No Ragnarok file byte is read
    /// here: the source is the GRF-backed <see cref="RoFolderSource"/>.
    /// </summary>
    [Category("NeedsData"), Category("Slow")]
    public class RoEffectImportReferenceTests
    {
        // evidence/colour-key-survey.csv: 24-bit BMPs, each used by one STR; corners ff00ff / 000000.
        const string PinkTexture = "texture/effect/a01.bmp";
        const string BlackTexture = "texture/effect/4ig_judge/judge_cast/jud_shine_1.bmp";

        // STRs of the data that are known to be corrupt (none: evidence/str-survey.txt has all 2,995 at version 0x94).
        static readonly string[] KnownCorrupt = { };

        static int _strCount;
        static RoReport _first, _second;
        static RoOutputRoots _roots;
        static string _ledger;
        static TimeSpan _firstTime, _secondTime;

        [OneTimeSetUp]
        public void ImportTwice()
        {
            string root = RoSourceLocation.Resolve();
            if (!Directory.Exists(root)) Assert.Ignore($"source data folder not found: {root}");
            var source = new RoFolderSource(root);
            var jobs = RoImportJobs.Expand("texture/effect/", RoJobKind.Effect, source).Jobs;
            _strCount = jobs.Count;
            if (_strCount == 0) Assert.Ignore("no STR under texture/effect/ in the source data");

            _roots = RoImportTestRoot.Create();
            var settings = AddressableAssetSettings.Create(RoImportTestRoot.Folder + "/aa", "TestAddressables", false, true);
            _ledger = Path.Combine(Path.GetTempPath(), "RoEffectImportReferenceTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_ledger);
            RoImportOptions Options() => new RoImportOptions
            {
                Source = source, Roots = _roots, LedgerFolder = _ledger, ShowProgress = false, CatalogAddressables = settings,
            };

            var watch = Stopwatch.StartNew();
            _first = RoImportApi.EffectFolder("texture/effect/", Options());
            _firstTime = watch.Elapsed;
            watch.Restart();
            _second = RoImportApi.EffectFolder("texture/effect/", Options());
            _secondTime = watch.Elapsed;
            TestContext.Progress.WriteLine($"[T3.12] {_strCount} STRs; first run {_firstTime}; second run {_secondTime}");
        }

        [OneTimeTearDown]
        public void Cleanup()
        {
            RoImportTestRoot.Delete();
            if (_ledger != null && Directory.Exists(_ledger)) Directory.Delete(_ledger, recursive: true);
        }

        static string Describe(RoReport r) => r.Summarize() + "\n" + string.Join("\n", r.Failures.Take(50).Select(f => f.ToString()));

        [Test]
        public void WholeFolder_GivesOneAssetPerStr_AndAReportEntryPerFile()
        {
            TestContext.Out.WriteLine(_first.Summarize());
            var unexpected = _first.Failures.Where(f => !KnownCorrupt.Contains(f.Item, StringComparer.OrdinalIgnoreCase)).ToList();
            Assert.That(unexpected, Is.Empty, "unexpected failures:\n" + string.Join("\n", unexpected.Take(50).Select(f => f.ToString())));
            Assert.That(_first.Created + _first.Updated + _first.Skipped + _first.Failed, Is.EqualTo(_strCount), "one report entry per STR");
            Assert.That(_first.Failed, Is.EqualTo(KnownCorrupt.Length), Describe(_first));
            Assert.That(_first.Created, Is.EqualTo(_strCount - KnownCorrupt.Length), Describe(_first));

            string folder = RoEffectOutputPaths.Root(_roots);
            var assets = AssetDatabase.FindAssets("t:EffectAsset", new[] { folder });
            Assert.That(assets.Length, Is.EqualTo(_strCount - KnownCorrupt.Length), "one asset per STR (FR-2)");
        }

        [Test]
        public void ThePinkBackgroundTexture_IsTransparent()
        {
            AssertBackgroundTransparent(PinkTexture);
        }

        [Test]
        public void TheBlackBackgroundTexture_IsTransparent()
        {
            AssertBackgroundTransparent(BlackTexture);
        }

        static void AssertBackgroundTransparent(string texture)
        {
            string asset = RoEffectOutputPaths.Texture(_roots, RoPath.Parse(texture));
            string disk = RoImportTestRoot.FullPath(asset);
            Assert.That(File.Exists(disk), Is.True, "converted PNG missing: " + asset);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(ImageConversion.LoadImage(tex, File.ReadAllBytes(disk)), Is.True);
                int w = tex.width, h = tex.height;
                Color32[] px = tex.GetPixels32();
                Color32 tl = px[(h - 1) * w], tr = px[(h - 1) * w + w - 1], bl = px[0], br = px[w - 1];
                Assert.That(new[] { tl.a, tr.a, bl.a, br.a }, Is.All.EqualTo((byte)0), texture + ": corners are transparent");
                Assert.That(px.Count(p => p.a == 0), Is.GreaterThan(0));
                Assert.That(px.Count(p => p.a == 255), Is.GreaterThan(0), texture + ": the picture itself stays opaque");
            }
            finally { UnityEngine.Object.DestroyImmediate(tex); }
        }

        [Test]
        public void ASecondRun_ImportsNothing()
        {
            TestContext.Out.WriteLine(_second.Summarize());
            Assert.That(_second.Created + _second.Updated, Is.EqualTo(0), Describe(_second));
            Assert.That(_second.Failed, Is.EqualTo(KnownCorrupt.Length), Describe(_second));
            Assert.That(_second.Skipped, Is.EqualTo(_strCount - KnownCorrupt.Length), Describe(_second));
        }
    }
}
