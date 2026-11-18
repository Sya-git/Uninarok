using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Uninarok.RoImport.Core.Rsm;

namespace Uninarok.RoImport.Tests.Rsm
{
    // TASKS-0003 T3.1 (FR-2, FR-3): rule set R1 of PLAN-0003 4.4, in Ragnarok space with column vectors.
    // Expected values are worked out by hand in the comments.
    public class RsmPoseTests
    {
        const float Eps = 1e-4f;

        static RsmRules R14
        {
            get
            {
                Assert.IsTrue(RsmRules.TryGet(1, 4, out var r));
                return r;
            }
        }

        static RsmRestPose Rest(RoRsm model)
        {
            Assert.IsTrue(RsmHierarchy.TryBuild(model, out var tree, out var issue), issue?.ToString());
            return RsmPose.ComputeRest(model, tree, R14);
        }

        static void AreEqual(Vector3 expected, Vector3 actual, string what = null) =>
            Assert.That((expected - actual).magnitude, Is.LessThan(Eps), $"{what}: expected {expected:F5}, got {actual:F5}");

        // ---- rules table (T0.3) ----

        [Test]
        public void Rules_Version14_IsVerified()
        {
            Assert.IsTrue(RsmRules.TryGet(1, 4, out var r));
            Assert.IsTrue(r.RestFromFirstKey);
            Assert.IsFalse(r.HasScaleKeys);
            Assert.IsTrue(r.Recenter);
            Assert.IsTrue(r.OffsetVectorInVertexMatrix);
        }

        [TestCase(1, 5)]
        [TestCase(1, 3)]
        [TestCase(2, 2)]
        [TestCase(2, 3)]
        public void Rules_OtherVersions_HaveNoRow(int major, int minor)
        {
            Assert.IsFalse(RsmRules.TryGet(major, minor, out var r));
            Assert.IsNull(r);
        }

        // ---- local matrix (4.4 steps 1, 2) ----

        [Test]
        public void IdentityNode_HasIdentityLocal()
        {
            var pose = RsmPose.RestLocal(new RoRsmNode(), R14);
            AreEqual(Vector3.zero, pose.Position);
            Assert.AreEqual(1f, Mathf.Abs(Quaternion.Dot(Quaternion.identity, pose.Rotation)), 1e-6f);
            AreEqual(Vector3.one, pose.Scale);
        }

        [Test]
        public void RotationOnly_RotatesAboutTheAxis_RightHanded()
        {
            // 90° about +Y (column vectors, right-handed): (1,0,0) -> (0,0,-1), (0,0,1) -> (1,0,0).
            var node = new RoRsmNode { RotationAngle = Mathf.PI / 2f, RotationAxis = new Vector3(0, 1, 0) };
            var m = RsmPose.RestLocal(node, R14).Matrix;
            AreEqual(new Vector3(0, 0, -1), m.MultiplyPoint3x4(new Vector3(1, 0, 0)));
            AreEqual(new Vector3(1, 0, 0), m.MultiplyPoint3x4(new Vector3(0, 0, 1)));
        }

        [Test]
        public void RotationAxis_IsNormalized_AndAZeroAxisMeansNoRotation()
        {
            var scaled = new RoRsmNode { RotationAngle = Mathf.PI / 2f, RotationAxis = new Vector3(0, 5, 0) };
            AreEqual(new Vector3(0, 0, -1), RsmPose.RestLocal(scaled, R14).Matrix.MultiplyPoint3x4(Vector3.right));
            var zero = new RoRsmNode { RotationAngle = 1f, RotationAxis = Vector3.zero };
            AreEqual(Vector3.right, RsmPose.RestLocal(zero, R14).Matrix.MultiplyPoint3x4(Vector3.right));
        }

        [Test]
        public void PositionOnly_Translates()
        {
            var node = new RoRsmNode { Position = new Vector3(1, 2, 3) };
            AreEqual(new Vector3(2, 2, 3), RsmPose.RestLocal(node, R14).Matrix.MultiplyPoint3x4(Vector3.right));
        }

        [Test]
        public void PositionAndRotation_TheReportedModelClass()
        {
            // 나무잡초꽃/버섯02: angle θ = -3.927 about +Y, scale 0.0901, position (-3.4286, 1.6567, 22.7747).
            // cos θ = -0.7071003, sin θ = 0.7071133 (3.927 = π + π/4 + 9.2e-6).
            // v = (1,0,0): S -> (0.0901, 0, 0); R_y -> (cos θ · 0.0901, 0, -sin θ · 0.0901) = (-0.0637097, 0, -0.0637109);
            // T -> (-3.4923097, 1.6567, 22.7109891).
            var node = RsmTestModels.RotatedPositioned();
            var m = RsmPose.RestLocal(node, R14).Matrix;
            AreEqual(new Vector3(-3.4923097f, 1.6567f, 22.7109891f), m.MultiplyPoint3x4(new Vector3(1, 0, 0)));
        }

        [Test]
        public void FirstKeys_BeatTheStaticFields()
        {
            var node = new RoRsmNode
            {
                Position = new Vector3(9, 9, 9),
                RotationAngle = 1f,
                RotationAxis = Vector3.up,
                PosKeys = new[] { new RoRsmPosKey(0, new Vector3(1, 2, 3)), new RoRsmPosKey(100, new Vector3(7, 7, 7)) },
                RotKeys = new[] { new RoRsmRotKey(0, Quaternion.AngleAxis(90f, Vector3.forward)), new RoRsmRotKey(100, Quaternion.identity) }
            };
            var pose = RsmPose.RestLocal(node, R14);
            AreEqual(new Vector3(1, 2, 3), pose.Position);
            // 90° about +Z: (1,0,0) -> (0,1,0)
            AreEqual(Vector3.up, pose.Rotation * Vector3.right);
        }

        [Test]
        public void RotationKey_IsNormalized()
        {
            var node = new RoRsmNode { RotKeys = new[] { new RoRsmRotKey(0, new Quaternion(0, 0, 0, 2f)) } };
            Assert.AreEqual(1f, Quaternion.Dot(RsmPose.RestLocal(node, R14).Rotation, Quaternion.identity), 1e-6f);
        }

        // ---- vertex matrix (4.4 step 4, D9) ----

        [Test]
        public void VertexMatrix_TakesTheFileGroupsAsColumns_PlusTheOffset()
        {
            // file 1,0,0, 0,0,1, 0,-1,0 -> columns c0 = (1,0,0), c1 = (0,0,1), c2 = (0,-1,0).
            // A·(0,1,0) = c1 = (0,0,1); plus offset (1,2,3) -> (1,2,4). A·(0,0,1) = c2 + offset = (1,1,3).
            var node = new RoRsmNode { OffsetMatrix = new[] { 1f, 0f, 0f, 0f, 0f, 1f, 0f, -1f, 0f }, Offset = new Vector3(1, 2, 3) };
            var a = RsmPose.VertexMatrix(node, R14);
            AreEqual(new Vector3(1, 2, 4), a.MultiplyPoint3x4(new Vector3(0, 1, 0)));
            AreEqual(new Vector3(1, 1, 3), a.MultiplyPoint3x4(new Vector3(0, 0, 1)));
        }

        [Test]
        public void Offset_AppliesToTheNodesOwnVertices_NotToItsChild()
        {
            var parent = RsmTestModels.Cube("parent");
            parent.OffsetMatrix = new[] { 1f, 0f, 0f, 0f, 0f, 1f, 0f, -1f, 0f };
            parent.Offset = new Vector3(1, 2, 3);
            var child = RsmTestModels.Cube("child");
            child.ParentName = "parent";
            child.Position = new Vector3(5, 0, 0);
            var rest = Rest(RsmTestModels.Model(parent, child));

            // The child's origin is the parent's model matrix applied to (5,0,0): no offset, no offset matrix.
            AreEqual(rest.Model[0].MultiplyPoint3x4(new Vector3(5, 0, 0)), rest.Model[1].MultiplyPoint3x4(Vector3.zero));
            // The parent's vertex (0,1,0) goes through A: (1,2,4), then through its model matrix.
            AreEqual(rest.Model[0].MultiplyPoint3x4(new Vector3(1, 2, 4)),
                     (rest.Model[0] * rest.Vertex[0]).MultiplyPoint3x4(new Vector3(0, 1, 0)));
            Assert.AreEqual(Matrix4x4.identity, rest.Vertex[1]);
        }

        [Test]
        public void ParentRotation_MovesTheChild()
        {
            // Parent 90° about +Y; child at (5,0,0) -> (0,0,-5) relative to the parent origin.
            var parent = RsmTestModels.Cube("parent");
            parent.RotationAngle = Mathf.PI / 2f;
            parent.RotationAxis = Vector3.up;
            var child = RsmTestModels.Cube("child");
            child.ParentName = "parent";
            child.Position = new Vector3(5, 0, 0);
            var rest = Rest(RsmTestModels.Model(parent, child));
            var parentOrigin = rest.Model[0].MultiplyPoint3x4(Vector3.zero);
            AreEqual(parentOrigin + new Vector3(0, 0, -5), rest.Model[1].MultiplyPoint3x4(Vector3.zero));
        }

        // ---- recentering (4.4 step 5, FR-3) ----

        [Test]
        public void Recenter_PutsTheBottomCenterOfTheBoundsAtTheOrigin()
        {
            // Cube 0..1 at position (10, 20, 30): bounds (10,20,30)..(11,21,31). Y points down, so the bottom is max y.
            // Recenter = (-10.5, -21, -30.5); recentered bounds (-0.5,-1,-0.5)..(0.5,0,0.5).
            var cube = RsmTestModels.Cube();
            cube.Position = new Vector3(10, 20, 30);
            var rest = Rest(RsmTestModels.Model(cube));
            AreEqual(new Vector3(-10.5f, -21f, -30.5f), rest.Recenter, "recenter");
            AreEqual(new Vector3(-0.5f, -1f, -0.5f), rest.BoundsMin, "min");
            AreEqual(new Vector3(0.5f, 0f, 0.5f), rest.BoundsMax, "max");
            AreEqual(new Vector3(-0.5f, -1f, -0.5f), rest.Local[0].Position, "root position includes the recenter");
        }

        [Test]
        public void Recenter_UsesTheVerticesOfAllNodes_WithRotationAndOffset()
        {
            var model = RsmTestModels.TwoNodeAnimated();
            var rest = Rest(model);
            var all = new List<Vector3>();
            for (int n = 0; n < model.Nodes.Count; n++)
                all.AddRange(model.Nodes[n].Vertices.Select(v => (rest.Model[n] * rest.Vertex[n]).MultiplyPoint3x4(v)));
            var min = all.Aggregate(Vector3.Min);
            var max = all.Aggregate(Vector3.Max);
            AreEqual(Vector3.zero, new Vector3((min.x + max.x) / 2f, max.y, (min.z + max.z) / 2f), "bottom center");
            AreEqual(min, rest.BoundsMin);
            AreEqual(max, rest.BoundsMax);
        }

        [Test]
        public void Recenter_IsAddedToTheRootsFirstPositionKey()
        {
            var cube = RsmTestModels.Cube();
            cube.PosKeys = new[] { new RoRsmPosKey(0, new Vector3(10, 20, 30)), new RoRsmPosKey(100, Vector3.zero) };
            var rest = Rest(RsmTestModels.Model(cube));
            AreEqual(new Vector3(10, 20, 30) + rest.Recenter, rest.Local[0].Position);
        }

        [Test]
        public void Recenter_IsAppliedToEveryRoot_AndNotToChildren()
        {
            var a = RsmTestModels.Cube("a");
            var b = RsmTestModels.Cube("b");
            b.Position = new Vector3(4, 0, 0);
            var c = RsmTestModels.Cube("c");
            c.ParentName = "a";
            c.Position = new Vector3(0, 0, 2);
            var rest = Rest(RsmTestModels.Model(a, b, c));
            AreEqual(rest.Recenter, rest.Local[0].Position);
            AreEqual(new Vector3(4, 0, 0) + rest.Recenter, rest.Local[1].Position);
            AreEqual(new Vector3(0, 0, 2), rest.Local[2].Position);
        }

        [Test]
        public void EmptyModel_HasNoRecenter()
        {
            var rest = Rest(RsmTestModels.Model(new RoRsmNode { Name = "empty" }));
            AreEqual(Vector3.zero, rest.Recenter);
        }

        // ---- guard against the old importer's formula ----

        [Test]
        public void OldImporterFormula_DoesNotMatchR1()
        {
            // RsmImpoter.cs: root only, v' = ((v · M_file) · R) · S - (P.x, -P.y, P.z) + offset, then Y flipped,
            // no recentering. Applied to the reported-model sample it misplaces the model; this test fails if R1 is
            // replaced by it.
            var node = RsmTestModels.RotatedPositioned();
            var rest = Rest(RsmTestModels.Model(node));
            var r1 = node.Vertices.Select(v => (rest.Model[0] * rest.Vertex[0]).MultiplyPoint3x4(v)).ToArray();

            var rowM = Matrix4x4.identity;   // v · M_file: the file groups as rows of a row-vector product
            for (int r = 0; r < 3; r++) for (int c = 0; c < 3; c++) rowM[c, r] = node.OffsetMatrix[3 * r + c];
            var rot = Matrix4x4.Rotate(Quaternion.AngleAxis(node.RotationAngle * Mathf.Rad2Deg, node.RotationAxis));
            var old = node.Vertices.Select(v =>
            {
                var p = rowM.MultiplyPoint3x4(v);
                p = rot.transpose.MultiplyPoint3x4(p);
                p = Vector3.Scale(p, node.Scale);
                p -= new Vector3(node.Position.x, -node.Position.y, node.Position.z);
                return p + node.Offset;
            }).ToArray();

            float worst = r1.Zip(old, (x, y) => (x - y).magnitude).Max();
            Assert.Greater(worst, 0.1f, "the old formula must not pass for R1");
        }
    }
}
