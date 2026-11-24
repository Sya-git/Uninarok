using UnityEngine;

namespace Uninarok.Effects
{
    [CreateAssetMenu] public sealed class EffectSettings : ScriptableObject
    {
        public Shader Shader;                       // Uninarok/RoEffect, set in T5.1
        [Tooltip("Lead (roBrowserLegacy): 35 STR pixels per cell, the same ratio as sprites (70 PPU, 1 cell = 0.5 u). Doubtful until the owner check.")] public float PixelsPerCell = 35f;
        [Tooltip("Lead (roBrowserLegacy): canvas 640 wide, art centre (320, 320) on the effect position.")] public Vector2 Pivot = new Vector2(320, 320);
        [Tooltip("Lead (roBrowserLegacy): the art centre sits half a cell lower than the effect position.")] public float VerticalBaseCells = 0.5f;
        [Tooltip("Lead (roBrowserLegacy), checked in T0.1 (str-reference.md): 1024 angle units per turn.")] public float AngleUnitsPerTurn = 1024f;
        [Tooltip("Lead: clip-space depth bias toward the camera, as Uninarok/RoSprite (PLAN-0009 D7).")] public float DepthBias = 0.5f;
        public Texture2D PlaceholderTexture;        // set in T5.2
        [Tooltip("Lead: seconds a placeholder part plays (plan 4.6).")] public float PlaceholderSeconds = 1f;
        [Tooltip("Lead: client tick 1/60 s. Frame step and frame number for authored entries (FR-11).")] public float AuthoredFrameRate = 60f;
    }
}
