using UnityEngine;
using Uninarok.RoImport.Core.Space;

namespace Uninarok.RoImport.Core.Sprites
{
    /// <summary>
    /// The sprite rules of PLAN-0007 4.3 (SPEC-0007 FR-1, FR-3, FR-5, FR-6): one named constant or function per rule, each
    /// pinned by <c>RoSpriteRulesTests</c> with the values of the rules check (TASKS-0007 T0.2). A rules change edits this
    /// class and its pinning tests only, and raises <c>RoSpriteConverter.RulesVersion</c>. The runtime player
    /// (<c>Uninarok.Sprites.SpriteActor</c>) applies the layer rules flagged here.
    /// </summary>
    public static class RoSpriteRules
    {
        /// <summary>One <c>AnimationSpeed</c> step: frame time = speed × 25 ms; larger is slower (act-reference §4).</summary>
        public const float FrameUnitMs = 25f;

        /// <summary>Sprite pixels across one GAT cell (rules check: a monster's size against GAT cells).</summary>
        public const float PixelsPerGatCell = 35f;

        /// <summary>Sprite pixels per Unity unit: one GAT cell is <see cref="RoUnits.GatCell"/> units.</summary>
        public const float PixelsPerUnit = PixelsPerGatCell / RoUnits.GatCell;

        /// <summary>A layer offset is the pixel position of the image centre relative to the origin, X right, Y down.</summary>
        public const bool OffsetYDown = true;
        /// <summary>Mirror flips the image horizontally about its own centre, before scale and rotation.</summary>
        public const bool MirrorAboutLayerCentre = true;
        /// <summary>Rotation is in degrees, clockwise on screen, about the layer centre.</summary>
        public const bool RotationClockwise = true;
        /// <summary>Scale multiplies the image size and applies before the rotation.</summary>
        public const bool ScaleBeforeRotation = true;
        /// <summary>The layer tint multiplies the frame colour, alpha included.</summary>
        public const bool TintMultipliesAlphaIncluded = true;
        /// <summary>Palette index 0 is transparent whatever its stored alpha (act-reference §5).</summary>
        public const bool PaletteIndex0Transparent = true;
        /// <summary>Layers draw in file order, later ones on top.</summary>
        public const bool LaterLayersOnTop = true;
        /// <summary>Every action loops; holding the last frame (death) is the caller's choice.</summary>
        public const bool EveryActionLoops = true;

        public static float FrameMs(float animationSpeed) => animationSpeed * FrameUnitMs;

        /// <summary>A file is directional (action = animation × 8 + direction) when its action count is a non-zero multiple of 8.</summary>
        public static bool IsDirectional(int actionCount) => actionCount > 0 && actionCount % 8 == 0;

        /// <summary>The sheet slot of a layer: indexed frames first, then true colour; −1 for an empty layer.</summary>
        public static int Slot(int spriteType, int spriteIndex, int indexedCount)
        {
            if (spriteIndex < 0) return -1;
            return spriteType == RoActLayer.TrueColor ? indexedCount + spriteIndex : spriteIndex;
        }

        /// <summary>Static (FR-11): one action of one frame with at most one layer that draws.</summary>
        public static bool IsStatic(RoAct act)
        {
            if (act == null || act.Actions.Count != 1 || act.Actions[0].Frames.Count != 1) return false;
            int drawn = 0;
            foreach (var layer in act.Actions[0].Frames[0].Layers)
                if (!layer.IsEmpty) drawn++;
            return drawn <= 1;
        }

        /// <summary>
        /// The normalised pivot (Unity: 0, 0 at the bottom-left) that puts the origin of a static sprite where its layer
        /// offset says: the image centre is (<paramref name="offsetX"/>, <paramref name="offsetY"/>) pixels from it, Y down.
        /// </summary>
        public static Vector2 StaticPivot(int width, int height, int offsetX, int offsetY) =>
            new Vector2((width / 2f - offsetX) / width, (height / 2f + offsetY) / height);
    }
}
