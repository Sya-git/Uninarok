using System;
using UnityEngine;

namespace Uninarok.RoImport.Core.Gnd
{
    /// <summary>
    /// Map space shared by the ground, walkability and map importers (PLAN-0004 4.2, D2): grid corner to Ragnarok and
    /// Unity positions, the centre RSW positions are relative to, and the cube under a position. Pure: numbers only.
    /// </summary>
    public readonly struct RoMapSpace
    {
        /// <summary>Sign of the Ragnarok Z axis along rows (TASKS-0004 T0.3: rows grow along +Z).</summary>
        public const int RowDirection = 1;

        public RoMapSpace(int width, int height, float zoom)
        {
            Width = width;
            Height = height;
            Zoom = zoom;
        }

        public int Width { get; }
        public int Height { get; }
        public float Zoom { get; }

        public Vector3 CornerRo(int column, int row, float roHeight) => throw Pending();
        public Vector3 CornerUnity(int column, int row, float roHeight) => throw Pending();
        public Vector3 CentreOffsetRo => throw Pending();
        public Vector3 FromRswRo(Vector3 rswPosition) => throw Pending();

        public bool TryCubeAtUnity(Vector3 unityPosition, out int column, out int row) => throw Pending();

        public static (int cubeColumn, int cubeRow) CubeOfGatCell(int gatColumn, int gatRow) => throw Pending();

        static Exception Pending() => new NotImplementedException("TASKS-0004 T2.2");
    }
}
