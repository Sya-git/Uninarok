using System.Collections.Generic;
using UnityEngine;

namespace Uninarok.Sprites.Tests
{
    /// <summary>
    /// A small <see cref="SpriteAnimation"/> built in code for the player tests (TASKS-0007 T2.2). Pixels per unit is 10, so
    /// a 10-pixel offset is one unit. Three sprites (slots 0 to 2) cut from one 32 × 8 texture.
    /// <list type="bullet">
    /// <item>Animation 0 (idle, all 8 directions → unique 0): 100 ms, 2 frames. Frame 0: slot 0 at (10, −20), event "atk",
    /// anchor (1, −56). Frame 1: slot 1 at (0, −20) mirrored, scale (2, 0.5), rotation 90, tint (255, 0, 0, 128), then slot 2
    /// at (5, −5); event "x.wav", anchor (2, −57).</item>
    /// <item>Animation 1 (walk → unique 1): 50 ms, 3 frames: slot 0; an empty layer (−1); slot 2.</item>
    /// <item>Animation 2 (→ unique 2): one frame whose only layer is empty: real data that draws nothing (FR-5).</item>
    /// </list>
    /// File action count 24 (3 animations × 8 directions); MaxLayers 2.
    /// </summary>
    public sealed class SpriteActorTestData
    {
        public const float Ppu = 10f;
        public readonly List<Object> Owned = new List<Object>();
        public SpriteAnimation Animation;

        public SpriteActorTestData()
        {
            var texture = new Texture2D(32, 8, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            Owned.Add(texture);
            var sprites = new Sprite[3];
            for (int i = 0; i < sprites.Length; i++)
            {
                sprites[i] = Sprite.Create(texture, new Rect(i * 10, 0, 8, 8), new Vector2(0.5f, 0.5f), Ppu);
                sprites[i].name = "f" + i;
                Owned.Add(sprites[i]);
            }

            var white = new Color32(255, 255, 255, 255);
            var a = ScriptableObject.CreateInstance<SpriteAnimation>();
            Owned.Add(a);
            a.Sprites = sprites;
            a.PixelsPerUnit = Ppu;
            a.FileActionCount = 24;
            a.Directional = true;
            a.ActionMap = new short[24];
            for (int i = 0; i < 24; i++) a.ActionMap[i] = (short)(i / 8);
            a.Actions = new[]
            {
                new SpriteAnimation.ActionData { FirstFrame = 0, FrameCount = 2, FrameMs = 100f },
                new SpriteAnimation.ActionData { FirstFrame = 2, FrameCount = 3, FrameMs = 50f },
                new SpriteAnimation.ActionData { FirstFrame = 5, FrameCount = 1, FrameMs = 100f },
            };
            a.Frames = new[]
            {
                new SpriteAnimation.FrameData { FirstLayer = 0, LayerCount = 1, EventId = 0, FirstAnchor = 0, AnchorCount = 1 },
                new SpriteAnimation.FrameData { FirstLayer = 1, LayerCount = 2, EventId = 1, FirstAnchor = 1, AnchorCount = 1 },
                new SpriteAnimation.FrameData { FirstLayer = 3, LayerCount = 1, EventId = -1 },
                new SpriteAnimation.FrameData { FirstLayer = 4, LayerCount = 1, EventId = -1 },
                new SpriteAnimation.FrameData { FirstLayer = 5, LayerCount = 1, EventId = -1 },
                new SpriteAnimation.FrameData { FirstLayer = 6, LayerCount = 1, EventId = -1 },
            };
            a.Layers = new[]
            {
                Layer(0, 10, -20, white),
                new SpriteAnimation.LayerData { Slot = 1, OffsetX = 0, OffsetY = -20, Mirror = true, ScaleX = 2f, ScaleY = 0.5f, Rotation = 90f, Tint = new Color32(255, 0, 0, 128) },
                Layer(2, 5, -5, white),
                Layer(0, 0, 0, white),
                Layer(-1, 0, 0, default),
                Layer(2, 0, 0, white),
                Layer(-1, 0, 0, default),
            };
            a.Anchors = new[] { new Vector2Int(1, -56), new Vector2Int(2, -57) };
            a.Events = new[] { "atk", "x.wav" };
            a.MaxLayers = 2;
            Animation = a;
        }

        static SpriteAnimation.LayerData Layer(short slot, short x, short y, Color32 tint) =>
            new SpriteAnimation.LayerData { Slot = slot, OffsetX = x, OffsetY = y, ScaleX = 1f, ScaleY = 1f, Tint = tint };

        public SpriteActor NewActor(string name = "actor")
        {
            var go = new GameObject(name);
            Owned.Add(go);
            go.SetActive(false);
            var actor = go.AddComponent<SpriteActor>();
            actor.Data = Animation;
            go.SetActive(true);
            return actor;
        }

        public void Destroy()
        {
            for (int i = Owned.Count - 1; i >= 0; i--)
                if (Owned[i] != null) Object.DestroyImmediate(Owned[i]);
            Owned.Clear();
        }
    }
}
