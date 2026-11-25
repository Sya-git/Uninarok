using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Source;
using Uninarok.RoImport.Editor.Effects;
using Uninarok.RoImport.Editor.Pipeline;

namespace Uninarok.RoImport.Tests.Effects
{
    /// <summary>SPEC-0012 FR-2, FR-7 (PLAN-0012 4.10): the effect folder import also converts the textures authored entries name.</summary>
    public class RoEffectFolderImportTests
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

        RoImportOptions Options(RoFolderSource source) => new RoImportOptions
        {
            Source = source, Roots = _roots, LedgerFolder = Path.Combine(_temp, "ledger"), ShowProgress = false,
        };

        static RoEffectFolderImport.AuthoredTextures[] Authored(params string[] names) =>
            new[] { new RoEffectFolderImport.AuthoredTextures("texture/effect/_authored/x/a.quad", names) };

        string Output(string source) => RoEffectOutputPaths.Texture(_roots, RoPath.Parse(source));

        [Test]
        public void TheNamedTextures_AreConverted_WithLedgerEntries_AndARerunSkipsThem()
        {
            var source = Source();
            var report = RoEffectFolderImport.Run(Options(source), null, Authored(Tga, Bmp), includeStr: false);

            Assert.AreEqual(2, report.Created);
            Assert.AreEqual(0, report.Issues.Count());
            Assert.IsTrue(File.Exists(RoImportTestRoot.FullPath(Output(Tga))));
            Assert.IsTrue(File.Exists(RoImportTestRoot.FullPath(Output(Bmp))));
            string ledgers = Path.Combine(_temp, "ledger");
            Assert.IsTrue(Directory.Exists(ledgers) && Directory.GetFiles(ledgers, "*", SearchOption.AllDirectories).Length > 0, "ledger written");

            var again = RoEffectFolderImport.Run(Options(source), null, Authored(Tga, Bmp), includeStr: false);
            Assert.AreEqual(0, again.Created + again.Updated);
            Assert.AreEqual(2, again.Skipped);
        }

        [Test]
        public void ABareName_IsFoundInTheEffectRoot()
        {
            var report = RoEffectFolderImport.Run(Options(Source()), null, Authored("pok1.tga"), includeStr: false);

            Assert.AreEqual(1, report.Created);
            Assert.IsTrue(File.Exists(RoImportTestRoot.FullPath(Output(Tga))));
        }

        [Test]
        public void ANameNotFound_GivesOneTextureMissingWarning()
        {
            var report = RoEffectFolderImport.Run(Options(Source()), null, Authored(Tga, "texture/effect/nope.tga"), includeStr: false);

            Assert.AreEqual(1, report.Created);
            Assert.AreEqual(1, report.Issues.Count(i => i.Code == RoIssueCode.TextureMissing));
        }
    }
}
