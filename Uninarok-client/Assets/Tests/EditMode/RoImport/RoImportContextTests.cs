using System;
using System.IO;
using NUnit.Framework;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Source;
using Uninarok.RoImport.Editor.Ledger;
using Uninarok.RoImport.Editor.Pipeline;
using Uninarok.RoImport.Editor.Writing;

namespace Uninarok.RoImport.Tests
{
    // SPEC-0002 FR-2 (conversions run from code, no UI), FR-6 (dependencies on demand, memo and cycle guard). Plan 4.10. TASKS T5.2.
    public class RoImportContextTests
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

        [Test]
        public void Context_OverTheFixtureSourceAndTheThrowawayRoot()
        {
            var source = new RoFolderSource(RoFolderSourceTests.FixtureRoot);
            var report = new RoReport();

            var ctx = new RoImportContext(source, _roots, _ledgerFolder, report);

            Assert.AreSame(source, ctx.Source);
            Assert.AreSame(_roots, ctx.Roots);
            Assert.AreSame(report, ctx.Report);
            Assert.IsNotNull(ctx.Writer);
            Assert.IsInstanceOf<RoPngCodec>(ctx.Png);
            Assert.IsFalse(ctx.Force);
            Assert.IsTrue(ctx.Source.TryGetFile(RoPath.Parse("texture/a.bmp"), out _));
        }

        [Test]
        public void WithoutAReport_TheContextMakesOne()
        {
            Assert.IsNotNull(new RoImportContext(new RoFolderSource(RoFolderSourceTests.FixtureRoot), _roots, _ledgerFolder).Report);
        }

        [Test]
        public void Ledger_IsLoadedOncePerCategory_FromTheGivenFolder()
        {
            var ctx = new RoImportContext(new RoFolderSource(RoFolderSourceTests.FixtureRoot), _roots, _ledgerFolder);

            var texture = ctx.Ledger("texture");

            Assert.AreSame(texture, ctx.Ledger("texture"));
            Assert.AreNotSame(texture, ctx.Ledger("material"));
            Assert.AreEqual(Path.GetFullPath(Path.Combine(_ledgerFolder, "texture.json")), texture.FilePath);
        }

        [Test]
        public void SaveLedgers_WritesTheLoadedLedgers()
        {
            var ctx = new RoImportContext(new RoFolderSource(RoFolderSourceTests.FixtureRoot), _roots, _ledgerFolder);
            ctx.Ledger("texture").Record(new RoLedgerEntry { Source = "texture/a.bmp", SourceHash = "aa", Rules = 1, Outputs = new[] { "x/a.png" } });

            ctx.SaveLedgers();

            Assert.IsTrue(File.Exists(Path.Combine(_ledgerFolder, "texture.json")));
            Assert.AreEqual("aa", RoLedger.Load("texture", _ledgerFolder).Find(RoPath.Parse("texture/a.bmp")).SourceHash);
        }

        [Test]
        public void Memo_RemembersResultsByKeyAndType()
        {
            var ctx = new RoImportContext(new RoFolderSource(RoFolderSourceTests.FixtureRoot), _roots, _ledgerFolder);

            Assert.IsFalse(ctx.TryGetMemo<string>("texture:texture/a.bmp", out _));
            ctx.Memo("texture:texture/a.bmp", "done");

            Assert.IsTrue(ctx.TryGetMemo<string>("texture:texture/a.bmp", out var value));
            Assert.AreEqual("done", value);
            Assert.IsFalse(ctx.TryGetMemo<int>("texture:texture/a.bmp", out _), "a value of another type is not returned");
        }

        [Test]
        public void InProgressSet_CatchesAReentry()
        {
            var ctx = new RoImportContext(new RoFolderSource(RoFolderSourceTests.FixtureRoot), _roots, _ledgerFolder);

            Assert.IsTrue(ctx.TryEnter("model:model/a.rsm"));
            Assert.IsFalse(ctx.TryEnter("model:model/a.rsm"), "the same item while it is converting is a cycle");
            ctx.Exit("model:model/a.rsm");
            Assert.IsTrue(ctx.TryEnter("model:model/a.rsm"));
        }
    }
}
