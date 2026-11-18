using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Rsm;
using Uninarok.RoImport.Core.Space;

namespace Uninarok.RoImport.Tests.Rsm
{
    // TASKS-0003 T4.2 (FR-5): normals by shade type and smoothing group (PLAN-0003 4.6).
    [Ignore("pending T4.3")]
    public class RsmShadingTests
    {
        const float Eps = 1e-4f;

        static RsmMeshData Build(RoRsmNode node, RsmShadeType shade, bool hasShadeType = true) =>
            RsmMeshBuilder.Build(node, shade, hasShadeType, Matrix4x4.identity, false, new RoReport());

        static int[] At(RsmMeshData mesh, Vector3 roPosition)
        {
            var p = RoSpace.Point(roPosition);
            return Enumerable.Range(0, mesh.Positions.Length).Where(i => (mesh.Positions[i] - p).sqrMagnitude < 1e-10f).ToArray();
        }

        static void AreEqual(Vector3 expected, Vector3 actual, string what = null) =>
            Assert.That((expected - actual).magnitude, Is.LessThan(Eps), $"{what}: expected {expected:F4}, got {actual:F4}");

        [Test]
        public void Smooth_NormalsAreEqualOnBothSidesOfAUvSeam()
        {
            var strip = RsmTestModels.CylinderStrip(8);
            var mesh = Build(strip, RsmShadeType.Smooth);
            var seam = At(mesh, strip.Vertices[0]);
            Assert.AreEqual(2, seam.Length, "the seam position has two uvs, so two vertices");
            AreEqual(mesh.Normals[seam[0]], mesh.Normals[seam[1]], "seam normals");
            // and the normal points outward, horizontally: (cos 0, 0, sin 0) = +X
            AreEqual(Vector3.right, mesh.Normals[seam[0]], "outward");
        }

        [Test]
        public void Smooth_AngleWeighting_MakesTheNormalIndependentOfTriangulation()
        {
            // A cube corner (0,0,0) with seamless uvs. Variant B splits the z = 0 side along the other diagonal, so that
            // the corner touches one triangle of that side instead of two. Angle-weighted, the corner normal is the same.
            var a = RsmTestModels.Cube(perFaceUv: false);
            var b = RsmTestModels.Cube(perFaceUv: false);
            var faces = b.Faces.ToArray();
            // side 0 is quad (0,3,2,1): faces (0,3,2), (0,2,1). Other diagonal: (3,2,1), (3,1,0).
            faces[0] = new RoRsmFace(3, 2, 1, 3, 2, 1);
            faces[1] = new RoRsmFace(3, 1, 0, 3, 1, 0);
            b.Faces = faces;

            var na = Build(a, RsmShadeType.Smooth).Normals[At(Build(a, RsmShadeType.Smooth), Vector3.zero)[0]];
            var meshB = Build(b, RsmShadeType.Smooth);
            var nb = meshB.Normals[At(meshB, Vector3.zero)[0]];
            AreEqual(na, nb, "corner normal");
            // the corner is symmetric: the normal points away from the cube along the diagonal
            AreEqual(RoSpace.Direction(new Vector3(-1, -1, -1)).normalized, na, "diagonal");
        }

        [Test]
        public void Flat_EachFaceKeepsItsNormal_AndOnlyCornersAreDuplicated()
        {
            var cube = RsmTestModels.Cube(perFaceUv: false);
            var mesh = Build(cube, RsmShadeType.Flat);
            Assert.AreEqual(36, mesh.Positions.Length, "every face corner is its own vertex");
            foreach (var s in mesh.Submeshes)
                for (int i = 0; i < s.Triangles.Length; i += 3)
                {
                    var a = mesh.Positions[s.Triangles[i]];
                    var b = mesh.Positions[s.Triangles[i + 1]];
                    var c = mesh.Positions[s.Triangles[i + 2]];
                    var faceNormal = Vector3.Cross(b - a, c - a).normalized;
                    for (int k = 0; k < 3; k++) AreEqual(faceNormal, mesh.Normals[s.Triangles[i + k]], "flat normal");
                }
        }

        [Test]
        public void Flat_NormalsPointOutward()
        {
            var cube = RsmTestModels.Cube(perFaceUv: false);
            var mesh = Build(cube, RsmShadeType.Flat);
            var center = RoSpace.Point(new Vector3(0.5f, 0.5f, 0.5f));
            for (int i = 0; i < mesh.Positions.Length; i++)
                Assert.Greater(Vector3.Dot(mesh.Normals[i], mesh.Positions[i] - center), 0f, $"vertex {i}");
        }

        [Test]
        public void TwoSmoothingGroups_KeepAHardEdge()
        {
            // The top side (y = 0 in Ragnarok space, faces 6 and 7 of the cube: quad (0,1,5,4)) is in group 1.
            var cube = RsmTestModels.Cube(perFaceUv: false);
            var faces = cube.Faces.ToArray();
            faces[4].SmoothGroup = 1;
            faces[5].SmoothGroup = 1;
            cube.Faces = faces;
            var mesh = Build(cube, RsmShadeType.Smooth);
            var corner = At(mesh, Vector3.zero);
            Assert.AreEqual(2, corner.Length, "one vertex per group at the shared corner");
            Assert.Less(Vector3.Dot(mesh.Normals[corner[0]], mesh.Normals[corner[1]]), 0.99f, "the edge stays hard");
            AreEqual(Vector3.up, mesh.Normals.Where((n, i) => corner.Contains(i)).OrderByDescending(n => n.y).First(), "group 1 is the flat top");
        }

        [Test]
        public void None_GivesOneConstantNormal_Up()
        {
            var mesh = Build(RsmTestModels.Cube(perFaceUv: false), RsmShadeType.None);
            Assert.AreEqual(8, mesh.Positions.Length);
            foreach (var n in mesh.Normals) AreEqual(Vector3.up, n);
        }

        [Test]
        public void VersionWithoutShadeType_IsSmooth()
        {
            var cube = RsmTestModels.Cube(perFaceUv: false);
            var withoutType = Build(cube, RsmShadeType.Flat, hasShadeType: false);
            var smooth = Build(cube, RsmShadeType.Smooth);
            Assert.AreEqual(smooth.Positions.Length, withoutType.Positions.Length);
            CollectionAssert.AreEqual(smooth.Normals, withoutType.Normals);
            Assert.AreEqual(RsmShadeType.Smooth, RsmShading.Effective(RsmShadeType.None, false));
            Assert.AreEqual(RsmShadeType.Flat, RsmShading.Effective(RsmShadeType.Flat, true));
        }

        [Test]
        public void SameGroup_IsIntegerEquality()
        {
            Assert.IsTrue(RsmShading.SameGroup(3, 3));
            Assert.IsFalse(RsmShading.SameGroup(1, 3), "group ids, not bit masks (T0.2)");
        }

        [Test]
        public void Normals_AreUnitLength()
        {
            foreach (var shade in new[] { RsmShadeType.None, RsmShadeType.Flat, RsmShadeType.Smooth })
            foreach (var n in Build(RsmTestModels.CylinderStrip(6), shade).Normals)
                Assert.AreEqual(1f, n.magnitude, Eps, shade.ToString());
        }
    }
}
