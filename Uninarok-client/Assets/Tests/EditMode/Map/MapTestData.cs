using UnityEngine;

namespace Uninarok.Map.Tests
{
    /// <summary>Synthetic <see cref="MapData"/> built in memory (TASKS-0005 T1.1). Heights are in Unity units, up is +y.</summary>
    static class MapTestData
    {
        public const float CellSize = 0.5f;

        /// <summary>
        /// A map of <paramref name="sizeX"/> by <paramref name="sizeZ"/> cells. Cell (x, z) has corner heights
        /// <c>h(x, z)</c> from <paramref name="height"/> (corner coordinates in cells) and raw type <paramref name="type"/>(x, z).
        /// Flags follow the server table of <c>docs/data/GatCellType.md</c>.
        /// </summary>
        public static MapData Create(int sizeX, int sizeZ, System.Func<int, int, float> height = null,
            System.Func<int, int, byte> type = null, Vector2 origin = default, float rowSign = 1f, bool diagonalFlip = false,
            string name = "synthetic")
        {
            height = height ?? ((x, z) => 0f);
            type = type ?? ((x, z) => 0);
            int count = sizeX * sizeZ;
            var heights = new float[count * 4];
            var types = new byte[count];
            var flags = new byte[count];
            var raw = new byte[count];
            for (int z = 0; z < sizeZ; z++)
            for (int x = 0; x < sizeX; x++)
            {
                int i = z * sizeX + x;
                heights[i * 4 + 0] = height(x, z);
                heights[i * 4 + 1] = height(x + 1, z);
                heights[i * 4 + 2] = height(x, z + 1);
                heights[i * 4 + 3] = height(x + 1, z + 1);
                byte t = type(x, z);
                raw[i] = t;
                types[i] = t <= 6 ? t : (byte)MapCellType.Other;
                flags[i] = (byte)FlagsOf((MapCellType)types[i]);
            }

            var data = ScriptableObject.CreateInstance<MapData>();
            data.Assign(name, sizeX, sizeZ, CellSize, origin, rowSign, diagonalFlip, heights, types, flags, raw);
            return data;
        }

        /// <summary>The server table (Hercules map_gat2cell), written out independently of the importer's rules.</summary>
        public static MapCellFlags FlagsOf(MapCellType type)
        {
            switch (type)
            {
                case MapCellType.Walkable:
                case MapCellType.Type2:
                case MapCellType.Type4:
                case MapCellType.Type6: return MapCellFlags.Walkable | MapCellFlags.Shootable;
                case MapCellType.Water: return MapCellFlags.Walkable | MapCellFlags.Shootable | MapCellFlags.Water;
                case MapCellType.Cliff: return MapCellFlags.Shootable;
                default: return MapCellFlags.None;
            }
        }
    }
}
