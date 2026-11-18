using System.Linq;
using NUnit.Framework;
using Uninarok.RoImport.Core.Source;
using Uninarok.RoImport.Editor.Pipeline;
using Uninarok.RoImport.Editor.Ui;

namespace Uninarok.RoImport.Tests
{
    // SPEC-0002 FR-2: the window converts a list of paths and folders. Plan 4.11. TASKS T7.1.
    public class RoImportJobsTests
    {
        [Test]
        public void Lines_ExpandToJobs_FoldersThroughTheSource()
        {
            var source = new RoFolderSource(RoFolderSourceTests.FixtureRoot);

            var expansion = RoImportJobs.Expand("# textures\ntexture/유저인터페이스/\n\r\nDATA\\Texture\\A.BMP\n  \ntexture/none/\nC:/bad.bmp\n", RoJobKind.Texture, source);

            CollectionAssert.AreEqual(new[] { "texture/유저인터페이스/button.bmp", "texture/a.bmp" }, expansion.Jobs.Select(j => j.Path.Canonical));
            Assert.IsTrue(expansion.Jobs.All(j => j.Kind == RoJobKind.Texture));
            Assert.AreEqual(2, expansion.Invalid.Count, "an empty folder and an invalid path are reported, not converted");
        }

        // Plan 4.11, D21: folders expand only to files the converter handles; a single path is always a job.
        [Test]
        public void AFolder_ExpandsToTheImagesOnly_ButASingleUnsupportedPathIsStillAJob()
        {
            var source = new RoFolderSource(RoFolderSourceTests.FixtureRoot);

            var folder = RoImportJobs.Expand("texture/", RoJobKind.Texture, source);
            var single = RoImportJobs.Expand("texture/unsupported.dds", RoJobKind.Texture, source);

            Assert.AreEqual(7, folder.Jobs.Count, "8 fixture files, the .dds skipped silently");
            Assert.IsFalse(folder.Jobs.Any(j => j.Path.Extension == ".dds"));
            CollectionAssert.IsEmpty(folder.Invalid, "skipping is silent");
            CollectionAssert.AreEqual(new[] { "texture/unsupported.dds" }, single.Jobs.Select(j => j.Path.Canonical),
                "an explicit request still reaches the converter, which warns (FR-8)");
        }

        // PLAN-0004 D11, TASKS-0004 T5.3: the map files sit at the data root; "/" alone names the root.
        [Test]
        public void ARootLine_CoversTheWholeDataRoot()
        {
            var source = new RoFolderSource(RoFolderSourceTests.FixtureRoot);

            var root = RoImportJobs.Expand("/", RoJobKind.Texture, source);
            var texture = RoImportJobs.Expand("texture/", RoJobKind.Texture, source);

            CollectionAssert.AreEqual(texture.Jobs.Select(j => j.Path.Canonical), root.Jobs.Select(j => j.Path.Canonical),
                "every image of the fixture is under texture/");
            CollectionAssert.IsEmpty(root.Invalid);
        }

        [Test]
        public void ConvertAllGrounds_ListsEveryGndInTheDataRoot()
        {
            string data = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "RoImportTests", System.Guid.NewGuid().ToString("N"));
            try
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.Combine(data, "texture"));
                foreach (string f in new[] { "b.gnd", "a.gnd", "a.gat", "a.rsw", "texture/c.gnd" })
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(data, f), new byte[] { 1 });

                var jobs = RoImportMenu.GroundJobs(new RoFolderSource(data));

                CollectionAssert.AreEqual(new[] { "a.gnd", "b.gnd", "texture/c.gnd" }, jobs.Select(j => j.Path.Canonical));
                Assert.IsTrue(jobs.All(j => j.Kind == RoJobKind.Ground));
            }
            finally
            {
                System.IO.Directory.Delete(data, recursive: true);
            }
        }

        [Test]
        public void AFolderWithoutImages_IsReported()
        {
            var expansion = RoImportJobs.Expand("texture/", RoJobKind.Texture, new RoFolderSource(System.IO.Path.Combine(RoFolderSourceTests.FixtureRoot, "no-such-root")));

            Assert.AreEqual(1, expansion.Invalid.Count);
        }
    }
}
