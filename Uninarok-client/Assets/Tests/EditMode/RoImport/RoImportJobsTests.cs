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

        [Test]
        public void TheWholeTextureFolder_ExpandsToEveryFixture()
        {
            var expansion = RoImportJobs.Expand("texture/", RoJobKind.Texture, new RoFolderSource(RoFolderSourceTests.FixtureRoot));

            Assert.AreEqual(8, expansion.Jobs.Count);
        }
    }
}
