using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Uninarok.Map;
using Uninarok.World.View;

namespace Uninarok.World.PlayTests
{
    // SPEC-0010 FR-11 (PLAN-0010 4.11, D10).
    public class CellPickerPlayTests
    {
        static MapData LoadMap(string name)
        {
#if UNITY_EDITOR
            foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:MapData"))
            {
                var map = UnityEditor.AssetDatabase.LoadAssetAtPath<MapData>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                if (map != null && map.MapName == name) return map;
            }
#endif
            Assert.Ignore($"no imported map data of {name}");
            return null;
        }

        // A map of 4 x 4 cells with the given corner heights (as PLAN-0005: 00, 10, 01, 11).
        static MapData Small(Func<int, int, Vector4> corners, float rowSign = 1f, bool flip = false)
        {
            const int n = 4;
            var map = ScriptableObject.CreateInstance<MapData>();
            var heights = new float[n * n * 4];
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                {
                    var c = corners(x, z);
                    for (int i = 0; i < 4; i++) heights[(z * n + x) * 4 + i] = c[i];
                }
            map.Assign("small", n, n, 0.5f, new Vector2(10f, 20f), rowSign, flip, heights, new byte[n * n], new byte[n * n], new byte[n * n]);
            return map;
        }

        /// <summary>
        /// The reference: every cell's two triangles as world points, the nearest one the ray hits (Möller–Trumbore), by brute force
        /// over the cells near the ray's shadow.
        /// </summary>
        static bool Reference(MapData map, Ray ray, out int hitX, out int hitZ, float reach)
        {
            hitX = hitZ = -1;
            float best = float.PositiveInfinity;
            var a = ray.origin;
            var b = ray.GetPoint(reach);
            int Cell(float v, float origin, float sign) => Mathf.FloorToInt((v - origin) * sign / map.CellSize);
            int x0 = Mathf.Max(0, Mathf.Min(Cell(a.x, map.Origin.x, 1f), Cell(b.x, map.Origin.x, 1f)) - 1), x1 = Mathf.Min(map.CellsX - 1, Mathf.Max(Cell(a.x, map.Origin.x, 1f), Cell(b.x, map.Origin.x, 1f)) + 1);
            int z0 = Mathf.Max(0, Mathf.Min(Cell(a.z, map.Origin.y, map.RowSign), Cell(b.z, map.Origin.y, map.RowSign)) - 1), z1 = Mathf.Min(map.CellsZ - 1, Mathf.Max(Cell(a.z, map.Origin.y, map.RowSign), Cell(b.z, map.Origin.y, map.RowSign)) + 1);
            for (int z = z0; z <= z1; z++)
                for (int x = x0; x <= x1; x++)
                {
                    var h = map.CornerHeights(x, z);
                    Vector3 P(int i, int j, float y) => new Vector3(map.Origin.x + (x + i) * map.CellSize, y, map.Origin.y + (z + j) * map.CellSize * map.RowSign);
                    Vector3 p00 = P(0, 0, h.x), p10 = P(1, 0, h.y), p01 = P(0, 1, h.z), p11 = P(1, 1, h.w);
                    float t = map.DiagonalFlip
                        ? Mathf.Min(Triangle(ray, p00, p10, p11), Triangle(ray, p00, p11, p01))
                        : Mathf.Min(Triangle(ray, p00, p10, p01), Triangle(ray, p10, p11, p01));
                    if (t >= best) continue;
                    best = t;
                    hitX = x;
                    hitZ = z;
                }
            return hitX >= 0;
        }

        static float Triangle(Ray ray, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 e1 = b - a, e2 = c - a, p = Vector3.Cross(ray.direction, e2);
            float det = Vector3.Dot(e1, p);
            if (Mathf.Abs(det) < 1e-12f) return float.PositiveInfinity;
            var s = ray.origin - a;
            float u = Vector3.Dot(s, p) / det;
            if (u < -1e-4f || u > 1.0001f) return float.PositiveInfinity;
            var q = Vector3.Cross(s, e1);
            float v = Vector3.Dot(ray.direction, q) / det;
            if (v < -1e-4f || u + v > 1.0001f) return float.PositiveInfinity;
            float t = Vector3.Dot(e2, q) / det;
            return t >= 0f ? t : float.PositiveInfinity;
        }

        [Test, Category("NeedsData")]
        public void CellPicker_PrtFild08Slope_4Turns_MinMaxTilt([Values(0f, 90f, 180f, 270f)] float yaw, [Values(20f, 50f, 65f)] float pitch)
        {
            var map = LoadMap("prt_fild08");
            // the steepest walkable cell away from the border
            int sx = 0, sz = 0;
            float steepest = 0f;
            for (int z = 60; z < map.CellsZ - 60; z++)
                for (int x = 60; x < map.CellsX - 60; x++)
                {
                    if (!map.IsWalkable(x, z)) continue;
                    var h = map.CornerHeights(x, z);
                    float slope = Mathf.Max(Mathf.Max(h.x, h.y), Mathf.Max(h.z, h.w)) - Mathf.Min(Mathf.Min(h.x, h.y), Mathf.Min(h.z, h.w));
                    if (slope <= steepest) continue;
                    steepest = slope;
                    sx = x;
                    sz = z;
                }
            Assert.Greater(steepest, 0.1f, "the map has slopes");

            var picker = new CellPicker(map);
            var target = map.CellCenter(sx, sz);
            var go = new GameObject("Camera");
            try
            {
                var camera = go.AddComponent<Camera>();
                camera.fieldOfView = 15f;
                camera.aspect = 16f / 9f;
                var rotation = Quaternion.Euler(pitch, yaw, 0f);
                camera.transform.SetPositionAndRotation(target - rotation * Vector3.forward * 40f, rotation);
                camera.pixelRect = new Rect(0, 0, 1600, 900);
                int compared = 0, hits = 0;
                for (int i = 0; i <= 20; i++)
                    for (int j = 0; j <= 20; j++)
                    {
                        var ray = camera.ViewportPointToRay(new Vector3(i / 20f + 0.0013f, j / 20f + 0.0017f, 0f));
                        bool expected = Reference(map, ray, out int ex, out int ez, 160f);
                        bool picked = picker.TryPick(ray, out int x, out int y);
                        compared++;
                        if (expected) hits++;
                        Assert.AreEqual((expected, ex, ez), (picked, x, y), $"viewport {i},{j}");
                    }
                Assert.Greater(hits, compared / 2);
                Assert.IsTrue(picker.TryPick(new Ray(camera.transform.position, (target - camera.transform.position).normalized), out _, out _));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void CellPicker_FlatMap_BothRowSigns_AndDiagonals([Values(1f, -1f)] float rowSign, [Values(false, true)] bool flip)
        {
            var map = Small((x, z) => Vector4.zero, rowSign, flip);
            var picker = new CellPicker(map);
            for (int z = 0; z < 4; z++)
                for (int x = 0; x < 4; x++)
                {
                    var centre = map.CellCenter(x, z);
                    Assert.IsTrue(picker.TryPick(new Ray(centre + new Vector3(0f, 5f, 0f), Vector3.down), out int px, out int py));
                    Assert.AreEqual((x, z), (px, py), "straight down");
                    Assert.IsTrue(picker.TryPick(new Ray(centre + new Vector3(-5f, 5f, 3f), new Vector3(5f, -5f, -3f).normalized), out px, out py));
                    Assert.AreEqual((x, z), (px, py), "slanting, from outside the map");
                }
            UnityEngine.Object.DestroyImmediate(map);
        }

        [Test]
        public void CellPicker_AWallOfGround_HidesWhatIsBehind()
        {
            // the third column stands 4 units high; looking east from low, it is hit before the ground behind it
            var map = Small((x, z) => x == 2 ? Vector4.one * 4f : Vector4.zero);
            var picker = new CellPicker(map);
            var top = map.CellCenter(2, 1);
            Assert.IsTrue(picker.TryPick(new Ray(top + new Vector3(-3f, 3f, 0f), new Vector3(3f, -3f, 0f).normalized), out int x, out int y));
            Assert.AreEqual((2, 1), (x, y));
            UnityEngine.Object.DestroyImmediate(map);
        }

        [Test]
        public void CellPicker_Misses()
        {
            var map = Small((x, z) => Vector4.zero);
            var picker = new CellPicker(map);
            var centre = map.CellCenter(1, 1);
            Assert.IsFalse(picker.TryPick(new Ray(centre + Vector3.up, Vector3.up), out int x, out int y), "away from the ground");
            Assert.AreEqual((-1, -1), (x, y));
            Assert.IsFalse(picker.TryPick(new Ray(centre + new Vector3(50f, 5f, 0f), Vector3.down), out _, out _), "beside the map");
            Assert.IsFalse(picker.TryPick(new Ray(centre + Vector3.up, Vector3.right), out _, out _), "level");
            UnityEngine.Object.DestroyImmediate(map);
        }

        [Test]
        public void CellPicker_AllocatesNothing()
        {
            var map = Small((x, z) => new Vector4(0f, 0.1f, 0.2f, 0.3f));
            var picker = new CellPicker(map);
            var ray = new Ray(map.CellCenter(2, 2) + new Vector3(-5f, 5f, 3f), new Vector3(5f, -5f, -3f).normalized);
            int hits = 0;
            Action run = () =>
            {
                for (int i = 0; i < 1000; i++)
                    if (picker.TryPick(ray, out _, out _)) hits++;
            };
            run();
            Assert.That(() => run(), UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory());
            Assert.AreEqual(2000, hits);
            UnityEngine.Object.DestroyImmediate(map);
        }
    }
}
