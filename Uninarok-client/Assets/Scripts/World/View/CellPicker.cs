using System;
using UnityEngine;
using Uninarok.Map;

namespace Uninarok.World.View
{
    /// <summary>
    /// The ground cell under a ray (SPEC-0010 FR-11; PLAN-0010 4.11, D10): it marches over the cells in the ray's ground
    /// projection, nearest first, and intersects each cell's two triangles. Only the map data counts, no colliders. Allocates nothing.
    /// </summary>
    public sealed class CellPicker
    {
        // A ray through the edge between two triangles belongs to one of them.
        const float Edge = 1e-4f;

        readonly MapData _map;

        public CellPicker(MapData map) => _map = map != null ? map : throw new ArgumentNullException(nameof(map));

        public MapData Map => _map;

        /// <summary>The first cell whose ground the ray hits; false when it hits none. <paramref name="y"/> is the server's y (the row).</summary>
        public bool TryPick(Ray ray, out int x, out int y)
        {
            x = y = -1;
            // Grid space: one unit per cell, rows growing.
            float size = _map.CellSize, row = _map.RowSign;
            var origin = _map.Origin;
            float px = (ray.origin.x - origin.x) / size, pz = (ray.origin.z - origin.y) * row / size, py = ray.origin.y;
            float dx = ray.direction.x / size, dz = ray.direction.z * row / size, dy = ray.direction.y;
            int width = _map.CellsX, height = _map.CellsZ;

            // Where the ray is over the map.
            float from = 0f, to = float.PositiveInfinity;
            if (!Clip(px, dx, width, ref from, ref to) || !Clip(pz, dz, height, ref from, ref to)) return false;

            int cx = Math.Clamp((int)Math.Floor(px + dx * from), 0, width - 1);
            int cz = Math.Clamp((int)Math.Floor(pz + dz * from), 0, height - 1);
            int stepX = Math.Sign(dx), stepZ = Math.Sign(dz);
            // The ray parameter at the next border in each direction, and from one border to the next.
            float nextX = stepX == 0 ? float.PositiveInfinity : (cx + (stepX > 0 ? 1 : 0) - px) / dx;
            float nextZ = stepZ == 0 ? float.PositiveInfinity : (cz + (stepZ > 0 ? 1 : 0) - pz) / dz;
            float deltaX = stepX == 0 ? float.PositiveInfinity : Math.Abs(1f / dx);
            float deltaZ = stepZ == 0 ? float.PositiveInfinity : Math.Abs(1f / dz);

            for (int guard = width + height + 2; guard > 0; guard--)
            {
                if (Hits(cx, cz, px - cx, pz - cz, py, dx, dz, dy))
                {
                    x = cx;
                    y = cz;
                    return true;
                }
                if (nextX < nextZ)
                {
                    if (nextX > to) return false;
                    cx += stepX;
                    nextX += deltaX;
                }
                else
                {
                    if (nextZ > to || float.IsPositiveInfinity(nextZ)) return false;
                    cz += stepZ;
                    nextZ += deltaZ;
                }
                if ((uint)cx >= (uint)width || (uint)cz >= (uint)height) return false;
            }
            return false;
        }

        static bool Clip(float p, float d, int size, ref float from, ref float to)
        {
            if (Math.Abs(d) < 1e-9f) return p >= 0f && p < size;
            float a = -p / d, b = (size - p) / d;
            if (a > b) (a, b) = (b, a);
            from = Math.Max(from, a);
            to = Math.Min(to, b);
            return from <= to;
        }

        // The ray against the cell's two triangles; u0, v0 are the ray's origin seen from the cell's corner.
        bool Hits(int cx, int cz, float u0, float v0, float y0, float du, float dv, float dy)
        {
            var h = _map.CornerHeights(cx, cz); // 00, 10, 01, 11
            if (!_map.DiagonalFlip)
            {
                // (00, 10, 01): u + v <= 1, and (10, 11, 01)
                return HitsPlane(h.x, h.y - h.x, h.z - h.x, u0, v0, y0, du, dv, dy, 0)
                       || HitsPlane(h.z + h.y - h.w, h.w - h.z, h.w - h.y, u0, v0, y0, du, dv, dy, 1);
            }
            // (00, 10, 11): u >= v, and (00, 11, 01)
            return HitsPlane(h.x, h.y - h.x, h.w - h.y, u0, v0, y0, du, dv, dy, 2)
                   || HitsPlane(h.x, h.w - h.z, h.z - h.x, u0, v0, y0, du, dv, dy, 3);
        }

        // height = a + b u + c v over one of the four triangle shapes
        static bool HitsPlane(float a, float b, float c, float u0, float v0, float y0, float du, float dv, float dy, int shape)
        {
            float slope = dy - b * du - c * dv;
            if (Math.Abs(slope) < 1e-9f) return false;
            float t = (a + b * u0 + c * v0 - y0) / slope;
            if (t < 0f) return false;
            float u = u0 + du * t, v = v0 + dv * t;
            if (u < -Edge || u > 1f + Edge || v < -Edge || v > 1f + Edge) return false;
            switch (shape)
            {
                case 0: return u + v <= 1f + Edge;
                case 1: return u + v >= 1f - Edge;
                case 2: return u >= v - Edge;
                default: return u <= v + Edge;
            }
        }
    }
}
