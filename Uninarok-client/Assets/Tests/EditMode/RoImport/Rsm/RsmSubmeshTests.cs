using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Uninarok.RoImport.Core.Materials;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Rsm;

namespace Uninarok.RoImport.Tests.Rsm
{
    // TASKS-0003 T4.2 (FR-6, FR-7): faces grouped by (texture, two-sided), PLAN-0003 4.3 and 4.6.
    public class RsmSubmeshTests
    {
        /// <summary>A cube whose 12 faces use node slots and sidedness given per face.</summary>
        static RoRsmNode Cube(int[] slots, bool[] twoSided, int[] textureSlots)
        {
            var cube = RsmTestModels.Cube(perFaceUv: false);
            cube.TextureSlots = textureSlots;
            cube.Faces = cube.Faces.Select((f, i) =>
            {
                f.TextureSlot = slots[i];
                f.TwoSided = twoSided[i];
                return f;
            }).ToArray();
            return cube;
        }

        static RsmMeshData Build(RoRsmNode node, RoReport report = null) =>
            RsmMeshBuilder.Build(node, RsmShadeType.Smooth, true, Matrix4x4.identity, false, report ?? new RoReport(), "model/test.rsm");

        [Test]
        public void Faces_GroupByTextureAndSidedness_InAscendingOrder()
        {
            // Node slots map to model texture indices 5 and 2.
            var slots = new[] { 0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 1, 1 };
            var two = new[] { false, false, false, false, true, true, true, true, false, false, false, false };
            var mesh = Build(Cube(slots, two, new[] { 5, 2 }));
            CollectionAssert.AreEqual(new[] { (2, false), (2, true), (5, false), (5, true) },
                mesh.Submeshes.Select(s => (s.TextureIndex, s.TwoSided)));
            CollectionAssert.AreEqual(new[] { 4, 2, 4, 2 }, mesh.Submeshes.Select(s => s.Triangles.Length / 3));
        }

        [Test]
        public void TwoSidedFaces_AreNotDuplicated()
        {
            var all = Enumerable.Repeat(true, 12).ToArray();
            var mesh = Build(Cube(new int[12], all, new[] { 0 }));
            Assert.AreEqual(1, mesh.Submeshes.Count);
            Assert.IsTrue(mesh.Submeshes[0].TwoSided);
            Assert.AreEqual(12, mesh.TriangleCount);
        }

        [Test]
        public void MaterialKey_OfATwoSidedGroup_IsTwoSided()
        {
            var two = Enumerable.Range(0, 12).Select(i => i < 6).ToArray();
            var mesh = Build(Cube(new int[12], two, new[] { 0 }));
            var texture = RoPath.Parse("texture/test/a.bmp");
            var keys = mesh.Submeshes.Select(s => s.MaterialKey(texture, RoSurface.Cutout)).ToArray();
            CollectionAssert.AreEqual(new[] { false, true }, keys.Select(k => k.TwoSided));
            Assert.AreEqual(RoSurface.Cutout, keys[1].Surface);
            Assert.AreEqual(texture, keys[1].Texture);
        }

        [Test]
        public void BadTextureSlot_DropsThatFace_WithOneWarningPerNode()
        {
            var slots = new[] { 0, 0, 7, 7, 0, 0, 0, 0, 0, 0, 0, 9 };
            var report = new RoReport();
            var mesh = Build(Cube(slots, new bool[12], new[] { 0 }), report);
            Assert.AreEqual(9, mesh.TriangleCount);
            Assert.AreEqual(1, report.Issues.Count);
            Assert.AreEqual(RoIssueCode.RsmBadIndex, report.Issues[0].Code);
            Assert.AreEqual("model/test.rsm", report.Issues[0].Referrer);
            StringAssert.Contains("3", report.Issues[0].Message, "names the number of dropped faces");
        }

        [Test]
        public void BadVertexOrTexCoordIndex_DropsThatFace_WithOneWarning()
        {
            var cube = RsmTestModels.Cube(perFaceUv: false);
            var faces = cube.Faces.ToArray();
            faces[0].V1 = 99;
            faces[1].T2 = -1;
            cube.Faces = faces;
            var report = new RoReport();
            var mesh = Build(cube, report);
            Assert.AreEqual(10, mesh.TriangleCount);
            Assert.AreEqual(1, report.Issues.Count(i => i.Code == RoIssueCode.RsmBadIndex));
        }

        [Test]
        public void NodeWithoutFaces_HasNoSubmesh()
        {
            var mesh = Build(new RoRsmNode { Name = "empty" });
            Assert.IsEmpty(mesh.Submeshes);
            Assert.IsEmpty(mesh.Positions);
        }

        [Test]
        public void MirroringVertexMatrix_ReversesTheWindingOnceMore()
        {
            var cube = RsmTestModels.Cube(perFaceUv: false);
            var plain = RsmMeshBuilder.Build(cube, RsmShadeType.Smooth, true, Matrix4x4.identity, false, new RoReport());
            var extra = RsmMeshBuilder.Build(cube, RsmShadeType.Smooth, true, Matrix4x4.identity, true, new RoReport());
            var a = plain.Submeshes[0].Triangles;
            var b = extra.Submeshes[0].Triangles;
            for (int i = 0; i < a.Length; i += 3)
                CollectionAssert.AreEqual(new[] { a[i], a[i + 2], a[i + 1] }, new[] { b[i], b[i + 1], b[i + 2] });
        }
    }
}
