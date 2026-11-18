using System;
using System.Collections.Generic;
using UnityEngine;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Space;

namespace Uninarok.RoImport.Core.Rsm
{
    /// <summary>
    /// Builds one node's mesh (PLAN-0003 4.6): vertices welded by value, normals by shade type (<see cref="RsmShading"/>),
    /// faces grouped into submeshes by (texture, two-sided), converted to Unity space.
    /// </summary>
    public static class RsmMeshBuilder
    {
        /// <param name="vertexMatrix">The node's vertex matrix <c>A</c> (4.4 step 4).</param>
        /// <param name="reverseExtraWinding">True when <c>A</c> mirrors (<see cref="RsmSpace.MirrorsWinding"/>).</param>
        /// <param name="report">Receives one <see cref="RoIssueCode.RsmBadIndex"/> warning per node with bad faces.</param>
        /// <param name="referrer">The model, named in warnings.</param>
        public static RsmMeshData Build(RoRsmNode node, RsmShadeType shade, bool hasShadeType, in Matrix4x4 vertexMatrix,
                                        bool reverseExtraWinding, RoReport report, string referrer = null)
        {
            var faces = ValidFaces(node, report, referrer);
            var effective = RsmShading.Effective(shade, hasShadeType);

            // 1. Positions converted and welded by value (-0 normalized to 0).
            var welded = new List<Vector3>();
            var weldIndex = new Dictionary<(float, float, float), int>();
            var posOf = new int[node.Vertices.Length];
            for (int i = 0; i < node.Vertices.Length; i++)
            {
                var p = Normalize(RsmSpace.Vertex(vertexMatrix, node.Vertices[i]));
                var key = (p.x, p.y, p.z);   // exact values; -0 was normalized
                if (!weldIndex.TryGetValue(key, out int w))
                {
                    w = welded.Count;
                    welded.Add(p);
                    weldIndex.Add(key, w);
                }
                posOf[i] = w;
            }

            // 2. Face normals from the triangle as it will be written: the conversion reverses the winding once, a
            //    mirroring vertex matrix once more (4.5). Smooth normals sum the corner-angle-weighted face normals per
            //    (position, group); a zero-area face contributes nothing.
            var faceNormal = new Vector3[faces.Count];
            var smooth = new Dictionary<(int, int), Vector3>();
            for (int fi = 0; fi < faces.Count; fi++)
            {
                var corners = Corners(faces[fi].Face, reverseExtraWinding);
                var a = welded[posOf[corners.a]];
                var b = welded[posOf[corners.b]];
                var c = welded[posOf[corners.c]];
                var cross = Vector3.Cross(b - a, c - a);
                if (cross.sqrMagnitude <= 0f) continue;
                var n = cross.normalized;
                faceNormal[fi] = n;
                if (effective != RsmShadeType.Smooth) continue;

                int group = faces[fi].Face.SmoothGroup;
                Accumulate(smooth, posOf[corners.a], group, n * Angle(b - a, c - a));
                Accumulate(smooth, posOf[corners.b], group, n * Angle(c - b, a - b));
                Accumulate(smooth, posOf[corners.c], group, n * Angle(a - c, b - c));
            }

            // 3. One vertex per (welded position, uv, normal group); triangles per (texture, two-sided).
            var positions = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var vertexIndex = new Dictionary<(int, float, float, int), int>();
            var groups = new SortedDictionary<(int texture, int twoSided), List<int>>();
            for (int fi = 0; fi < faces.Count; fi++)
            {
                var face = faces[fi].Face;
                int group = RsmShading.GroupKey(effective, fi, face.SmoothGroup);
                var key = (faces[fi].Texture, face.TwoSided ? 1 : 0);
                if (!groups.TryGetValue(key, out var triangles)) groups.Add(key, triangles = new List<int>());

                foreach (var (v, t) in new[] { (face.V0, face.T0), (face.V1, face.T1), (face.V2, face.T2) })
                {
                    int pos = posOf[v];
                    var tc = node.TexCoords[t];
                    var uv = new Vector2(Normalize(tc.U), Normalize(1f - tc.V));
                    var vk = (pos, uv.x, uv.y, group);
                    if (!vertexIndex.TryGetValue(vk, out int index))
                    {
                        index = positions.Count;
                        vertexIndex.Add(vk, index);
                        positions.Add(welded[pos]);
                        uvs.Add(uv);
                        normals.Add(NormalOf(effective, pos, face.SmoothGroup, faceNormal[fi], smooth));
                    }
                    triangles.Add(index);
                }
            }

            var submeshes = new List<RsmSubmesh>(groups.Count);
            foreach (var g in groups)
            {
                var triangles = g.Value.ToArray();
                RoSpace.ReverseWinding(triangles);
                if (reverseExtraWinding) RoSpace.ReverseWinding(triangles);
                submeshes.Add(new RsmSubmesh { TextureIndex = g.Key.texture, TwoSided = g.Key.twoSided != 0, Triangles = triangles });
            }

            return new RsmMeshData
            {
                Positions = positions.ToArray(),
                Normals = normals.ToArray(),
                Uvs = uvs.ToArray(),
                Submeshes = submeshes
            };
        }

        readonly struct ValidFace
        {
            public readonly RoRsmFace Face;
            public readonly int Texture;

            public ValidFace(RoRsmFace face, int texture)
            {
                Face = face;
                Texture = texture;
            }
        }

        /// <summary>Faces whose indices exist; the others are dropped with one warning for the node (4.3).</summary>
        static List<ValidFace> ValidFaces(RoRsmNode node, RoReport report, string referrer)
        {
            var result = new List<ValidFace>(node.Faces.Length);
            int dropped = 0;
            foreach (var f in node.Faces)
            {
                bool ok = InRange(f.V0, node.Vertices.Length) && InRange(f.V1, node.Vertices.Length) && InRange(f.V2, node.Vertices.Length)
                          && InRange(f.T0, node.TexCoords.Length) && InRange(f.T1, node.TexCoords.Length) && InRange(f.T2, node.TexCoords.Length)
                          && InRange(f.TextureSlot, node.TextureSlots.Length);
                if (!ok)
                {
                    dropped++;
                    continue;
                }
                result.Add(new ValidFace(f, node.TextureSlots[f.TextureSlot]));
            }

            if (dropped > 0)
                report?.Warn(RoIssueCode.RsmBadIndex, node.Name, referrer,
                    $"{dropped} of {node.Faces.Length} faces refer to a vertex, texture coordinate or texture slot that does not exist; they are dropped");
            return result;
        }

        static bool InRange(int index, int count) => index >= 0 && index < count;

        /// <summary>The face's corners in the order they are written after the winding reversal(s).</summary>
        static (int a, int b, int c) Corners(RoRsmFace f, bool reverseExtraWinding) =>
            reverseExtraWinding ? (f.V0, f.V1, f.V2) : (f.V0, f.V2, f.V1);

        static float Angle(Vector3 u, Vector3 v)
        {
            float lu = u.magnitude, lv = v.magnitude;
            if (lu <= 0f || lv <= 0f) return 0f;
            return Mathf.Acos(Mathf.Clamp(Vector3.Dot(u, v) / (lu * lv), -1f, 1f));
        }

        static void Accumulate(Dictionary<(int, int), Vector3> sums, int position, int group, Vector3 value)
        {
            var key = (position, group);
            sums[key] = sums.TryGetValue(key, out var sum) ? sum + value : value;
        }

        static Vector3 NormalOf(RsmShadeType effective, int position, int smoothGroup, Vector3 faceNormal,
                                Dictionary<(int, int), Vector3> smooth)
        {
            switch (effective)
            {
                case RsmShadeType.None:
                    return Vector3.up;
                case RsmShadeType.Flat:
                    return faceNormal.sqrMagnitude > 0f ? faceNormal : Vector3.up;
                default:
                    if (smooth.TryGetValue((position, smoothGroup), out var sum) && sum.sqrMagnitude > 0f) return sum.normalized;
                    return faceNormal.sqrMagnitude > 0f ? faceNormal : Vector3.up;
            }
        }

        static float Normalize(float f) => f == 0f ? 0f : f;   // -0 becomes 0

        static Vector3 Normalize(Vector3 v) => new Vector3(Normalize(v.x), Normalize(v.y), Normalize(v.z));
    }
}
