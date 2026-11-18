using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Rsm;
using Uninarok.RoImport.Core.Space;

namespace Uninarok.RoImport.Tests.Rsm
{
    // TASKS-0003 T4.1 (FR-4): vertices that share position and texture coordinate are stored once (PLAN-0003 4.6).
    public class RsmMeshBuilderTests
    {
        static RsmMeshData Build(RoRsmNode node, RsmShadeType shade = RsmShadeType.Smooth, RoReport report = null) =>
            RsmMeshBuilder.Build(node, shade, true, Matrix4x4.identity, false, report ?? new RoReport());

        /// <summary>Distinct (position, uv) pairs of the file's face corners.</summary>
        static int DistinctPositionUv(RoRsmNode node)
        {
            var set = new HashSet<(Vector3, float, float)>();
            foreach (var f in node.Faces)
                foreach (var (v, t) in new[] { (f.V0, f.T0), (f.V1, f.T1), (f.V2, f.T2) })
                    set.Add((node.Vertices[v], node.TexCoords[t].U, node.TexCoords[t].V));
            return set.Count;
        }

        [Test]
        public void Cube_WithPerFaceUvs_Has24Vertices()
        {
            var mesh = Build(RsmTestModels.Cube());
            Assert.AreEqual(24, mesh.Positions.Length);
            Assert.AreEqual(12, mesh.TriangleCount);
        }

        [Test]
        public void Cube_WithSeamlessUvs_Has8Vertices()
        {
            var mesh = Build(RsmTestModels.Cube(perFaceUv: false));
            Assert.AreEqual(8, mesh.Positions.Length);
            Assert.AreEqual(12, mesh.TriangleCount);
        }

        [Test]
        public void CylinderStrip_HasTheDistinctPositionUvPairs()
        {
            var strip = RsmTestModels.CylinderStrip(8);
            var mesh = Build(strip);
            Assert.AreEqual(DistinctPositionUv(strip), mesh.Positions.Length);
            Assert.AreEqual(18, mesh.Positions.Length);   // 16 positions + the 2 seam positions with u = 1
        }

        [Test]
        public void EqualValuedVertices_AtDifferentFileIndices_Merge()
        {
            // Two triangles of a quad, each with its own copy of the shared edge's vertices.
            var node = new RoRsmNode
            {
                Name = "quad",
                TextureSlots = new[] { 0 },
                Vertices = new[] { V(0, 0, 0), V(1, 0, 0), V(1, 0, 1), V(0, 0, 0), V(1, 0, 1), V(0, 0, 1) },
                TexCoords = new[] { new RoRsmTexCoord(0, 0), new RoRsmTexCoord(1, 0), new RoRsmTexCoord(1, 1), new RoRsmTexCoord(0, 1) },
                Faces = new[] { new RoRsmFace(0, 1, 2, 0, 1, 2), new RoRsmFace(3, 4, 5, 0, 2, 3) }
            };
            Assert.AreEqual(4, Build(node).Positions.Length);
        }

        [Test]
        public void NegativeZero_WeldsWithZero()
        {
            var node = new RoRsmNode
            {
                Name = "z",
                TextureSlots = new[] { 0 },
                Vertices = new[] { V(0, 0, 0), V(1, 0, 0), V(0, 0, 1), V(-0f, 0, 0) },
                TexCoords = new[] { new RoRsmTexCoord(0, 0), new RoRsmTexCoord(1, 0), new RoRsmTexCoord(0, 1) },
                Faces = new[] { new RoRsmFace(0, 1, 2, 0, 1, 2), new RoRsmFace(3, 2, 1, 0, 2, 1) }
            };
            Assert.AreEqual(3, Build(node).Positions.Length);
        }

        [Test]
        public void DuplicateFaces_AreKept()
        {
            var node = RsmTestModels.Cube();
            node.Faces = node.Faces.Concat(new[] { node.Faces[0] }).ToArray();
            Assert.AreEqual(13, Build(node).TriangleCount);
        }

        [Test]
        public void DegenerateFaces_AreKept()
        {
            var node = RsmTestModels.Cube();
            node.Faces = node.Faces.Concat(new[] { new RoRsmFace(0, 0, 1, 0, 0, 1) }).ToArray();
            var mesh = Build(node);
            Assert.AreEqual(13, mesh.TriangleCount);
            Assert.IsTrue(mesh.Normals.All(n => Mathf.Abs(n.magnitude - 1f) < 1e-4f), "a zero-area face adds nothing to the normals");
        }

        [Test]
        public void Indices_Are16BitBelow65536Vertices_And32BitAbove()
        {
            Assert.IsFalse(Build(RsmTestModels.Cube()).Needs32BitIndices);

            // 22,000 separate triangles with their own positions: 66,000 vertices.
            const int triangles = 22000;
            var vertices = new Vector3[triangles * 3];
            var faces = new RoRsmFace[triangles];
            for (int i = 0; i < triangles; i++)
            {
                vertices[3 * i] = V(i, 0, 0);
                vertices[3 * i + 1] = V(i, 1, 0);
                vertices[3 * i + 2] = V(i, 0, 1);
                faces[i] = new RoRsmFace(3 * i, 3 * i + 1, 3 * i + 2, 0, 0, 0);
            }
            var large = new RoRsmNode
            {
                Name = "large",
                TextureSlots = new[] { 0 },
                Vertices = vertices,
                TexCoords = new[] { new RoRsmTexCoord(0, 0) },
                Faces = faces
            };
            var mesh = Build(large);
            Assert.AreEqual(66000, mesh.Positions.Length);
            Assert.IsTrue(mesh.Needs32BitIndices);
            Assert.AreEqual(66000 - 1, mesh.Submeshes.SelectMany(s => s.Triangles).Max());
        }

        [Test]
        public void Uv_IsUAndOneMinusV()
        {
            var node = RsmTestModels.Cube(perFaceUv: false);
            node.TexCoords = node.TexCoords.Select((t, i) => new RoRsmTexCoord(0.1f * i, 0.05f * i)).ToArray();
            var mesh = Build(node);
            for (int i = 0; i < mesh.Positions.Length; i++)
            {
                // Find the file vertex at this (converted) position; with seamless uv its index is its texcoord index.
                int file = System.Array.FindIndex(node.Vertices, v => (RoSpace.Point(v) - mesh.Positions[i]).sqrMagnitude < 1e-10f);
                Assert.AreEqual(new Vector2(0.1f * file, 1f - 0.05f * file), mesh.Uvs[i]);
            }
        }

        [Test]
        public void Positions_AreConvertedThroughTheVertexMatrixAndRoSpace()
        {
            var node = RsmTestModels.Cube(perFaceUv: false);
            var a = Matrix4x4.TRS(V(1, 2, 3), Quaternion.Euler(10, 20, 30), V(2, 2, 2));
            var mesh = RsmMeshBuilder.Build(node, RsmShadeType.Smooth, true, a, false, new RoReport());
            var expected = node.Vertices.Select(v => RoSpace.Point(a.MultiplyPoint3x4(v))).ToList();
            foreach (var p in mesh.Positions)
                Assert.IsTrue(expected.Any(e => (e - p).sqrMagnitude < 1e-10f), p.ToString("F4"));
        }

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
    }
}
