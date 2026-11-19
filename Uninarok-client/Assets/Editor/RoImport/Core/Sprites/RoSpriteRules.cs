using System;
using UnityEngine;

namespace Uninarok.RoImport.Core.Sprites
{
    /// <summary>The sprite rules of PLAN-0007 4.3. Implemented by TASKS-0007 T4.2.</summary>
    public static class RoSpriteRules
    {
        public const float FrameUnitMs = 25f;
        public const float PixelsPerGatCell = 0f;
        public const float PixelsPerUnit = 0f;
        public const bool OffsetYDown = false, MirrorAboutLayerCentre = false, RotationClockwise = false, ScaleBeforeRotation = false,
                          TintMultipliesAlphaIncluded = false, PaletteIndex0Transparent = false, LaterLayersOnTop = false, EveryActionLoops = false;

        public static float FrameMs(float animationSpeed) => throw new NotImplementedException("TASKS-0007 T4.2");
        public static bool IsDirectional(int actionCount) => throw new NotImplementedException("TASKS-0007 T4.2");
        public static int Slot(int spriteType, int spriteIndex, int indexedCount) => throw new NotImplementedException("TASKS-0007 T4.2");
        public static bool IsStatic(RoAct act) => throw new NotImplementedException("TASKS-0007 T4.2");
        public static Vector2 StaticPivot(int width, int height, int offsetX, int offsetY) => throw new NotImplementedException("TASKS-0007 T4.2");
    }
}
