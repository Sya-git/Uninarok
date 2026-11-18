using System;
using NUnit.Framework;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Paths;

namespace Uninarok.RoImport.Tests
{
    // SPEC-0002 FR-4: outputs are project assets in three areas, mirroring the Ragnarok path, never colliding by name.
    // Plan 4.5, D9. TASKS T4.1.
    public class RoOutputPathsTests
    {
        readonly RoOutputRoots Roots = new RoOutputRoots();

        [Test]
        public void DefaultRoots_AreTheThreeProjectAreas()
        {
            Assert.AreEqual("Assets/Art/Imported", Roots.Art);
            Assert.AreEqual("Assets/Data", Roots.Data);
            Assert.AreEqual("Assets/Scenes/Maps", Roots.Scenes);
        }

        [Test]
        public void SameFileNameInTwoRagnarokFolders_GivesTwoOutputPaths()
        {
            string a = RoOutputPaths.Texture(Roots, RoPath.Parse("texture/프론테라/wall.bmp"));
            string b = RoOutputPaths.Texture(Roots, RoPath.Parse("texture/izlude/wall.bmp"));

            Assert.AreNotEqual(a, b);
            Assert.AreEqual("Assets/Art/Imported/texture/프론테라/wall.png", a);
            Assert.AreEqual("Assets/Art/Imported/texture/izlude/wall.png", b);
        }

        [Test]
        public void OutputPath_IsLowercaseWithForwardSlashes_HoweverTheSourceWasSpelled()
        {
            string path = RoOutputPaths.Texture(Roots, RoPath.Parse(@"DATA\Texture\Sub\A.BMP"));

            Assert.AreEqual("Assets/Art/Imported/texture/sub/a.png", path);
            StringAssert.DoesNotContain("\\", path);
        }

        [Test]
        public void Mirror_UsesTheConvertersExtension()
        {
            Assert.AreEqual("Assets/Art/Imported/model/izlude/iz_bridge01.prefab",
                RoOutputPaths.Mirror(Roots, RoPath.Parse("model/izlude/iz_bridge01.rsm"), ".prefab"));
        }

        [Test]
        public void Material_IsUnderMaterials_WithTheTexturePathAndSuffix()
        {
            Assert.AreEqual("Assets/Art/Imported/_materials/texture/sub/a__cutout_2s.mat",
                RoOutputPaths.Material(Roots, RoPath.Parse("texture/sub/a.bmp"), "cutout_2s"));
        }

        [Test]
        public void PlaceholderDataAndScenePaths()
        {
            Assert.AreEqual("Assets/Art/Imported/_placeholders/missing_texture.png", RoOutputPaths.Placeholder(Roots, "missing_texture.png"));
            Assert.AreEqual("Assets/Data/Map/prontera.asset", RoOutputPaths.Data(Roots, "Map", "prontera", ".asset"));
            Assert.AreEqual("Assets/Data/Map/prontera.asset", RoOutputPaths.Data(Roots, "Map", "prontera", "asset"));
            Assert.AreEqual("Assets/Scenes/Maps/prontera.unity", RoOutputPaths.Scene(Roots, "prontera"));
            Assert.AreEqual("Assets/Scenes/Maps/prontera.additive.unity", RoOutputPaths.AdditiveScene(Roots, "prontera"));
            Assert.AreNotEqual(RoOutputPaths.Scene(Roots, "prontera"), RoOutputPaths.AdditiveScene(Roots, "prontera"));
        }

        [Test]
        public void CustomRoots_AreUsed()
        {
            var test = RoOutputRoots.Under("Assets/__RoImportTests/");

            Assert.AreEqual("Assets/__RoImportTests/Art/texture/a.png", RoOutputPaths.Texture(test, RoPath.Parse("texture/a.bmp")));
            Assert.AreEqual("Assets/__RoImportTests/Data/Map/x.asset", RoOutputPaths.Data(test, "Map", "x", ".asset"));
            Assert.AreEqual("Assets/__RoImportTests/Scenes/x.unity", RoOutputPaths.Scene(test, "x"));
        }

        [TestCase("")]
        [TestCase(" ")]
        [TestCase("a/b")]
        [TestCase(@"a\b")]
        [TestCase("..")]
        public void NamesThatAreNotOneSegment_AreRejected(string name)
        {
            Assert.Throws<ArgumentException>(() => RoOutputPaths.Placeholder(Roots, name));
            Assert.Throws<ArgumentException>(() => RoOutputPaths.Scene(Roots, name));
            Assert.Throws<ArgumentException>(() => RoOutputPaths.Data(Roots, "Map", name, ".asset"));
        }

        [Test]
        public void EmptySource_IsRejected()
        {
            Assert.Throws<ArgumentException>(() => RoOutputPaths.Texture(Roots, default));
        }
    }
}
