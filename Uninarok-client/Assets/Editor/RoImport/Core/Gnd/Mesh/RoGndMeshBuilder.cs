using System;
using System.Collections.Generic;
using UnityEngine;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Space;

namespace Uninarok.RoImport.Core.Gnd
{
    /// <summary>
    /// Builds the chunk meshes of a ground (PLAN-0004 4.4). Pure: arrays in, arrays out.
    /// <list type="number">
    /// <item>The quads of <see cref="RoGndSurfaces"/> are converted to Unity space and split into triangles: tops on the
    /// rules' diagonal, walls on the same diagonal oriented toward their lower side (T0.3). Zero-area triangles are
    /// dropped.</item>
    /// <item>Normals are computed on the whole map before the cut (FR-7, D12): tops are one smoothing group, merged by
    /// exact world position across texture seams and chunk borders, with angle-weighted face normals; walls are flat,
    /// grouped by their face normal rounded to 1e-4.</item>
    /// <item>Each chunk merges equal (position, uv, normal group) vertices, groups triangles into one submesh per texture
    /// in ascending order, and stores positions relative to its origin. Winding is reversed once per submesh with
    /// <see cref="RoSpace.ReverseWinding"/>.</item>
    /// </list>
    /// </summary>
    public static class RoGndMeshBuilder
    {
        /// <summary>Cubes per chunk edge.</summary>
        public const int ChunkSize = RoGndChunker.ChunkSize;

        /// <summary>The scale of the rounding of a wall's normal group key (PLAN-0003 D11).</summary>
        const float GroupRounding = 1e4f;

        /// <summary>The normal group of a corner: tops share one group; a wall's group is its rounded face normal.</summary>
        readonly struct Group : IEquatable<Group>
        {
            public readonly bool Top;
            public readonly int X, Y, Z;

            Group(bool top, int x, int y, int z) { Top = top; X = x; Y = y; Z = z; }

            public static Group ForTop => new Group(true, 0, 0, 0);

            public static Group ForWall(Vector3 n) => new Group(false,
                Mathf.RoundToInt(n.x * GroupRounding), Mathf.RoundToInt(n.y * GroupRounding), Mathf.RoundToInt(n.z * GroupRounding));

            public bool Equals(Group o) => Top == o.Top && X == o.X && Y == o.Y && Z == o.Z;
            public override bool Equals(object obj) => obj is Group g && Equals(g);
            public override int GetHashCode() => Top ? 1 : ((X * 397) ^ (Y * 31) ^ Z) * 2;
        }

        /// <summary>A triangle corner in Unity world space.</summary>
        readonly struct Corner
        {
            public readonly Vector3 World;
            public readonly Vector2 Uv;

            public Corner(Vector3 world, Vector2 uv) { World = world; Uv = uv; }
        }

        /// <summary>A triangle in base order (a, b, c): the mesh draws it as (a, c, b) after the one winding reversal.</summary>
        readonly struct Triangle
        {
            public readonly int ChunkColumn, ChunkRow, Texture;
            public readonly Corner A, B, C;
            public readonly Group Group;
            /// <summary>The unit normal of the drawn (reversed) winding.</summary>
            public readonly Vector3 Normal;

            public Triangle(int cx, int cz, int texture, Corner a, Corner b, Corner c, Group group, Vector3 normal)
            {
                ChunkColumn = cx; ChunkRow = cz; Texture = texture; A = a; B = b; C = c; Group = group; Normal = normal;
            }
        }

        public static IReadOnlyList<RoGndMeshData> Build(RoGnd gnd, RoGndRules rules, RoReport report)
        {
            if (gnd == null) throw new ArgumentNullException(nameof(gnd));

            var triangles = new List<Triangle>();
            foreach (var quad in RoGndSurfaces.Enumerate(gnd, rules, report))
                AddQuad(gnd, rules, quad, triangles);

            var topNormals = TopNormals(triangles);
            return Chunks(gnd, triangles, topNormals);
        }

        // ---- 1. quads to triangles -------------------------------------------------------------------------------

        static void AddQuad(RoGnd gnd, RoGndRules rules, RoGndQuad quad, List<Triangle> triangles)
        {
            if (quad.Tile < 0 || quad.Tile >= gnd.Tiles.Count) return;
            var tile = gnd.Tiles[quad.Tile];
            if (tile.Texture < 0 || tile.Texture >= gnd.Textures.Count) return;   // no texture: drawn as none (reader, D13)

            var c00 = new Corner(RoSpace.Point(quad.P00), Uv(tile.Uv00));
            var c10 = new Corner(RoSpace.Point(quad.P10), Uv(tile.Uv10));
            var c01 = new Corner(RoSpace.Point(quad.P01), Uv(tile.Uv01));
            var c11 = new Corner(RoSpace.Point(quad.P11), Uv(tile.Uv11));
            var (cx, cz) = RoGndChunker.ChunkOf(quad.Column, quad.Row);

            if (quad.Face == RoGndFace.Top)
            {
                // Base order (00,10,01), (10,11,01) on diagonal H10-H01 draws facing up after the reversal.
                if (rules.Diagonal == RoGndDiagonal.H10ToH01)
                {
                    Add(triangles, cx, cz, tile.Texture, c00, c10, c01, Group.ForTop);
                    Add(triangles, cx, cz, tile.Texture, c10, c11, c01, Group.ForTop);
                }
                else
                {
                    Add(triangles, cx, cz, tile.Texture, c00, c10, c11, Group.ForTop);
                    Add(triangles, cx, cz, tile.Texture, c00, c11, c01, Group.ForTop);
                }
                return;
            }

            // Walls face the side whose edge is lower (T0.3): this cube's edge is P00/P10, the neighbour's P01/P11.
            // Ragnarok Y points down, so the larger sum is the lower edge.
            bool thisLower = quad.P00.y + quad.P10.y > quad.P01.y + quad.P11.y;
            Vector3 towardNext = quad.Face == RoGndFace.FrontWall ? new Vector3(0, 0, RoMapSpace.RowDirection) : Vector3.right;
            var facing = thisLower ? -towardNext : towardNext;

            // The drawn normal of base order (00,10,01) is Cross(p01 - p00, p10 - p00) (reversed winding); pick the base
            // order whose drawn normal points along facing.
            var n = DrawnNormal(c00.World, c10.World, c01.World);
            if (n == Vector3.zero) n = DrawnNormal(c10.World, c11.World, c01.World);
            bool keep = Vector3.Dot(n, facing) >= 0f;
            var group = Group.ForWall(facing);                   // a wall lies in an axis-aligned plane: its normal is its facing
            if (keep)
            {
                Add(triangles, cx, cz, tile.Texture, c00, c10, c01, group);
                Add(triangles, cx, cz, tile.Texture, c10, c11, c01, group);
            }
            else
            {
                Add(triangles, cx, cz, tile.Texture, c00, c01, c10, group);
                Add(triangles, cx, cz, tile.Texture, c10, c01, c11, group);
            }
        }

        static void Add(List<Triangle> triangles, int cx, int cz, int texture, Corner a, Corner b, Corner c, Group group)
        {
            var n = DrawnNormal(a.World, b.World, c.World);
            if (n == Vector3.zero) return;                                       // zero area (a wall closing at one end)
            triangles.Add(new Triangle(cx, cz, texture, a, b, c, group, n.normalized));
        }

        /// <summary>The normal of base order (a, b, c) as drawn after the winding reversal, i.e. of (a, c, b).</summary>
        static Vector3 DrawnNormal(Vector3 a, Vector3 b, Vector3 c) => Vector3.Cross(c - a, b - a);

        /// <summary>Tile coordinates to Unity texture space: V points down in the file (T0.3).</summary>
        static Vector2 Uv(Vector2 file) => new Vector2(file.x, 1f - file.y);

        // ---- 2. map-wide smooth normals of the tops ----------------------------------------------------------------

        static Dictionary<(float, float, float), Vector3> TopNormals(List<Triangle> triangles)
        {
            var sums = new Dictionary<(float, float, float), Vector3>();
            foreach (var t in triangles)
            {
                if (!t.Group.Top) continue;
                // Drawn order (a, c, b); the corner angles do not depend on the order.
                Accumulate(sums, t.A.World, t.B.World, t.C.World, t.Normal);
                Accumulate(sums, t.B.World, t.C.World, t.A.World, t.Normal);
                Accumulate(sums, t.C.World, t.A.World, t.B.World, t.Normal);
            }

            var result = new Dictionary<(float, float, float), Vector3>(sums.Count);
            foreach (var pair in sums)
                result[pair.Key] = pair.Value.sqrMagnitude > 0f ? pair.Value.normalized : Vector3.up;
            return result;
        }

        static void Accumulate(Dictionary<(float, float, float), Vector3> sums, Vector3 at, Vector3 p, Vector3 q, Vector3 normal)
        {
            float angle = Angle(p - at, q - at);
            var key = Exact(at);
            sums.TryGetValue(key, out var sum);
            sums[key] = sum + normal * angle;
        }

        static float Angle(Vector3 u, Vector3 v)
        {
            float d = Vector3.Dot(u.normalized, v.normalized);
            return Mathf.Acos(Mathf.Clamp(d, -1f, 1f));
        }

        // ---- 3. chunks ------------------------------------------------------------------------------------------------

        static IReadOnlyList<RoGndMeshData> Chunks(RoGnd gnd, List<Triangle> triangles, Dictionary<(float, float, float), Vector3> topNormals)
        {
            int columns = RoGndChunker.ChunksAlong(gnd.Width), rows = RoGndChunker.ChunksAlong(gnd.Height);
            var perChunk = new List<Triangle>[columns * rows];
            foreach (var t in triangles)
            {
                int i = t.ChunkRow * columns + t.ChunkColumn;
                (perChunk[i] ??= new List<Triangle>()).Add(t);
            }

            var space = new RoMapSpace(gnd.Width, gnd.Height, gnd.Zoom);
            var result = new List<RoGndMeshData>();
            for (int cz = 0; cz < rows; cz++)
            for (int cx = 0; cx < columns; cx++)
            {
                var list = perChunk[cz * columns + cx];
                if (list != null) result.Add(Chunk(cx, cz, space.CornerUnity(cx * ChunkSize, cz * ChunkSize, 0f), list, topNormals));
            }
            return result;
        }

        static RoGndMeshData Chunk(int cx, int cz, Vector3 origin, List<Triangle> triangles, Dictionary<(float, float, float), Vector3> topNormals)
        {
            var index = new Dictionary<((float, float, float), (float, float), Group), int>();
            var positions = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var submeshes = new SortedDictionary<int, List<int>>();

            int Vertex(Corner corner, Group group, Vector3 wallNormal)
            {
                var key = (Exact(corner.World), (Exact(corner.Uv.x), Exact(corner.Uv.y)), group);
                if (index.TryGetValue(key, out int i)) return i;
                i = positions.Count;
                index[key] = i;
                positions.Add(corner.World - origin);
                normals.Add(group.Top ? topNormals[Exact(corner.World)] : wallNormal);
                uvs.Add(corner.Uv);
                return i;
            }

            foreach (var t in triangles)
            {
                if (!submeshes.TryGetValue(t.Texture, out var list)) submeshes[t.Texture] = list = new List<int>();
                list.Add(Vertex(t.A, t.Group, t.Normal));
                list.Add(Vertex(t.B, t.Group, t.Normal));
                list.Add(Vertex(t.C, t.Group, t.Normal));
            }

            var result = new List<RoGndSubmesh>(submeshes.Count);
            foreach (var pair in submeshes)
            {
                var indices = pair.Value.ToArray();
                RoSpace.ReverseWinding(indices);                                   // once per mesh (PLAN-0002 4.3)
                result.Add(new RoGndSubmesh { Texture = pair.Key, Triangles = indices });
            }

            return new RoGndMeshData
            {
                ChunkColumn = cx,
                ChunkRow = cz,
                Origin = origin,
                Positions = positions.ToArray(),
                Normals = normals.ToArray(),
                Uvs = uvs.ToArray(),
                Submeshes = result
            };
        }

        /// <summary>Exact float values with -0 folded into 0, the merge key of PLAN-0003 4.6.</summary>
        static float Exact(float f) => f == 0f ? 0f : f;

        static (float, float, float) Exact(Vector3 v) => (Exact(v.x), Exact(v.y), Exact(v.z));
    }
}
