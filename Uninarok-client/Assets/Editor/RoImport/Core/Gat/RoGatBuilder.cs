using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Uninarok.Map;
using Uninarok.RoImport.Core.Gnd;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Space;

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
        /// <summary>The most distinct unknown raw values listed in one issue.</summary>
        const int MaxListedValues = 8;

        /// <summary>
        /// The arrays for <paramref name="gat"/> over the ground of <paramref name="space"/>, or null when the grid is not
        /// exactly twice the ground (one <see cref="RoIssueCode.GatGndMismatch"/>, D7). Unknown raw types are reported
        /// once with the values and a count.
        /// </summary>
        public static RoMapDataArrays Build(string source, RoGat gat, RoMapSpace space, RoGndRules rules, RoReport report)
        {
            if (gat.Width != 2 * space.Width || gat.Height != 2 * space.Height)
            {
                report?.Warn(RoIssueCode.GatGndMismatch, source, null,
                    $"the GAT has {gat.Width} x {gat.Height} cells, the GND {space.Width} x {space.Height} cubes (expected {2 * space.Width} x {2 * space.Height} cells)");
                return null;
            }

            int count = gat.CellCount;
            var heights = new float[count * 4];
            var types = new byte[count];
            var flags = new byte[count];
            var raw = new byte[count];
            int unknown = 0;
            var unknownValues = new SortedSet<uint>();

            for (int i = 0; i < count; i++)
            {
                for (int k = 0; k < 4; k++)
                    heights[i * 4 + k] = RoSpace.Point(new Vector3(0f, gat.Heights[i * 4 + k], 0f)).y;

                uint value = gat.Types[i];
                var type = RoGatRules.TypeOf(value);
                types[i] = (byte)type;
                flags[i] = (byte)RoGatRules.FlagsOf(type);
                raw[i] = unchecked((byte)value);
                if (type == MapCellType.Other)
                {
                    unknown++;
                    if (unknownValues.Count < MaxListedValues) unknownValues.Add(value);
                }
            }

            if (unknown > 0)
                report?.Warn(RoIssueCode.GatUnknownType, source, null,
                    $"{unknown} cells with an unknown type ({string.Join(", ", unknownValues.Select(v => "0x" + v.ToString("X")))}), treated as blocked");

            // Corner (0, 0) of the grid and the edge of one cell: half a cube (GAT is twice the ground's resolution).
            Vector3 corner = space.CornerUnity(0, 0, 0f);
            float cube = space.CornerUnity(1, 0, 0f).x - corner.x;
            return new RoMapDataArrays
            {
                CellsX = gat.Width,
                CellsZ = gat.Height,
                CellSize = cube / 2f,
                Origin = new Vector2(corner.x, corner.z),
                RowSign = RoMapSpace.RowDirection,
                DiagonalFlip = rules.Diagonal == RoGndDiagonal.H00ToH11,
                Heights = heights,
                Types = types,
                Flags = flags,
                RawTypes = raw,
                UnknownCells = unknown
            };
        }
    }
}
