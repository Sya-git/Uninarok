using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using Uninarok.Effects;
using Uninarok.RoImport.Core.Formats;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Source;
using Uninarok.RoImport.Editor.Effects;
using Uninarok.RoImport.Editor.Pipeline;
using RoImportApi = Uninarok.RoImport.Editor.Pipeline.RoImport;

namespace Uninarok.RoImport.Tests.Effects
{
    /// <summary>
    /// SPEC-0012 FR-2, FR-10 (PLAN-0012 4.10): one effect asset per STR. The STRs are written by GRF itself
    /// (<see cref="RoStrReader.BuildThroughGrf"/>) and the textures are the fixture BMPs of the world texture tests; no Ragnarok
    /// file byte is read here.
    /// </summary>
    public class RoStrConverterTests
    {
        const string X = "texture/effect/x.str";
        const string Y = "sprite/이팩트/y.str";
        const string TexA = "texture/effect/a.bmp";

        string _temp, _data, _ledger;
        RoOutputRoots _roots;
        AddressableAssetSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _roots = RoImportTestRoot.Create();
            _settings = AddressableAssetSettings.Create(RoImportTestRoot.Folder + "/aa", "TestAddressables", false, true);
            _temp = Path.Combine(Path.GetTempPath(), "RoStrConverterTests", Guid.NewGuid().ToString("N"));
            _data = Path.Combine(_temp, "data");
            _ledger = Path.Combine(_temp, "ledger");
            Directory.CreateDirectory(_ledger);
        }

        [TearDown]
        public void TearDown()
        {
            RoImportTestRoot.Delete();
            if (Directory.Exists(_temp)) Directory.Delete(_temp, true);
        }

        static string Fixture(string name) => Path.Combine(RoFolderSourceTests.FixtureRoot, "texture", name);

        void PutFile(string relative, byte[] bytes)
        {
            string full = Path.Combine(_data, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllBytes(full, bytes);
        }

        void PutTexture(string relative, string fixture) => PutFile(relative, File.ReadAllBytes(Fixture(fixture)));

        void PutStr(string relative, params string[][] layerTextures) =>
            PutFile(relative, RoStrReader.BuildThroughGrf(layerTextures, 60, 40));

        RoReport Run(bool force = false) => RoImportApi.EffectFolder(null, new RoImportOptions
        {
            Source = new RoFolderSource(_data), Roots = _roots, LedgerFolder = _ledger, ShowProgress = false,
            CatalogAddressables = _settings, Force = force,
        });

        string AssetPath(string str) => RoEffectOutputPaths.Asset(_roots, RoPath.Parse(str));

        EffectAsset Load(string str) => AssetDatabase.LoadAssetAtPath<EffectAsset>(AssetPath(str));

        [Test]
        public void OneAssetPerStr_AtItsPath_WithTheCompiledData()
        {
            PutTexture(TexA, "a.bmp");
            PutStr(X, new[] { "a.bmp" }, new[] { "a.bmp", "a.bmp" });
            PutStr(Y, new[] { "a.bmp" });

            var report = Run();

            Assert.That(report.Created, Is.EqualTo(2), report.Summarize());
            Assert.That(report.Failed, Is.EqualTo(0));
            Assert.That(AssetPath(X), Is.EqualTo(_roots.Art.TrimEnd('/') + "/effect/texture/effect/x.str.asset"));
            var x = Load(X);
            Assert.That(x, Is.Not.Null);
            Assert.That(x.Path, Is.EqualTo(X), "the canonical catalog key");
            Assert.That(x.Kind, Is.EqualTo(EffectKind.Str));
            Assert.That(x.Fps, Is.EqualTo(60f));
            Assert.That(x.FrameCount, Is.EqualTo(40));
            Assert.That(x.Length, Is.EqualTo(40f / 60f).Within(1e-6f));
            Assert.That(x.Layers.Count(l => l.KeyCount > 0), Is.EqualTo(2), "GRF's file may add an empty layer 0, as the data's files have");
            Assert.That(x.Keys.Length, Is.EqualTo(2));
            Assert.That(x.TextureNames, Is.EqualTo(new[] { "a.bmp", "a.bmp", "a.bmp" }));
            Assert.That(x.Textures.Length, Is.EqualTo(3));
            Assert.That(x.Textures.All(t => t != null), Is.True);
            Assert.That(Load(Y), Is.Not.Null);
            Assert.That(Load(Y).Path, Is.EqualTo(Y));
        }

        [Test]
        public void ATextureSharedByTwoStrs_IsConvertedOnce()
        {
            PutTexture(TexA, "a.bmp");
            PutStr(X, new[] { "a.bmp" });
            PutStr("texture/effect/z.str", new[] { "a.bmp" });

            var report = Run();

            Assert.That(report.Failed, Is.EqualTo(0), report.Summarize());
            string effectFolder = RoEffectOutputPaths.Root(_roots);
            var textures = AssetDatabase.FindAssets("t:Texture2D", new[] { effectFolder });
            Assert.That(textures.Length, Is.EqualTo(1), "one PNG for the texture both STRs name");
            Assert.That(Load(X).Textures[0], Is.SameAs(Load("texture/effect/z.str").Textures[0]));
            Assert.That(Load(X).Textures[0], Is.Not.Null);
        }

        [Test]
        public void SecondRun_IsSkipped_AndAForcedRunKeepsTheGuids()
        {
            PutTexture(TexA, "a.bmp");
            PutStr(X, new[] { "a.bmp" });
            PutStr(Y, new[] { "a.bmp" });
            Run();
            string guid = AssetDatabase.AssetPathToGUID(AssetPath(X));

            var again = Run();
            Assert.That(again.Skipped, Is.EqualTo(2), again.Summarize());
            Assert.That(again.Created + again.Updated, Is.EqualTo(0));

            var forced = Run(force: true);
            Assert.That(forced.Created, Is.EqualTo(0), forced.Summarize());
            Assert.That(AssetDatabase.AssetPathToGUID(AssetPath(X)), Is.EqualTo(guid));
            Assert.That(Load(X), Is.Not.Null);
        }

        [Test]
        public void AChangedTexture_ConvertsItsStrAgain()
        {
            PutTexture(TexA, "a.bmp");
            PutStr(X, new[] { "a.bmp" });
            PutStr(Y, new[] { "other.bmp" });
            PutTexture("texture/effect/other.bmp", "a.bmp");
            Run();

            PutTexture(TexA, "KEY8.BMP");
            var report = Run();

            Assert.That(report.Updated, Is.EqualTo(1), report.Summarize());
            Assert.That(report.Skipped, Is.EqualTo(1), "the STR of the untouched texture stays skipped");
        }

        [Test]
        public void AMissingTexture_IsOneWarningAndThePlaceholder_AndAppearingLaterConvertsTheStrAgain()
        {
            PutStr(X, new[] { "a.bmp" });

            var first = Run();
            Assert.That(first.Created, Is.EqualTo(1), first.Summarize());
            Assert.That(first.Issues.Count(i => i.Code == RoIssueCode.TextureMissing), Is.EqualTo(1));
            Assert.That(Load(X).Textures[0], Is.Not.Null, "the placeholder fills the slot");

            Assert.That(Run().Skipped, Is.EqualTo(1), "still missing: nothing changed");

            PutTexture(TexA, "a.bmp");
            var later = Run();
            Assert.That(later.Updated, Is.EqualTo(1), later.Summarize());
            Assert.That(later.Issues.Count(i => i.Code == RoIssueCode.TextureMissing), Is.EqualTo(0));
        }

        [Test]
        public void ACorruptStr_IsOneIssueAndNoOutput()
        {
            var junk = new byte[64];
            new System.Random(7).NextBytes(junk);
            PutFile(X, junk);

            var report = Run();

            Assert.That(report.Failed, Is.EqualTo(1), report.Summarize());
            Assert.That(report.Issues.Count(), Is.EqualTo(1), report.Summarize());
            Assert.That(report.Issues.Single().Code, Is.EqualTo(RoIssueCode.CorruptFile));
            Assert.That(Load(X), Is.Null);
        }

        [Test]
        public void AnUnsupportedStrVersion_IsOneIssueAndNoOutput()
        {
            var bytes = RoStrReader.BuildThroughGrf(new[] { new[] { "a.bmp" } }, 60, 40);
            PutFile(X, RoStrReader.RewriteVersionThroughGrf(bytes, 0x95, 0));

            var report = Run();

            Assert.That(report.Failed, Is.EqualTo(1), report.Summarize());
            Assert.That(report.Issues.Count(), Is.EqualTo(1), report.Summarize());
            Assert.That(report.Issues.Single().Code, Is.EqualTo(RoIssueCode.UnsupportedStrVersion));
            Assert.That(Load(X), Is.Null);
        }

        [Test]
        public void TheStrJobKind_IsRegistered_AndHandlesOnlyStr()
        {
            var converter = RoConverterRegistry.For(RoJobKind.Effect);
            Assert.That(converter, Is.TypeOf<RoStrConverter>());
            Assert.That(converter.Handles(RoPath.Parse(X)), Is.True);
            Assert.That(converter.Handles(RoPath.Parse(TexA)), Is.False);
        }
    }
}
