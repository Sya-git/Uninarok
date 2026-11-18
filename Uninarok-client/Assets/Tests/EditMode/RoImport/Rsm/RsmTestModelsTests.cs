using System.Linq;
using NUnit.Framework;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Rsm;

namespace Uninarok.RoImport.Tests.Rsm
{
    // TASKS-0003 T1.1 (FR-1, FR-8): the hand-made sample models build with the expected counts, so later tests can rely
    // on them.
    public class RsmTestModelsTests
    {
        [Test]
        public void Cube_PerFaceUv_Has8Vertices24TexCoords12Faces()
        {
            var cube = RsmTestModels.Cube();
            Assert.AreEqual(8, cube.Vertices.Length);
            Assert.AreEqual(24, cube.TexCoords.Length);
            Assert.AreEqual(12, cube.Faces.Length);
            Assert.IsFalse(cube.IsAnimated);
        }

        [Test]
        public void Cube_SeamlessUv_Has8TexCoords()
        {
            var cube = RsmTestModels.Cube(perFaceUv: false);
            Assert.AreEqual(8, cube.Vertices.Length);
            Assert.AreEqual(8, cube.TexCoords.Length);
            Assert.AreEqual(12, cube.Faces.Length);
        }

        [Test]
        public void CylinderStrip_HasSharedPositionsAndASeam()
        {
            var strip = RsmTestModels.CylinderStrip(8);
            Assert.AreEqual(16, strip.Vertices.Length);
            Assert.AreEqual(18, strip.TexCoords.Length);
            Assert.AreEqual(16, strip.Faces.Length);
        }

        [Test]
        public void Faces_OnlyReferToExistingVerticesAndCoordinates()
        {
            foreach (var node in new[] { RsmTestModels.Cube(), RsmTestModels.Cube(perFaceUv: false), RsmTestModels.CylinderStrip(5) })
            foreach (var f in node.Faces)
            {
                foreach (var v in new[] { f.V0, f.V1, f.V2 }) Assert.That(v, Is.InRange(0, node.Vertices.Length - 1));
                foreach (var t in new[] { f.T0, f.T1, f.T2 }) Assert.That(t, Is.InRange(0, node.TexCoords.Length - 1));
            }
        }

        [Test]
        public void RotatedPositioned_IsOneNodeWithRotationAndPosition()
        {
            var model = RsmTestModels.Model(RsmTestModels.RotatedPositioned());
            Assert.AreEqual(1, model.Nodes.Count);
            var n = model.Nodes[0];
            Assert.AreNotEqual(0f, n.RotationAngle);
            Assert.AreNotEqual(0f, n.Position.sqrMagnitude);
            Assert.AreEqual(9, n.OffsetMatrix.Length);
        }

        [Test]
        public void ParentAndChild_HasTwoNodesAndOneRoot()
        {
            var model = RsmTestModels.ParentAndChild();
            Assert.AreEqual(2, model.Nodes.Count);
            Assert.AreEqual(1, model.Nodes.Count(n => string.IsNullOrEmpty(n.ParentName)));
            Assert.AreEqual("parent", model.Nodes[1].ParentName);
        }

        [Test]
        public void TwoNodeAnimated_HasFiveRotationKeysOnTheChildOnly()
        {
            var model = RsmTestModels.TwoNodeAnimated();
            Assert.AreEqual(2, model.Nodes.Count);
            Assert.IsFalse(model.Nodes[0].IsAnimated);
            Assert.AreEqual(5, model.Nodes[1].RotKeys.Length);
            Assert.AreEqual(1000, model.Nodes[1].RotKeys[4].Time);
            Assert.AreEqual(0, model.Nodes[1].PosKeys.Length);
            Assert.AreEqual(0, model.Nodes[1].ScaleKeys.Length);
        }

        [Test]
        public void Defaults_AreIdentityAndEmpty()
        {
            var n = new RoRsmNode();
            CollectionAssert.AreEqual(new[] { 1f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f }, n.OffsetMatrix);
            Assert.AreEqual(UnityEngine.Vector3.one, n.Scale);
            Assert.IsEmpty(n.Vertices);
            var m = new RoRsm();
            Assert.AreEqual(RsmShadeType.Smooth, m.ShadeType);
            Assert.IsEmpty(m.Nodes);
        }

        [Test]
        public void IssueCodes_ForRsmExist()
        {
            foreach (var name in new[] { "UnsupportedRsmVersion", "RsmParentMissing", "RsmCycle", "RsmDuplicateName", "RsmBadIndex" })
                Assert.IsTrue(System.Enum.IsDefined(typeof(RoIssueCode), name), name);
        }
    }
}
