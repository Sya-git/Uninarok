using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Formats;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Rsm;
using Uninarok.RoImport.Core.Space;

namespace Uninarok.RoImport.Tests.Rsm
{
    // TASKS-0003 T3.3 (FR-2): the four reported models and the control group (reference-models.md §1, §2) against the
    // R1 reference values recorded in PLAN-0003 4.4 ("R1 reference values"), computed by the T0.3 scratch code that
    // matched the reference viewers. Ragnarok space, after recentering.
    [Category(RsmTestData.Category)]
    public class RsmReferenceTests
    {
        const float Eps = 1e-4f;

        public sealed class Expected
        {
            public string Model;
            public Vector3 Position, Scale, Min, Max;
            public Quaternion Rotation;
            public override string ToString() => Model;
        }

        static Expected E(string model, Vector3 position, Quaternion rotation, Vector3 scale, Vector3 min, Vector3 max) =>
            new Expected { Model = model, Position = position, Rotation = rotation, Scale = scale, Min = min, Max = max };

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        static Quaternion Q(float x, float y, float z, float w) => new Quaternion(x, y, z, w);

        static Expected[] RestValues() => new[]
        {
            // §1 reported wrong
            E("나무잡초꽃/버섯02.rsm", V(0.046991f, 1.665913f, -0.145184f), Q(0f, -0.923878f, 0f, -0.382688f), V(0.0901f, 0.0901f, 0.0901f), V(-4.358802f, -5.739874f, -3.774851f), V(4.358802f, 0f, 3.774849f)),
            E("나무잡초꽃/버섯01.rsm", V(0.046991f, 1.665913f, -0.145184f), Q(0f, -0.923878f, 0f, -0.382688f), V(0.0901f, 0.0901f, 0.0901f), V(-4.358802f, -5.739874f, -3.774851f), V(4.358802f, 0f, 3.774849f)),
            E("나무잡초꽃/넝쿨01.rsm", V(0.226999f, -40.2408f, -0.000001f), Q(0.707108f, 0f, 0f, 0.707106f), V(1f, 1f, 1f), V(-20f, -80f, -0.000148f), V(20f, 0f, 0.000148f)),
            E("나무잡초꽃/덤불02.rsm", V(-8.305627f, 17.98348f, -3.509289f), Q(0.02982f, -0.208807f, 0.056931f, 0.975843f), V(0.1811f, 0.1811f, 0.1811f), V(-25.04809f, -14.98955f, -24.90288f), V(25.04809f, 0f, 24.90288f)),
            // §2 control group
            E("나무잡초꽃/덤불01.rsm", V(-8.305627f, 19.50137f, -1.240438f), Q(0.02982f, -0.208807f, 0.056931f, 0.975843f), V(0.1811f, 0.1811f, 0.1811f), V(-25.04809f, -13.47166f, -22.63403f), V(25.04809f, 0f, 22.63403f)),
            E("나무잡초꽃/야자수01.rsm", V(4.419601f, 0f, -8.387604f), Q(0f, -0.500001f, 0f, 0.866025f), V(1f, 1f, 1f), V(-21.95475f, -67.95193f, -21.04791f), V(21.95475f, 0f, 21.04791f)),
            E("프론테라/성_기둥.rsm", V(0.155977f, -193.0966f, 3.696754f), Q(0f, 0.831471f, 0f, 0.555568f), V(0.2658f, 0.2395f, 0.2658f), V(-17.86841f, -102.102f, -19.08291f), V(17.86841f, 0f, 19.08291f)),
            E("프론테라/휘장.rsm", V(2.689514f, -7.367931f, -0.000003f), Q(0.707108f, 0f, 0f, 0.707106f), V(0.6908f, 0.6908f, 0.6908f), V(-5.785612f, -24.3201f, -0.000045f), V(5.785612f, 0f, 0.000045f)),
            E("외부소품/이정표01.rsm", V(-0.183999f, -15.70174f, 6.458787f), Q(0.707108f, 0f, 0f, 0.707106f), V(1f, 1f, 1f), V(-21.9134f, -24.42023f, -3.504345f), V(21.9134f, 0f, 3.504345f)),
        };

        static void Near(Vector3 expected, Vector3 actual, string what) =>
            Assert.That((expected - actual).magnitude, Is.LessThan(Eps), $"{what}: expected {expected:F6}, got {actual:F6}");

        [TestCaseSource(nameof(RestValues))]
        public void RootRestTransformAndBounds_MatchTheRecordedValues(Expected e)
        {
            byte[] bytes = RsmTestData.ModelBytes(e.Model);
            Assert.IsTrue(RoRsmReader.TryRead(RoPath.Parse("model/" + e.Model), bytes, out var rsm, out var reason), reason);
            Assert.IsTrue(RsmRules.TryGet(rsm.VersionMajor, rsm.VersionMinor, out var rules));
            Assert.IsTrue(RsmHierarchy.TryBuild(rsm, out var tree, out var issue), issue?.ToString());
            var rest = RsmPose.ComputeRest(rsm, tree, rules);

            Assert.AreEqual(1, tree.Roots.Count);
            var root = rest.Local[tree.Roots[0]];
            Near(e.Position, root.Position, "root position");
            Assert.That(Mathf.Abs(Quaternion.Dot(e.Rotation.normalized, root.Rotation)), Is.GreaterThan(1f - Eps), $"root rotation {root.Rotation:F6}");
            Near(e.Scale, root.Scale, "root scale");
            Near(e.Min, rest.BoundsMin, "bounds min");
            Near(e.Max, rest.BoundsMax, "bounds max");

            // The converted transform is RoSpace of the same values (4.5).
            Near(RoSpace.Point(e.Position), RsmSpace.LocalPosition(root), "converted position");
            Near(e.Scale, RsmSpace.LocalScale(root), "converted scale");
        }

        // ---- part 2 (T4.4, FR-4, FR-5): meshes of the real models ----

        static IEnumerable<string> AllReferenceModels() => RsmTestData.ReferenceModels();

        sealed class Built
        {
            public RoRsm Rsm;
            public RsmRestPose Rest;
            public RsmMeshData[] Meshes;
            public RoReport Report;
        }

        static Built BuildAll(string model)
        {
            byte[] bytes = RsmTestData.ModelBytes(model);
            Assert.IsTrue(RoRsmReader.TryRead(RoPath.Parse("model/" + model), bytes, out var rsm, out var reason), reason);
            Assert.IsTrue(RsmRules.TryGet(rsm.VersionMajor, rsm.VersionMinor, out var rules));
            Assert.IsTrue(RsmHierarchy.TryBuild(rsm, out var tree, out var issue), issue?.ToString());
            var rest = RsmPose.ComputeRest(rsm, tree, rules);
            var report = new RoReport();
            var meshes = new RsmMeshData[rsm.Nodes.Count];
            for (int n = 0; n < meshes.Length; n++)
                meshes[n] = RsmMeshBuilder.Build(rsm.Nodes[n], rsm.ShadeType, rsm.HasShadeType, rest.Vertex[n],
                                                 RsmSpace.MirrorsWinding(rest.Vertex[n]), report, model);
            return new Built { Rsm = rsm, Rest = rest, Meshes = meshes, Report = report };
        }

        static float Z(float f) => f == 0f ? 0f : f;

        [TestCaseSource(nameof(AllReferenceModels))]
        public void Meshes_HaveFewerVerticesThanThreePerFace_AndEveryTriangle(string model)
        {
            var b = BuildAll(model);
            int faces = b.Rsm.Nodes.Sum(n => n.Faces.Length);
            int vertices = b.Meshes.Sum(m => m.Positions.Length);
            Assert.AreEqual(faces, b.Meshes.Sum(m => m.TriangleCount), "triangle count");
            Assert.Less(vertices, 3 * faces, "vertex count against the old importer's 3 per face");
            CollectionAssert.IsEmpty(b.Report.Issues, "no bad index in the reference data");
            TestContext.Out.WriteLine($"{model}: {vertices} vertices for {faces} faces (old importer: {3 * faces})");
        }

        [TestCaseSource(nameof(AllReferenceModels))]
        public void VertexCount_IsTheDistinctPositionUvPairs_PerNormalGroup(string model)
        {
            // FR-4 with D3: one vertex per distinct (position, uv); a hard edge adds exactly the vertices whose normal
            // group differs (smoothing group for smooth models, the face for flat ones, nothing for none).
            var b = BuildAll(model);
            var shade = RsmShading.Effective(b.Rsm.ShadeType, b.Rsm.HasShadeType);
            int expected = 0, pairs = 0;
            for (int n = 0; n < b.Rsm.Nodes.Count; n++)
            {
                var node = b.Rsm.Nodes[n];
                var keyed = new HashSet<(float, float, float, float, float, (int, int, int))>();
                var plain = new HashSet<(float, float, float, float, float)>();
                for (int fi = 0; fi < node.Faces.Length; fi++)
                {
                    var f = node.Faces[fi];
                    // normal group: the smoothing group (smooth), the rounded face normal (flat, D11), none (none)
                    var a = RsmSpace.Vertex(b.Rest.Vertex[n], node.Vertices[f.V0]);
                    var faceNormal = Vector3.Cross(RsmSpace.Vertex(b.Rest.Vertex[n], node.Vertices[f.V2]) - a,
                                                   RsmSpace.Vertex(b.Rest.Vertex[n], node.Vertices[f.V1]) - a).normalized;
                    var group = shade == RsmShadeType.Flat ? RsmShading.FlatKey(faceNormal)
                              : shade == RsmShadeType.Smooth ? (f.SmoothGroup, 0, 0) : (0, 0, 0);
                    foreach (var (v, t) in new[] { (f.V0, f.T0), (f.V1, f.T1), (f.V2, f.T2) })
                    {
                        var p = RsmSpace.Vertex(b.Rest.Vertex[n], node.Vertices[v]);
                        float u = Z(node.TexCoords[t].U), w = Z(1f - node.TexCoords[t].V);
                        keyed.Add((Z(p.x), Z(p.y), Z(p.z), u, w, group));
                        plain.Add((Z(p.x), Z(p.y), Z(p.z), u, w));
                    }
                }
                expected += keyed.Count;
                pairs += plain.Count;
            }
            int vertices = b.Meshes.Sum(m => m.Positions.Length);
            Assert.AreEqual(expected, vertices, "distinct (position, uv, normal group)");
            bool oneGroup = b.Rsm.Nodes.SelectMany(n => n.Faces).Select(f => f.SmoothGroup).Distinct().Count() <= 1;
            if (shade == RsmShadeType.None || (shade == RsmShadeType.Smooth && oneGroup))
                Assert.AreEqual(pairs, vertices, "exactly the distinct (position, uv) pairs");
        }

        [TestCase("izlude/iz_enchant.rsm")]
        [TestCase("외부소품/이즈루드초소.rsm")]
        public void ShadeNone_HasOneConstantNormal(string model)
        {
            var b = BuildAll(model);
            Assert.AreEqual(RsmShadeType.None, b.Rsm.ShadeType);
            foreach (var m in b.Meshes)
                foreach (var n in m.Normals)
                    Assert.That((n - Vector3.up).magnitude, Is.LessThan(1e-6f));
        }

        [TestCase("나무잡초꽃/넝쿨01.rsm")]
        [TestCase("외부소품/동굴입구.rsm")]
        [TestCase("izlude/iz_bridge01.rsm")]
        public void ShadeFlat_EveryCornerHasItsFacesNormal(string model)
        {
            var b = BuildAll(model);
            Assert.AreEqual(RsmShadeType.Flat, b.Rsm.ShadeType);
            foreach (var m in b.Meshes)
                foreach (var s in m.Submeshes)
                    for (int i = 0; i < s.Triangles.Length; i += 3)
                    {
                        var p0 = m.Positions[s.Triangles[i]];
                        var cross = Vector3.Cross(m.Positions[s.Triangles[i + 1]] - p0, m.Positions[s.Triangles[i + 2]] - p0);
                        if (cross.sqrMagnitude < 1e-12f) continue;   // degenerate faces keep a default normal
                        for (int k = 0; k < 3; k++)
                            Assert.That((m.Normals[s.Triangles[i + k]] - cross.normalized).magnitude, Is.LessThan(1e-3f));
                    }
        }

        [TestCase("나무잡초꽃/야자수01.rsm")]
        [TestCase("프론테라/성_기둥.rsm")]
        public void ShadeSmooth_SharesNormalsAcrossUvSeams(string model)
        {
            // Vertices at one position with different uvs and one shared normal: a texture seam without a lighting seam.
            var b = BuildAll(model);
            Assert.AreEqual(RsmShadeType.Smooth, b.Rsm.ShadeType);
            int seams = 0;
            foreach (var m in b.Meshes)
            {
                foreach (var g in Enumerable.Range(0, m.Positions.Length).GroupBy(i => m.Positions[i]).Where(g => g.Count() > 1))
                    if (g.Select(i => m.Uvs[i]).Distinct().Count() == g.Count() && g.Select(i => m.Normals[i]).Distinct().Count() == 1)
                        seams++;
                foreach (var nrm in m.Normals) Assert.AreEqual(1f, nrm.magnitude, 1e-4f);
            }
            Assert.Greater(seams, 0);
        }
    }
}
