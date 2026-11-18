using UnityEngine;
using Uninarok.RoImport.Core.Space;

namespace Uninarok.RoImport.Core.Gnd
{
    /// <summary>
    /// Map space shared by the ground, walkability and map importers (PLAN-0004 4.2, D2): grid corner to Ragnarok and
    /// Unity positions, the centre RSW positions are relative to, and the cube under a position. Every other map position
    /// (GAT cells, RSW objects, gizmos, picking) derives from <see cref="CornerRo"/>. Pure: numbers only.
    /// </summary>
    public readonly struct RoMapSpace
    {
        /// <summary>Sign of the Ragnarok Z axis along rows (TASKS-0004 T0.3: rows grow along +Z).</summary>
        public const int RowDirection = 1;

        /// <summary>Tolerance, in cubes, for a position on a cube's low edge that float rounding puts just below it.</summary>
        const float EdgeTolerance = 1e-4f;

        public RoMapSpace(int width, int height, float zoom)
        {
            Width = width;
            Height = height;
            Zoom = zoom;
        }

        /// <summary>Cubes along columns (x).</summary>
        public int Width { get; }
        /// <summary>Cubes along rows (z).</summary>
        public int Height { get; }
        /// <summary>Cube edge in Ragnarok units, from the map's header (FR-5).</summary>
        public float Zoom { get; }

        /// <summary>The Ragnarok-space position of grid corner (column, row) at the given unconverted height.</summary>
        public Vector3 CornerRo(int column, int row, float roHeight) =>
            new Vector3(column * Zoom, roHeight, row * Zoom * RowDirection);

        /// <summary><see cref="RoSpace.Point"/> of <see cref="CornerRo"/>: a cube of zoom 10 is one Unity unit wide.</summary>
        public Vector3 CornerUnity(int column, int row, float roHeight) => RoSpace.Point(CornerRo(column, row, roHeight));

        /// <summary>The map centre in Ragnarok space, which RSW positions are relative to.</summary>
        public Vector3 CentreOffsetRo => new Vector3(Width * Zoom / 2f, 0f, Height * Zoom * RowDirection / 2f);

        /// <summary>An RSW position (relative to the centre) in Ragnarok map space (T0.3: no sign change).</summary>
        public Vector3 FromRswRo(Vector3 rswPosition) => rswPosition + CentreOffsetRo;

        /// <summary>
        /// The cube whose column and row range contains the position (low edges inside, high edges outside); the height
        /// is ignored. False outside the map.
        /// </summary>
        public bool TryCubeAtUnity(Vector3 unityPosition, out int column, out int row)
        {
            float edge = Zoom * RoUnits.Scale;
            float c = unityPosition.x / edge, r = unityPosition.z / (edge * RowDirection);
            column = Mathf.FloorToInt(c + EdgeTolerance);
            row = Mathf.FloorToInt(r + EdgeTolerance);
            if (c + EdgeTolerance < 0f || r + EdgeTolerance < 0f || column >= Width || row >= Height)
            {
                column = row = -1;
                return false;
            }
            return true;
        }

        /// <summary>The cube that contains a GAT cell: GAT has twice the resolution of the ground.</summary>
        public static (int cubeColumn, int cubeRow) CubeOfGatCell(int gatColumn, int gatRow) => (gatColumn / 2, gatRow / 2);
    }
}
