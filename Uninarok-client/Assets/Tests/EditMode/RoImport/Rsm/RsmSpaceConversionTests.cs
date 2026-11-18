using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Uninarok.RoImport.Core.Rsm;
using Uninarok.RoImport.Core.Space;

namespace Uninarok.RoImport.Tests.Rsm
{
    // TASKS-0003 T3.1 (FR-2, FR-6): PLAN-0003 4.5. The prefab's transform hierarchy with the converted vertices must
    // equal the Ragnarok matrix chain converted by RoSpace, and faces must stay outward facing.
    public class RsmSpaceConversionTests
    {
        static RsmRules R14
        {
            get
            {
                Assert.IsTrue(RsmRules.TryGet(1, 4, out var r));
                return r;
            }
        }

        [Test]
        public void Conversion_OfLocal_IsRoSpace()
        {
            var pose = new RsmNodePose(new Vector3(10, 20, 30), Quaternion.AngleAxis(30f, new Vector3(1, 2, 3).normalized), new Vector3(1, 2, 3));
            Assert.AreEqual(RoSpace.Point(pose.Position), RsmSpace.LocalPosition(pose));
            Assert.AreEqual(1f, Mathf.Abs(Quaternion.Dot(RoSpace.Rotation(Matrix4x4.Rotate(pose.Rotation)), RsmSpace.LocalRotation(pose))), 1e-6f);
            Assert.AreEqual(pose.Scale, RsmSpace.LocalScale(pose));
        }

        [Test]
        public void Rotation_ConjugatesTheQuaternion()
        {
            // F·R·F for F = diag(1,-1,1): (x, y, z, w) -> (-x, y, -z, w), up to sign.
            var q = new Quaternion(0.1f, 0.2f, 0.3f, 0.927f).normalized;
            var u = RsmSpace.LocalRotation(new RsmNodePose(Vector3.zero, q, Vector3.one));
            var expected = new Quaternion(-q.x, q.y, -q.z, q.w);
            Assert.AreEqual(1f, Mathf.Abs(Quaternion.Dot(expected, u)), 1e-5f);
        }

        [Test]
        public void TransformHierarchy_WithConvertedVertices_EqualsTheConvertedMatrixChain()
        {
            var random = new System.Random(3);
            float R(float a) => (float)(random.NextDouble() * 2 - 1) * a;
            for (int trial = 0; trial < 50; trial++)
            {
                var root = RsmTestModels.Cube("root");
                root.Position = new Vector3(R(50), R(50), R(50));
                root.RotationAngle = R(4);
                root.RotationAxis = new Vector3(R(1), R(1), R(1));
                root.Scale = new Vector3(0.5f + random.Next(2), 1f + R(0.5f), trial % 3 == 0 ? -1f : 1f);
                var child = RsmTestModels.Cube("child");
                child.ParentName = "root";
                child.Position = new Vector3(R(20), R(20), R(20));
                child.RotKeys = new[] { new RoRsmRotKey(0, new Quaternion(R(1), R(1), R(1), R(1)).normalized) };
                child.Offset = new Vector3(R(5), R(5), R(5));
                child.OffsetMatrix = Enumerable.Range(0, 9).Select(_ => R(2)).ToArray();
                var model = RsmTestModels.Model(root, child);
                Assert.IsTrue(RsmHierarchy.TryBuild(model, out var tree, out _));
                var rest = RsmPose.ComputeRest(model, tree, R14);

                // Unity side: two nested transforms with the converted local values.
                var go = new GameObject("root");
                var goChild = new GameObject("child");
                try
                {
                    goChild.transform.SetParent(go.transform, false);
                    foreach (var (t, n) in new[] { (go.transform, 0), (goChild.transform, 1) })
                    {
                        t.localPosition = RsmSpace.LocalPosition(rest.Local[n]);
                        t.localRotation = RsmSpace.LocalRotation(rest.Local[n]);
                        t.localScale = RsmSpace.LocalScale(rest.Local[n]);
                    }
                    var transforms = new[] { go.transform, goChild.transform };
                    for (int n = 0; n < 2; n++)
                    foreach (var v in model.Nodes[n].Vertices)
                    {
                        // The Ragnarok chain Model·A·v, converted as a point (Y flipped, scaled to Unity units).
                        var expected = RoSpace.Point((rest.Model[n] * rest.Vertex[n]).MultiplyPoint3x4(v));
                        var actual = transforms[n].TransformPoint(RsmSpace.Vertex(rest.Vertex[n], v));
                        Assert.That((expected - actual).magnitude, Is.LessThan(1e-3f * Mathf.Max(1f, expected.magnitude)),
                            $"trial {trial} node {n}: expected {expected:F4}, got {actual:F4}");
                    }
                }
                finally
                {
                    Object.DestroyImmediate(go);
                }
            }
        }

        // ---- outward facing (FR-6) ----

        /// <summary>Signed volume of a closed triangle mesh; positive when cross(b - a, c - a) points outward.</summary>
        static float SignedVolume(Vector3[] v, int[] tris)
        {
            float vol = 0f;
            for (int i = 0; i < tris.Length; i += 3)
                vol += Vector3.Dot(v[tris[i]], Vector3.Cross(v[tris[i + 1]], v[tris[i + 2]]));
            return vol / 6f;
        }

        static int[] FileTriangles(RoRsmNode node) => node.Faces.SelectMany(f => new[] { f.V0, f.V1, f.V2 }).ToArray();

        /// <summary>Unity triangles as the mesh builder makes them: reversed once, and once more for a mirroring A.</summary>
        static int[] UnityTriangles(RoRsmNode node, Matrix4x4 a)
        {
            var tris = FileTriangles(node);
            RoSpace.ReverseWinding(tris);
            if (RsmSpace.MirrorsWinding(a)) RoSpace.ReverseWinding(tris);
            return tris;
        }

        [Test]
        public void TestCube_IsOutwardInRagnarokSpace()
        {
            var cube = RsmTestModels.Cube();
            Assert.Greater(SignedVolume(cube.Vertices, FileTriangles(cube)), 0f);
        }

        [Test]
        public void Cube_StaysOutward_AfterConversion()
        {
            var cube = RsmTestModels.Cube();
            var a = RsmPose.VertexMatrix(cube, R14);
            var v = cube.Vertices.Select(p => RsmSpace.Vertex(a, p)).ToArray();
            Assert.IsFalse(RsmSpace.MirrorsWinding(a));
            Assert.Greater(SignedVolume(v, UnityTriangles(cube, a)), 0f);
        }

        [Test]
        public void Cube_StaysOutward_WithAMirroredOffsetMatrix()
        {
            var cube = RsmTestModels.Cube();
            cube.OffsetMatrix = new[] { -1f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f };
            var a = RsmPose.VertexMatrix(cube, R14);
            Assert.IsTrue(RsmSpace.MirrorsWinding(a));
            var v = cube.Vertices.Select(p => RsmSpace.Vertex(a, p)).ToArray();
            Assert.Greater(SignedVolume(v, UnityTriangles(cube, a)), 0f);
        }

        [Test]
        public void Cube_StaysOutward_WithAMirroredScale()
        {
            // A negative scale stays a negative localScale; Unity flips the culling for a negative determinant, so the
            // facing seen on screen is the signed volume in world space times the sign of that determinant.
            var cube = RsmTestModels.Cube();
            cube.Scale = new Vector3(-1f, 1f, 1f);
            var model = RsmTestModels.Model(cube);
            Assert.IsTrue(RsmHierarchy.TryBuild(model, out var tree, out _));
            var rest = RsmPose.ComputeRest(model, tree, R14);
            var go = new GameObject("mirrored");
            try
            {
                go.transform.localPosition = RsmSpace.LocalPosition(rest.Local[0]);
                go.transform.localRotation = RsmSpace.LocalRotation(rest.Local[0]);
                go.transform.localScale = RsmSpace.LocalScale(rest.Local[0]);
                Assert.AreEqual(-1f, go.transform.localScale.x);
                var a = rest.Vertex[0];
                var world = cube.Vertices.Select(p => go.transform.TransformPoint(RsmSpace.Vertex(a, p))).ToArray();
                float facing = SignedVolume(world, UnityTriangles(cube, a)) * Mathf.Sign(go.transform.localToWorldMatrix.determinant);
                Assert.Greater(facing, 0f);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
