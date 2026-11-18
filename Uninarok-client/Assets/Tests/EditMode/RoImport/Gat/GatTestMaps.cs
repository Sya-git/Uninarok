using System;
using Uninarok.RoImport.Core.Gat;

namespace Uninarok.RoImport.Tests.Gat
{
    /// <summary>
    /// Synthetic GAT maps for the pure tests (TASKS-0005 T2.1). Heights are in Ragnarok units (Y down: a higher cell has
    /// a lower number). Unless a builder says otherwise, neighbouring cells share their corner heights.
    /// </summary>
    public static class GatTestMaps
    {
        /// <summary>A map whose corner (column, row) has height <paramref name="corner"/>(column, row) and cell type <paramref name="type"/>(x, z).</summary>
        public static RoGat Build(int width, int height, Func<int, int, float> corner, Func<int, int, uint> type)
        {
            var gat = new RoGat
            {
                VersionMajor = 1,
                VersionMinor = 2,
                Width = width,
                Height = height,
                Heights = new float[width * height * 4],
                Types = new uint[width * height]
            };
            for (int z = 0; z < height; z++)
            for (int x = 0; x < width; x++)
            {
                int i = gat.IndexOf(x, z);
                gat.Heights[i * 4 + 0] = corner(x, z);
                gat.Heights[i * 4 + 1] = corner(x + 1, z);
                gat.Heights[i * 4 + 2] = corner(x, z + 1);
                gat.Heights[i * 4 + 3] = corner(x + 1, z + 1);
                gat.Types[i] = type(x, z);
            }
            return gat;
        }

        /// <summary>4 x 4 walkable cells at height 0.</summary>
        public static RoGat Flat4x4() => Build(4, 4, (x, z) => 0f, (x, z) => 0u);

        /// <summary>4 x 4 walkable cells rising one Ragnarok unit per column (height -x).</summary>
        public static RoGat Ramp4x4() => Build(4, 4, (x, z) => -x, (x, z) => 0u);

        /// <summary>
        /// 4 x 4 cells with a cliff: cell (2, 1) is type 5 (cliff) and its north-east corner is 20 units higher than the
        /// rest of the map. The cell does not share that corner with its neighbours.
        /// </summary>
        public static RoGat Cliff4x4()
        {
            var gat = Build(4, 4, (x, z) => 0f, (x, z) => x == 2 && z == 1 ? 5u : 0u);
            gat.Heights[gat.IndexOf(2, 1) * 4 + 3] = -20f;
            return gat;
        }

        /// <summary>
        /// A non-square map with an asymmetric pattern, so a swapped or mirrored grid is visible: cell (W-1, 0) is a wall
        /// (1), cell (0, H-1) a cliff (5), cell (1, 2) walkable water (3), everything else walkable; corner height
        /// <c>-(x + 10 z)</c>.
        /// </summary>
        public static RoGat Asymmetric(int width, int height) =>
            Build(width, height, (x, z) => -(x + 10f * z), (x, z) =>
                x == width - 1 && z == 0 ? 1u : x == 0 && z == height - 1 ? 5u : x == 1 && z == 2 ? 3u : 0u);

        /// <summary>8 x 6 cells (a 4 x 3 cube ground).</summary>
        public static RoGat Asymmetric8x6() => Asymmetric(8, 6);

        /// <summary>6 x 8 cells (a 3 x 4 cube ground).</summary>
        public static RoGat Asymmetric6x8() => Asymmetric(6, 8);

        /// <summary>4 x 4 walkable cells with one walkable water cell (type 3) at (1, 2).</summary>
        public static RoGat Water4x4() => Build(4, 4, (x, z) => 0f, (x, z) => x == 1 && z == 2 ? 3u : 0u);
    }
}
