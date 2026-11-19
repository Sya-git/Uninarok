using System.Collections.Generic;
using UnityEngine;

namespace Uninarok.RoImport.Core.Sprites
{
    /// <summary>Where each frame of an SPR goes in its sheet (PLAN-0007 4.2). Implemented by TASKS-0007 T3.2.</summary>
    public static class RoSpriteSheetPacker
    {
        public const int Padding = 2, MaxSize = 4096;

        public static bool TryPack(IReadOnlyList<Vector2Int> sizes, out RoSheetLayout layout)
        {
            layout = null;
            return false;
        }
    }

    /// <summary>A sheet layout: size and one rectangle per slot, in pixels with the origin at the <b>top-left</b> (rows top first).</summary>
    public sealed class RoSheetLayout
    {
        public int Width, Height;
        public RectInt[] Rects;
    }
}
