using System;
using NUnit.Framework;
using UnityEngine;

namespace Uninarok.Map.Tests
{
    // TASKS-0005 T1.2 / T1.3 (FR-2): cell and position queries of PLAN-0005 4.3. Positions are built from the cell
    // geometry (origin, cell size, row sign), so non-square maps and both row signs are covered.
    public class MapDataQueryTests
    {
        const float Eps = 1e-5f;
        static readonly Vector2 Origin = new Vector2(1f, 2f);

        MapData _data;

        [TearDown]
        public void TearDown()
        {
            if (_data != null) UnityEngine.Object.DestroyImmediate(_data);
        }

        /// <summary>A Unity position at fractional cell coordinates (fx, fz) of the map.</summary>
        static Vector3 At(MapData d, float fx, float fz, float y = 0f) =>
            new Vector3(d.Origin.x + fx * d.CellSize, y, d.Origin.y + fz * d.CellSize * d.RowSign);

        MapData Make(int sx, int sz, float rowSign, Func<int, int, float> height = null, Func<int, int, byte> type = null,
            bool flip = false) =>
            _data = MapTestData.Create(sx, sz, height, type, Origin, rowSign, flip);

        // ---- cells ----

        [TestCase(8, 6, 1f)]
        [TestCase(8, 6, -1f)]
        [TestCase(6, 8, 1f)]
        [TestCase(6, 8, -1f)]
        public void TryGetCell_FindsCell00_AndTheLastCell(int sx, int sz, float rowSign)
        {
            var d = Make(sx, sz, rowSign);

            Assert.IsTrue(d.TryGetCell(At(d, 0.5f, 0.5f), out int x, out int z));
            Assert.AreEqual((0, 0), (x, z));
            Assert.IsTrue(d.TryGetCell(At(d, sx - 0.5f, sz - 0.5f), out x, out z));
            Assert.AreEqual((sx - 1, sz - 1), (x, z));
            Assert.IsTrue(d.TryGetCell(At(d, 3.25f, 1.75f), out x, out z));
            Assert.AreEqual((3, 1), (x, z), "column from x, row from z, not swapped");
        }

        [TestCase(1f)]
        [TestCase(-1f)]
        public void TryGetCell_OnACellEdge_TakesTheCellThatStartsThere(float rowSign)
        {
            var d = Make(8, 6, rowSign);

            Assert.IsTrue(d.TryGetCell(At(d, 2f, 3f), out int x, out int z));
            Assert.AreEqual((2, 3), (x, z));
            Assert.IsTrue(d.TryGetCell(At(d, 0f, 0f), out x, out z), "the outer corner of cell (0, 0) is inside");
            Assert.AreEqual((0, 0), (x, z));
        }

        [TestCase(1f)]
        [TestCase(-1f)]
        public void PositionsOutside_OnAllFourSides_AreNotWalkable_AndNeverThrow(float rowSign)
        {
            var d = Make(8, 6, rowSign);
            var outside = new[]
            {
                At(d, -0.01f, 3f), At(d, 8f, 3f), At(d, 4f, -0.01f), At(d, 4f, 6f),
                At(d, -100f, -100f), new Vector3(float.NaN, 0f, 0f), new Vector3(float.PositiveInfinity, 0f, 0f)
            };

            foreach (var p in outside)
            {
                Assert.IsFalse(d.TryGetCell(p, out int x, out int z), p.ToString());
                Assert.IsFalse(d.IsWalkable(p), p.ToString());
                Assert.IsFalse(d.TryGetHeight(p, out _), p.ToString());
            }
        }

        [Test]
        public void CellsOutside_AreNotWalkable_OtherAndNone()
        {
            var d = Make(8, 6, 1f);

            foreach (var (x, z) in new[] { (-1, 0), (8, 0), (0, -1), (0, 6), (int.MinValue, int.MaxValue) })
            {
                Assert.IsFalse(d.InBounds(x, z));
                Assert.IsFalse(d.IsWalkable(x, z));
                Assert.IsFalse(d.IsWalkableWater(x, z));
                Assert.AreEqual(MapCellType.Other, d.TypeAt(x, z));
                Assert.AreEqual(MapCellFlags.None, d.FlagsAt(x, z));
                Assert.AreEqual(Vector4.zero, d.CornerHeights(x, z));
            }
            Assert.IsTrue(d.InBounds(0, 0));
            Assert.IsTrue(d.InBounds(7, 5));
        }

        // ---- types and flags (the server table of docs/data/GatCellType.md) ----

        [TestCase(1f)]
        [TestCase(-1f)]
        public void TypesAndFlags_ByCell_AndByPosition(float rowSign)
        {
            // raw type = column (0..7, 7 is not a type), so every type appears once per row
            var d = Make(8, 6, rowSign, type: (x, z) => (byte)x);

            for (int x = 0; x < 8; x++)
            {
                var expectedType = x <= 6 ? (MapCellType)x : MapCellType.Other;
                var expectedFlags = MapTestData.FlagsOf(expectedType);
                bool walkable = (expectedFlags & MapCellFlags.Walkable) != 0;

                Assert.AreEqual(expectedType, d.TypeAt(x, 4), $"type of column {x}");
                Assert.AreEqual(expectedFlags, d.FlagsAt(x, 4), $"flags of column {x}");
                Assert.AreEqual(walkable, d.IsWalkable(x, 4), $"walkable column {x}");
                Assert.AreEqual(walkable, d.IsWalkable(At(d, x + 0.5f, 4.5f)), $"walkable at the centre of column {x}");
                Assert.AreEqual(expectedType == MapCellType.Water, d.IsWalkableWater(x, 4), $"water column {x}");
            }
        }

        [Test]
        public void IsWalkable_ByPosition_ReadsTheRightRow_OnANonSquareMap()
        {
            // only cell (1, 6) is walkable on a 4 x 8 map
            var d = Make(4, 8, -1f, type: (x, z) => (byte)(x == 1 && z == 6 ? 0 : 1));

            Assert.IsTrue(d.IsWalkable(At(d, 1.5f, 6.5f)));
            Assert.IsFalse(d.IsWalkable(At(d, 6.5f, 1.5f)), "swapped axes would hit the walkable cell's mirror");
            Assert.IsFalse(d.IsWalkable(At(d, 1.5f, 1.5f)), "mirrored rows would hit row 1");
        }

        // ---- heights ----

        [Test]
        public void CornerHeights_AreTheStoredCorners()
        {
            var d = Make(8, 6, 1f, height: (x, z) => x + 10f * z);

            Assert.AreEqual(new Vector4(3f + 20f, 4f + 20f, 3f + 30f, 4f + 30f), d.CornerHeights(3, 2));
        }

        [TestCase(1f, false)]
        [TestCase(-1f, false)]
        [TestCase(1f, true)]
        [TestCase(-1f, true)]
        public void TryGetHeight_OnAPlane_IsThePlane_Everywhere(float rowSign, bool flip)
        {
            var d = Make(6, 8, rowSign, height: (x, z) => 0.1f * x + 0.2f * z, flip: flip);

            foreach (var (fx, fz) in new[] { (0.5f, 0.5f), (5.5f, 7.5f), (2.2f, 3.9f), (3f, 4f), (4.9f, 0.1f), (0f, 0f) })
            {
                Assert.IsTrue(d.TryGetHeight(At(d, fx, fz, y: 99f), out float y), $"({fx}, {fz})");
                Assert.AreEqual(0.1f * fx + 0.2f * fz, y, Eps, $"({fx}, {fz})");
            }
        }

        // A cell whose corner H11 is 1 and the others 0. With the ground's diagonal (H10 to H01) the triangle
        // (00, 10, 01) is flat and (10, 11, 01) rises; with the other diagonal (H00 to H11) both rise.
        static float Tent(int x, int z) => x == 3 && z == 3 ? 1f : 0f;

        [TestCase(0.25f, 0.25f, false, 0f)]
        [TestCase(0.75f, 0.75f, false, 0.5f)]
        [TestCase(0.9f, 0.4f, false, 0.3f)]
        [TestCase(0.25f, 0.25f, true, 0.25f)]
        [TestCase(0.75f, 0.75f, true, 0.75f)]
        [TestCase(0.9f, 0.4f, true, 0.4f)]
        public void TryGetHeight_UsesTheCellsDiagonal_OnBothTriangles(float u, float v, bool flip, float expected)
        {
            var d = Make(6, 6, 1f, height: Tent, flip: flip);

            Assert.IsTrue(d.TryGetHeight(At(d, 2f + u, 2f + v), out float y));

            Assert.AreEqual(expected, y, Eps);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TryGetHeight_IsContinuousAcrossTheDiagonal(bool flip)
        {
            var d = Make(6, 6, -1f, height: Tent, flip: flip);
            const float t = 1e-4f;

            for (float s = 0.1f; s < 1f; s += 0.2f)
            {
                // a point on the diagonal, nudged to each side
                Vector2 p = flip ? new Vector2(s, s) : new Vector2(s, 1f - s);
                Vector2 n = flip ? new Vector2(t, -t) : new Vector2(t, t);
                d.TryGetHeight(At(d, 2f + p.x + n.x, 2f + p.y + n.y), out float a);
                d.TryGetHeight(At(d, 2f + p.x - n.x, 2f + p.y - n.y), out float b);
                Assert.AreEqual(a, b, 1e-3f, $"at {s}");
            }
        }

        [TestCase(1f, false)]
        [TestCase(-1f, false)]
        [TestCase(-1f, true)]
        public void CellCenter_IsTheCentre_WithTheDiagonalHeight(float rowSign, bool flip)
        {
            var d = Make(8, 6, rowSign, height: Tent, flip: flip);

            var c = d.CellCenter(2, 2);

            var expected = At(d, 2.5f, 2.5f);
            Assert.AreEqual(expected.x, c.x, Eps);
            Assert.AreEqual(expected.z, c.z, Eps);
            Assert.AreEqual(flip ? 0.5f : 0f, c.y, Eps, "the diagonal's mean: (H10 + H01) / 2 or (H00 + H11) / 2");
            Assert.IsTrue(d.TryGetHeight(new Vector3(c.x, 0f, c.z), out float h));
            Assert.AreEqual(c.y, h, Eps, "the centre lies on the surface TryGetHeight describes");
        }

        // ---- allocation ----

        [Test]
        public void Queries_DoNotAllocate()
        {
            var d = Make(8, 6, -1f, height: (x, z) => x * 0.1f, type: (x, z) => (byte)(x % 7));
            var p = At(d, 3.3f, 2.7f);
            float sum = 0f;
            Run(); // warm up (JIT)

            long before = GC.GetAllocatedBytesForCurrentThread();
            Run();
            long after = GC.GetAllocatedBytesForCurrentThread();

            Assert.AreEqual(0, after - before);
            Assert.AreNotEqual(float.NaN, sum);

            void Run()
            {
                for (int i = 0; i < 10000; i++)
                {
                    d.TryGetCell(p, out int x, out int z);
                    if (d.IsWalkable(p)) sum += 1f;
                    if (d.IsWalkable(x, z)) sum += 1f;
                    sum += (int)d.TypeAt(x, z) + (int)d.FlagsAt(x, z);
                    if (d.TryGetHeight(p, out float y)) sum += y;
                    sum += d.CellCenter(x, z).y + d.CornerHeights(x, z).w;
                }
            }
        }
    }
}
