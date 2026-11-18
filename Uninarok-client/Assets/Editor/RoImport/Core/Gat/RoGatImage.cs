using System;
using UnityEngine;
using Uninarok.Map;
using Uninarok.RoImport.Core.Imaging;

namespace Uninarok.RoImport.Core.Gat
{
    /// <summary>
    /// The optional flat image of the cell types (PLAN-0005 4.5, FR-8): one pixel per cell, one fixed colour per
    /// <see cref="MapCellType"/>, row 0 at the bottom as in the map. Pure and deterministic; the gizmo legend uses the
    /// same palette (4.6).
    /// </summary>
    public static class RoGatImage
    {
        public static Color32 ColorOf(MapCellType type)
        {
            switch (type)
            {
                case MapCellType.Walkable: return new Color32(76, 175, 80, 255);    // green
                case MapCellType.Wall: return new Color32(55, 55, 60, 255);         // dark grey
                case MapCellType.Type2: return new Color32(139, 195, 74, 255);      // light green
                case MapCellType.Water: return new Color32(33, 150, 243, 255);      // blue
                case MapCellType.Type4: return new Color32(205, 220, 57, 255);      // lime
                case MapCellType.Cliff: return new Color32(255, 152, 0, 255);       // orange
                case MapCellType.Type6: return new Color32(0, 150, 136, 255);       // teal
                default: return new Color32(233, 30, 99, 255);                      // magenta: Other
            }
        }

        /// <summary>The image of <paramref name="types"/> (one <see cref="MapCellType"/> byte per cell, row-major).</summary>
        public static RoImage Build(int cellsX, int cellsZ, byte[] types)
        {
            if (types == null) throw new ArgumentNullException(nameof(types));
            if (types.Length != cellsX * cellsZ) throw new ArgumentException($"expected {cellsX * cellsZ} cells", nameof(types));

            var image = new RoImage(cellsX, cellsZ);
            for (int z = 0; z < cellsZ; z++)
            for (int x = 0; x < cellsX; x++)
                image.SetPixel(x, cellsZ - 1 - z, ColorOf((MapCellType)types[z * cellsX + x]));   // RoImage is top row first
            return image;
        }
    }
}
