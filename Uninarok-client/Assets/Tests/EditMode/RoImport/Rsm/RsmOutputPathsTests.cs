using System.IO;
using System.Linq;
using NUnit.Framework;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Editor.Pipeline;
using Uninarok.RoImport.Editor.Rsm;

namespace Uninarok.RoImport.Tests.Rsm
{
    // TASKS-0003 T6.1 (FR-1, FR-10): output paths of PLAN-0003 §3.
    public class RsmOutputPathsTests
    {
        static RoOutputRoots Roots => new RoOutputRoots();

        [Test]
        public void Paths_MirrorTheRagnarokPath()
        {
            var p = RsmOutputPaths.For(Roots, RoPath.Parse("model/나무잡초꽃/버섯02.rsm"), new[] { "Cylinder01" });
            Assert.AreEqual("Assets/Art/Imported/model/나무잡초꽃/버섯02.prefab", p.Prefab);
            Assert.AreEqual("Assets/Art/Imported/model/나무잡초꽃/버섯02", p.Folder);
            Assert.AreEqual("Assets/Art/Imported/model/나무잡초꽃/버섯02/Cylinder01.asset", p.Meshes.Single());
            Assert.AreEqual("Assets/Art/Imported/model/나무잡초꽃/버섯02/버섯02.anim", p.Clip(0));
            Assert.AreEqual("Assets/Art/Imported/model/나무잡초꽃/버섯02/버섯02_2.anim", p.Clip(1));
            Assert.AreEqual("Assets/Art/Imported/model/나무잡초꽃/버섯02/버섯02.controller", p.Controller);
        }

        [Test]
        public void TwoNodesWithTheSameName_GiveTwoMeshFiles()
        {
            // RsmTree.UniqueNames already renamed the second one; the file names stay distinct.
            var p = RsmOutputPaths.For(Roots, RoPath.Parse("model/a/b.rsm"), new[] { "leaf", "leaf_2" });
            Assert.AreEqual(2, p.Meshes.Distinct().Count());
        }

        [Test]
        public void NamesThatOnlyDifferInCaseOrSanitizedCharacters_GiveDistinctFiles()
        {
            var p = RsmOutputPaths.For(Roots, RoPath.Parse("model/a/b.rsm"), new[] { "Box", "box", "a:b", "a_b" });
            Assert.AreEqual(4, p.Meshes.Select(m => m.ToLowerInvariant()).Distinct().Count(), string.Join(", ", p.Meshes));
        }

        [Test]
        public void KoreanAndInvalidCharacters_GiveAValidPath()
        {
            var p = RsmOutputPaths.For(Roots, RoPath.Parse("model/a/b.rsm"), new[] { "나무<1>:*?|\"/\\. " });
            string file = Path.GetFileName(p.Meshes[0]);
            StringAssert.StartsWith("나무", file);
            Assert.IsEmpty(file.Intersect(Path.GetInvalidFileNameChars()));
            Assert.IsFalse(file.Contains(':') || file.Contains('*') || file.Contains('?'));
            // < > : * ? | " / \ become _; the trailing ". " goes.
            Assert.AreEqual("나무_1________.asset", file);
        }

        [Test]
        public void EmptyName_GetsAName()
        {
            Assert.AreEqual("node", RsmOutputPaths.FileSafe(""));
            Assert.AreEqual("node", RsmOutputPaths.FileSafe(null));
            Assert.AreEqual("a", RsmOutputPaths.FileSafe("a. "));
        }

        [Test]
        public void ModelsWithTheSameNameInDifferentFolders_DoNotCollide()
        {
            var a = RsmOutputPaths.For(Roots, RoPath.Parse("model/x/tree.rsm"), new[] { "n" });
            var b = RsmOutputPaths.For(Roots, RoPath.Parse("model/y/tree.rsm"), new[] { "n" });
            Assert.AreNotEqual(a.Prefab, b.Prefab);
            Assert.AreNotEqual(a.Meshes[0], b.Meshes[0]);
            Assert.AreNotEqual(a.Clip(0), b.Clip(0));
            Assert.AreNotEqual(a.Controller, b.Controller);
        }

        [Test]
        public void ModelJobKind_Exists()
        {
            Assert.IsTrue(System.Enum.IsDefined(typeof(RoJobKind), "Model"));
        }
    }
}
