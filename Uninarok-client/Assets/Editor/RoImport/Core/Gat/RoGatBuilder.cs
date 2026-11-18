using UnityEngine;
using Uninarok.RoImport.Core.Gnd;
using Uninarok.RoImport.Core.Results;

namespace Uninarok.RoImport.Core.Gat
{
    /// <summary>The arrays and geometry of a <c>MapData</c> asset, built in memory (PLAN-0005 4.3).</summary>
    public sealed class RoMapDataArrays
    {
        public int CellsX, CellsZ;
        public float CellSize;
        public Vector2 Origin;
        public float RowSign;
        public bool DiagonalFlip;
        /// <summary>Four per cell, H00, H10, H01, H11, in Unity units (up is +y).</summary>
        public float[] Heights;
        public byte[] Types, Flags, RawTypes;
        /// <summary>Cells whose raw type is not 0 to 6 (reported as <see cref="RoIssueCode.GatUnknownType"/>).</summary>
        public int UnknownCells;
    }

    /// <summary>
    /// Builds the map data arrays from a GAT and the map space of its GND (PLAN-0005 4.3, 4.4). Pure: the geometry comes
    /// from <see cref="RoMapSpace"/> (cell size, origin, row direction) and the diagonal from <see cref="RoGndRules"/>, so
    /// the runtime needs no copy of those rules (D3).
    /// </summary>
    public static class RoGatBuilder
    {
        /// <summary>Implemented by TASKS-0005 T3.2.</summary>
        public static RoMapDataArrays Build(string source, RoGat gat, RoMapSpace space, RoGndRules rules, RoReport report) =>
            throw new System.NotImplementedException();
    }
}
