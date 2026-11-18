using System;
using UnityEngine;
using Uninarok.Map;
using Uninarok.RoImport.Core.Imaging;

namespace Uninarok.RoImport.Core.Gat
{
    /// <summary>The optional flat image of the cell types (PLAN-0005 4.5, FR-8). Implemented by TASKS-0005 T4.2.</summary>
    public static class RoGatImage
    {
        public static Color32 ColorOf(MapCellType type) => throw new NotImplementedException();

        public static RoImage Build(int cellsX, int cellsZ, byte[] types) => throw new NotImplementedException();
    }
}
