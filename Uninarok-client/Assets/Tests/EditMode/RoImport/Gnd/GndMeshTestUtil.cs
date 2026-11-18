using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Uninarok.RoImport.Core.Gnd;
using Uninarok.RoImport.Core.Results;

namespace Uninarok.RoImport.Tests.Gnd
{
    /// <summary>Helpers shared by the ground mesh tests (TASKS-0004 T4.1, T4.2).</summary>
    public static class GndMeshTestUtil
    {
        /// <summary>A triangle of a built mesh in Unity world space (chunk origin added).</summary>
        public readonly struct WorldTriangle
        {
            public readonly Vector3 A, B, C;
            public readonly int Texture, Chunk;
            public readonly int IndexA, IndexB, IndexC;

            public WorldTriangle(Vector3 a, Vector3 b, Vector3 c, int texture, int chunk, int ia, int ib, int ic)
            {
                A = a; B = b; C = c; Texture = texture; Chunk = chunk; IndexA = ia; IndexB = ib; IndexC = ic;
            }

            /// <summary>The geometric normal of the winding: Unity's front face is the one this points toward.</summary>
            public Vector3 Normal => Vector3.Cross(B - A, C - A);

            public override string ToString() => $"{A} {B} {C}";
        }

        public static IReadOnlyList<RoGndMeshData> Build(RoGnd gnd, RoReport report = null)
        {
            Assert.IsTrue(RoGndRules.TryGet(gnd.VersionMajor, gnd.VersionMinor, out var rules));
            return RoGndMeshBuilder.Build(gnd, rules, report ?? new RoReport());
        }

        public static List<WorldTriangle> WorldTriangles(IReadOnlyList<RoGndMeshData> meshes)
        {
            var result = new List<WorldTriangle>();
            for (int m = 0; m < meshes.Count; m++)
            {
                var mesh = meshes[m];
                foreach (var sub in mesh.Submeshes)
                    for (int i = 0; i < sub.Triangles.Length; i += 3)
                    {
                        int a = sub.Triangles[i], b = sub.Triangles[i + 1], c = sub.Triangles[i + 2];
                        result.Add(new WorldTriangle(mesh.Origin + mesh.Positions[a], mesh.Origin + mesh.Positions[b],
                            mesh.Origin + mesh.Positions[c], sub.Texture, m, a, b, c));
                    }
            }
            return result;
        }

        /// <summary>
        /// A 20 x 18 map (2 x 2 chunks) with a continuous uneven height field (non-planar tops, every shared corner equal)
        /// and a 15-unit cliff along the row edge 9 that crosses the chunk border. Every cube has a top and wall tiles.
        /// </summary>
        public static RoGnd Terrain() =>
            GndTestMaps.Build(20, 18, (c, r, dx, dz) => Field(c + dx, r + dz) + (r >= 9 ? -15f : 0f));

        /// <summary>Integer heights, so corners convert exactly and equal corners are equal bit for bit.</summary>
        static float Field(int x, int z) => -((x * 7 + z * 3) % 5);

        /// <summary>The float bits of a position, -0 folded into 0, for exact comparisons.</summary>
        public static (int, int, int) Bits(Vector3 v) => (Bits(v.x), Bits(v.y), Bits(v.z));

        static int Bits(float f) => f == 0f ? 0 : BitConverter.SingleToInt32Bits(f);
    }
}
