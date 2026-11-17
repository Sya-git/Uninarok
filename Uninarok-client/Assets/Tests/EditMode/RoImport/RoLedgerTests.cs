using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Source;
using Uninarok.RoImport.Editor.Ledger;
using Uninarok.RoImport.Editor.Writing;

namespace Uninarok.RoImport.Tests
{
    // SPEC-0002 FR-5 (skip unchanged, keep references), FR-12 (hand-made content survives), FR-16 (deterministic files).
    // Plan 4.8, 4.13, D5, D14. TASKS T4.5.
    public class RoLedgerTests
    {
        string _folder;
        RoOutputRoots _roots;
        RoAssetWriter _writer;
        RoFolderSource _source;

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "RoImportTests", Guid.NewGuid().ToString("N"), "Ledger");
            _roots = RoImportTestRoot.Create();
            _writer = new RoAssetWriter(_roots);
            _source = new RoFolderSource(RoFolderSourceTests.FixtureRoot);
        }

        [TearDown]
        public void TearDown()
        {
            RoImportTestRoot.Delete();
            string parent = Path.GetDirectoryName(_folder);
            if (Directory.Exists(parent)) Directory.Delete(parent, recursive: true);
        }

        string Hash(string canonical)
        {
            Assert.IsTrue(_source.TryGetFile(RoPath.Parse(canonical), out var file), canonical);
            return file.ContentHash();
        }

        string Output(string relative, byte value = 1)
        {
            string path = _roots.Art + "/" + relative;
            _writer.WriteBytes(path, new[] { value });
            return path;
        }

        static RoLedgerEntry Entry(string source, string hash, int rules, string[] outputs, params RoLedgerDependency[] deps) =>
            new RoLedgerEntry { Source = source, SourceHash = hash, Rules = rules, Outputs = outputs, Dependencies = deps };

        // ---- File format ----

        [Test]
        public void SavedFile_HasSortedEntries_FixedKeyOrder_LfNewlines_AndNoTimestamps()
        {
            var ledger = RoLedger.Load("texture", _folder);
            ledger.Record(Entry("texture/b.bmp", "bb", 1, new[] { "Assets/Art/Imported/texture/b.png" }));
            ledger.Record(Entry("texture/a.bmp", "aa", 2, new[] { "Assets/Art/Imported/z.png", "Assets/Art/Imported/texture/a.png" },
                new RoLedgerDependency("texture/z.bmp", "zz"), new RoLedgerDependency("texture/m.bmp", "")));
            ledger.Save();

            string text = File.ReadAllText(ledger.FilePath);

            StringAssert.DoesNotContain("\r", text);
            StringAssert.EndsWith("\n", text);
            Assert.Less(text.IndexOf("texture/a.bmp", StringComparison.Ordinal), text.IndexOf("texture/b.bmp", StringComparison.Ordinal), "entries sorted by source");
            Assert.Less(text.IndexOf("texture/a.png", StringComparison.Ordinal), text.IndexOf("/z.png", StringComparison.Ordinal), "outputs sorted");
            Assert.Less(text.IndexOf("texture/m.bmp", StringComparison.Ordinal), text.IndexOf("texture/z.bmp", StringComparison.Ordinal), "dependencies sorted");
            var keys = Regex.Matches(text, "\"(\\w+)\":").Cast<Match>().Select(m => m.Groups[1].Value).Distinct().ToArray();
            CollectionAssert.AreEqual(new[] { "entries", "Source", "SourceHash", "Rules", "Outputs", "Dependencies", "Hash" }, keys);
            Assert.IsFalse(Regex.IsMatch(text, @"\d{4}-\d{2}-\d{2}"), "no dates or timestamps");
        }

        [Test]
        public void SaveReloadSave_IsByteIdentical()
        {
            var ledger = RoLedger.Load("texture", _folder);
            ledger.Record(Entry("texture/b.bmp", "bb", 1, new[] { "x/b.png" }));
            ledger.Record(Entry("texture/a.bmp", "aa", 1, new[] { "x/a.png" }, new RoLedgerDependency("texture/c.bmp", "cc")));
            ledger.Save();
            byte[] first = File.ReadAllBytes(ledger.FilePath);

            var reloaded = RoLedger.Load("texture", _folder);
            reloaded.Save();

            CollectionAssert.AreEqual(first, File.ReadAllBytes(reloaded.FilePath));
            Assert.AreEqual(2, reloaded.Entries.Count());
            Assert.AreEqual("cc", reloaded.Find(RoPath.Parse("texture/a.bmp")).Dependencies.Single().Hash);
        }

        [Test]
        public void MissingFile_IsAnEmptyLedger()
        {
            var ledger = RoLedger.Load("model", _folder);

            CollectionAssert.IsEmpty(ledger.Entries);
            Assert.IsNull(ledger.Find(RoPath.Parse("model/a.rsm")));
            StringAssert.EndsWith("model.json", ledger.FilePath);
        }

        [TestCase("")]
        [TestCase("a/b")]
        [TestCase("..")]
        public void InvalidCategory_Throws(string category)
        {
            Assert.Throws<ArgumentException>(() => RoLedger.Load(category, _folder));
        }

        // ---- IsCurrent ----

        RoLedger CurrentLedger(out RoLedgerEntry entry)
        {
            var ledger = RoLedger.Load("texture", _folder);
            entry = Entry("texture/a.bmp", Hash("texture/a.bmp"), 3, new[] { Output("texture/a.bytes") },
                new RoLedgerDependency("texture/KEY8.BMP".ToLowerInvariant(), Hash("texture/key8.bmp")),
                new RoLedgerDependency("texture/gone.bmp", ""));
            ledger.Record(entry);
            return ledger;
        }

        [Test]
        public void IsCurrent_WhenHashRulesOutputsAndDependenciesMatch()
        {
            var ledger = CurrentLedger(out var entry);

            Assert.IsTrue(ledger.IsCurrent(RoPath.Parse("texture/a.bmp"), entry.SourceHash, 3, _source, _writer));
        }

        [Test]
        public void IsNotCurrent_WhenTheSourceHashDiffers()
        {
            var ledger = CurrentLedger(out _);
            Assert.IsFalse(ledger.IsCurrent(RoPath.Parse("texture/a.bmp"), "different", 3, _source, _writer));
        }

        [Test]
        public void IsNotCurrent_WhenTheRulesVersionDiffers()
        {
            var ledger = CurrentLedger(out var entry);
            Assert.IsFalse(ledger.IsCurrent(RoPath.Parse("texture/a.bmp"), entry.SourceHash, 4, _source, _writer));
        }

        [Test]
        public void IsNotCurrent_WhenAnOutputIsMissing()
        {
            var ledger = CurrentLedger(out var entry);
            File.Delete(RoImportTestRoot.FullPath(entry.Outputs[0]));

            Assert.IsFalse(ledger.IsCurrent(RoPath.Parse("texture/a.bmp"), entry.SourceHash, 3, _source, _writer));
        }

        [Test]
        public void IsNotCurrent_WhenADependencyChanged()
        {
            var ledger = RoLedger.Load("texture", _folder);
            string hash = Hash("texture/a.bmp");
            ledger.Record(Entry("texture/a.bmp", hash, 1, new[] { Output("texture/a.bytes") },
                new RoLedgerDependency("texture/key8.bmp", "an older hash")));

            Assert.IsFalse(ledger.IsCurrent(RoPath.Parse("texture/a.bmp"), hash, 1, _source, _writer));
        }

        [Test]
        public void IsNotCurrent_WhenAMissingDependencyAppears_OrAPresentOneDisappears()
        {
            var ledger = RoLedger.Load("texture", _folder);
            string hash = Hash("texture/a.bmp");
            string output = Output("texture/a.bytes");

            // Recorded as missing (empty hash, plan 4.12), but the file exists now: stale.
            ledger.Record(Entry("texture/a.bmp", hash, 1, new[] { output }, new RoLedgerDependency("texture/key8.bmp", "")));
            Assert.IsFalse(ledger.IsCurrent(RoPath.Parse("texture/a.bmp"), hash, 1, _source, _writer));

            // Recorded with a hash, but the file is gone now: stale.
            ledger.Record(Entry("texture/a.bmp", hash, 1, new[] { output }, new RoLedgerDependency("texture/gone.bmp", "abc")));
            Assert.IsFalse(ledger.IsCurrent(RoPath.Parse("texture/a.bmp"), hash, 1, _source, _writer));
        }

        [Test]
        public void IsNotCurrent_WithoutAnEntry()
        {
            var ledger = RoLedger.Load("texture", _folder);
            Assert.IsFalse(ledger.IsCurrent(RoPath.Parse("texture/a.bmp"), Hash("texture/a.bmp"), 1, _source, _writer));
        }

        // ---- Ownership ----

        [Test]
        public void Record_OfAnOutputOwnedByAnotherSource_ReportsOutputCollision_AndIsRefused()
        {
            var ledger = RoLedger.Load("texture", _folder);
            var report = new RoReport();
            ledger.Record(Entry("texture/a.bmp", "aa", 1, new[] { "Assets/Art/Imported/texture/a.png" }), report);
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[RoImport\] OutputCollision texture/a\.tga \(referenced by texture/a\.bmp\)"));

            bool recorded = ledger.Record(Entry("texture/a.tga", "tt", 1, new[] { "Assets/Art/Imported/texture/a.png" }), report);

            Assert.IsFalse(recorded);
            Assert.AreEqual("texture/a.bmp", ledger.OwnerOf("Assets/Art/Imported/texture/a.png"));
            Assert.IsNull(ledger.Find(RoPath.Parse("texture/a.tga")));
            Assert.AreEqual(1, report.Issues.Count(i => i.Code == RoIssueCode.OutputCollision));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Record_AgainForTheSameSource_ReplacesItsOutputs()
        {
            var ledger = RoLedger.Load("texture", _folder);
            Assert.IsTrue(ledger.Record(Entry("texture/a.bmp", "aa", 1, new[] { "x/old.png" })));

            Assert.IsTrue(ledger.Record(Entry("texture/a.bmp", "ab", 1, new[] { "x/new.png" })));

            Assert.IsNull(ledger.OwnerOf("x/old.png"));
            Assert.AreEqual("texture/a.bmp", ledger.OwnerOf("x/new.png"));
            Assert.AreEqual("ab", ledger.Find(RoPath.Parse("texture/a.bmp")).SourceHash);
        }

        [Test]
        public void OnlyLedgerOwnedPaths_AreRewritten_HandMadeFilesSurvive()
        {
            var ledger = RoLedger.Load("map", _folder);
            string generated = Output("model/a/mesh.bytes");
            ledger.Record(Entry("model/a.rsm", "aa", 1, new[] { generated }));

            string handMade = Output("model/a/custom.bytes", 7);                 // a person's asset inside the output root
            string additive = RoOutputPaths.AdditiveScene(_roots, "prontera");  // the hand-made additive scene
            Directory.CreateDirectory(Path.GetDirectoryName(RoImportTestRoot.FullPath(additive)));
            File.WriteAllBytes(RoImportTestRoot.FullPath(additive), new byte[] { 42 });

            // A run that rewrites everything it owns.
            foreach (var entry in ledger.Entries)
            foreach (var output in entry.Outputs)
                Assert.AreEqual(RoStatus.Updated, _writer.WriteBytes(output, new byte[] { 9 }));

            Assert.IsNull(ledger.OwnerOf(handMade));
            Assert.IsNull(ledger.OwnerOf(additive));
            CollectionAssert.AreEqual(new byte[] { 7 }, File.ReadAllBytes(RoImportTestRoot.FullPath(handMade)));
            CollectionAssert.AreEqual(new byte[] { 42 }, File.ReadAllBytes(RoImportTestRoot.FullPath(additive)));
            Assert.Throws<ArgumentException>(() => ledger.Record(Entry("maps/prontera.rsw", "pp", 1, new[] { additive })),
                "the additive scene can never become ledger-owned");
        }
    }
}
