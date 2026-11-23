using System;
using UnityEngine;

namespace Uninarok.Sprites
{
    /// <summary>
    /// One converted ACT (PLAN-0007 4.5, SPEC-0007 FR-3, FR-4, FR-5): its actions, frames and layers as flat arrays, with an
    /// action map so that identical actions are stored once. Pixel values are kept as in the file (Y down); the player
    /// converts them with <see cref="PixelsPerUnit"/>. Written by the importer, read-only at run time.
    /// </summary>
    public sealed class SpriteAnimation : ScriptableObject
    {
        /// <summary>One <c>AnimationSpeed</c> step in milliseconds (act-reference §4).</summary>
        public const float FrameUnitMs = 25f;

        /// <summary>Directions per animation in a directional file (action = animation × 8 + direction).</summary>
        public const int Directions = 8;

        /// <summary>Sprites by sheet slot: indexed frames first, then true colour (plan 4.2). Shared with every animation of the sheet.</summary>
        public Sprite[] Sprites = Array.Empty<Sprite>();
        public float PixelsPerUnit = 1f;
        /// <summary>The ACT's action count.</summary>
        public int FileActionCount;
        public bool Directional;
        /// <summary>File action → unique action (FR-4).</summary>
        public short[] ActionMap = Array.Empty<short>();
        public ActionData[] Actions = Array.Empty<ActionData>();
        public FrameData[] Frames = Array.Empty<FrameData>();
        public LayerData[] Layers = Array.Empty<LayerData>();
        /// <summary>Attach points in pixels, Y down, as in the file.</summary>
        public Vector2Int[] Anchors = Array.Empty<Vector2Int>();
        /// <summary>The event table: <c>atk</c> and sound names.</summary>
        public string[] Events = Array.Empty<string>();
        /// <summary>The largest layer count of any frame: the renderer slots a player needs.</summary>
        public int MaxLayers;

        [Serializable]
        public struct ActionData
        {
            public int FirstFrame;
            public int FrameCount;
            public float FrameMs;
        }

        [Serializable]
        public struct FrameData
        {
            public int FirstLayer;
            public short LayerCount;
            /// <summary>Index into <see cref="SpriteAnimation.Events"/>, or −1.</summary>
            public short EventId;
            public int FirstAnchor;
            public short AnchorCount;
        }

        [Serializable]
        public struct LayerData
        {
            /// <summary>Sheet slot, or −1 for an empty layer that draws nothing.</summary>
            public short Slot;
            public short OffsetX, OffsetY;
            public bool Mirror;
            public float ScaleX, ScaleY;
            /// <summary>Degrees, as in the file.</summary>
            public float Rotation;
            public Color32 Tint;
        }

        /// <summary>
        /// The rectangle a frame's layers cover in the actor's plane, in Unity units, as <c>SpriteActor</c> places them (offset, mirror,
        /// scale, rotation). False when no layer draws (SPEC-0011 FR-2, FR-10: picking; PLAN-0011 4.9).
        /// </summary>
        public bool TryGetFrameRect(int frameIndex, out Rect rect)
        {
            rect = default;
            if (frameIndex < 0 || frameIndex >= Frames.Length) return false;
            var frame = Frames[frameIndex];
            float ppu = PixelsPerUnit > 0f ? PixelsPerUnit : 1f;
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            for (int i = 0; i < frame.LayerCount; i++)
            {
                var layer = Layers[frame.FirstLayer + i];
                if (layer.Slot < 0 || layer.Slot >= Sprites.Length || ReferenceEquals(Sprites[layer.Slot], null)) continue;
                var b = Sprites[layer.Slot].bounds;
                float radians = -layer.Rotation * Mathf.Deg2Rad, cos = Mathf.Cos(radians), sin = Mathf.Sin(radians);
                float ox = layer.OffsetX / ppu, oy = -layer.OffsetY / ppu;
                for (int c = 0; c < 4; c++)
                {
                    float x = (c & 1) == 0 ? b.min.x : b.max.x, y = (c & 2) == 0 ? b.min.y : b.max.y;
                    if (layer.Mirror) x = -x;
                    x *= layer.ScaleX;
                    y *= layer.ScaleY;
                    float rx = ox + x * cos - y * sin, ry = oy + x * sin + y * cos;
                    if (rx < minX) minX = rx;
                    if (rx > maxX) maxX = rx;
                    if (ry < minY) minY = ry;
                    if (ry > maxY) maxY = ry;
                }
            }
            if (minX > maxX) return false;
            rect = Rect.MinMaxRect(minX, minY, maxX, maxY);
            return true;
        }

        /// <summary>The number of animations: action count / 8 for a directional file, the action count otherwise.</summary>
        public int AnimationCount => Directional ? FileActionCount / Directions : FileActionCount;

        /// <summary>
        /// The unique action for <paramref name="animation"/> in <paramref name="direction"/> (FR-5). An action inside the
        /// file's count is returned even when it is empty; outside, <paramref name="policy"/> decides: <see cref="SpriteMissingAction.DrawNothing"/>
        /// returns false, <see cref="SpriteMissingAction.Fallback"/> tries the same animation in direction 0, then idle in
        /// the requested direction, then the first action. Non-directional files ignore the direction.
        /// </summary>
        public bool TryGetAction(int animation, int direction, SpriteMissingAction policy, out int uniqueAction)
        {
            uniqueAction = -1;
            if (FileActionCount <= 0 || animation < 0) return false;
            direction = ((direction % Directions) + Directions) % Directions;

            if (TryFileAction(FileAction(animation, direction), out uniqueAction)) return true;
            if (policy == SpriteMissingAction.DrawNothing) return false;

            if (Directional && TryFileAction(FileAction(animation, 0), out uniqueAction)) return true;
            if (TryFileAction(FileAction(0, direction), out uniqueAction)) return true;
            return TryFileAction(0, out uniqueAction);
        }

        int FileAction(int animation, int direction) => Directional ? animation * Directions + direction : animation;

        bool TryFileAction(int fileAction, out int uniqueAction)
        {
            if (fileAction >= 0 && fileAction < FileActionCount && fileAction < ActionMap.Length)
            {
                uniqueAction = ActionMap[fileAction];
                return true;
            }
            uniqueAction = -1;
            return false;
        }
    }
}
