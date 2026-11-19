using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Uninarok.RoImport.Core.Sprites;

namespace Uninarok.RoImport.Tests.Sprites
{
    /// <summary>
    /// Synthetic SPR and ACT values for the pure sprite tests (TASKS-0007 T1.1, SPEC-0007 FR-1, FR-3, FR-8). Built in code,
    /// raw as GRF gives them: pixels top row first, offsets in pixels with Y down, rotation in degrees.
    /// </summary>
    public static class SpriteTestData
    {
        public static readonly Color32 White = new Color32(255, 255, 255, 255);

        // ---- palettes and frames ----

        /// <summary>A palette where entry i is (i, 255 − i, i / 2, 255); entry 0 has alpha 255 to prove index 0 is transparent by rule.</summary>
        public static RoPal Palette(int seed = 0)
        {
            var colors = new Color32[RoPal.Size];
            for (int i = 0; i < colors.Length; i++)
                colors[i] = new Color32((byte)((i + seed) & 255), (byte)(255 - i), (byte)(i / 2), 255);
            return new RoPal { Colors = colors };
        }

        /// <summary>An indexed frame whose pixel (x, y) is index <c>1 + (x + y + seed) % 200</c>, with a transparent border of index 0.</summary>
        public static RoSprFrame IndexedFrame(int width, int height, int seed = 0)
        {
            var pixels = new byte[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    bool border = x == 0 || y == 0 || x == width - 1 || y == height - 1;
                    pixels[y * width + x] = border ? (byte)0 : (byte)(1 + (x + y + seed) % 200);
                }
            return RoSprFrame.FromIndices(width, height, pixels);
        }

        /// <summary>A true-colour frame: pixel (x, y) = (x, y, seed, alpha) with alpha 0 on the left column and 128 on the right one.</summary>
        public static RoSprFrame TrueColorFrame(int width, int height, int seed = 0)
        {
            var pixels = new byte[width * height * 4];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int i = (y * width + x) * 4;
                    pixels[i] = (byte)x;
                    pixels[i + 1] = (byte)y;
                    pixels[i + 2] = (byte)seed;
                    pixels[i + 3] = x == 0 ? (byte)0 : x == width - 1 ? (byte)128 : (byte)255;
                }
            return RoSprFrame.FromRgba(width, height, pixels);
        }

        public static RoSpr Spr(IEnumerable<RoSprFrame> indexed, IEnumerable<RoSprFrame> trueColor = null, RoPal palette = null) =>
            new RoSpr
            {
                VersionMajor = 2,
                VersionMinor = 1,
                Indexed = indexed.ToList(),
                TrueColor = (trueColor ?? Enumerable.Empty<RoSprFrame>()).ToList(),
                Palette = palette ?? Palette(),
            };

        // ---- ACT building blocks ----

        public static RoActFrame Frame(params RoActLayer[] layers) => new RoActFrame { Layers = layers };

        public static RoActFrame Frame(int eventId, Vector2Int[] anchors, params RoActLayer[] layers) =>
            new RoActFrame { Layers = layers, EventId = eventId, Anchors = anchors };

        public static RoActAction Action(float speed, params RoActFrame[] frames) => new RoActAction { AnimationSpeed = speed, Frames = frames };

        public static RoAct Act(IEnumerable<RoActAction> actions, params string[] events) =>
            new RoAct { VersionMajor = 2, VersionMinor = 5, Actions = actions.ToList(), Events = events };

        /// <summary>An action of <paramref name="frames"/> frames, each one layer showing <c>firstSprite + f</c>.</summary>
        static RoActAction Run(int firstSprite, int frames, float speed = 4f, int offsetY = -20, bool mirror = false)
        {
            var list = new RoActFrame[frames];
            for (int f = 0; f < frames; f++)
                list[f] = Frame(new RoActLayer(RoActLayer.Indexed, firstSprite + f, 0, offsetY, mirror, 1f, 1f, 0, White));
            return Action(speed, list);
        }

        // ---- samples (TASKS T1.1) ----

        /// <summary>
        /// An indexed monster: 8 frames, two animations (idle 2 frames, walk 3 frames) × 8 directions = 16 actions. Directions
        /// repeat in pairs (0 = 1, 2 = 3, …) and odd directions are the even ones mirrored, so 4 + 4 = 8 distinct actions.
        /// </summary>
        public static (RoSpr spr, RoAct act) Monster()
        {
            var spr = Spr(Enumerable.Range(0, 8).Select(i => IndexedFrame(10 + i, 12, i)));
            var actions = new List<RoActAction>();
            for (int anim = 0; anim < 2; anim++)
                for (int dir = 0; dir < 8; dir++)
                {
                    int pair = dir / 2;
                    bool mirror = pair >= 2;
                    actions.Add(anim == 0 ? Run(pair % 2 * 2, 2, 4f, -20, mirror) : Run(4 + pair % 2, 3, 2.5f, -20, mirror));
                }
            return (spr, Act(actions));
        }

        public const int MonsterActions = 16, MonsterDistinctActions = 8;

        /// <summary>A true-colour-only effect: 3 frames, no palette, one non-directional action of 3 frames (action count 1 → not static).</summary>
        public static (RoSpr spr, RoAct act) TrueColorEffect()
        {
            var spr = Spr(new RoSprFrame[0], Enumerable.Range(0, 3).Select(i => TrueColorFrame(16, 16, i)));
            spr.Palette = null;
            var act = Act(new[]
            {
                Action(2f, Enumerable.Range(0, 3).Select(i => Frame(RoActLayer.Simple(i, 0, -8, RoActLayer.TrueColor))).ToArray())
            });
            return (spr, act);
        }

        /// <summary>
        /// A mixed sprite: 2 indexed and 2 true-colour frames; one action whose frame draws indexed 1 under true colour 0 with
        /// mirror, scale, rotation and tint set.
        /// </summary>
        public static (RoSpr spr, RoAct act) Mixed()
        {
            var spr = Spr(new[] { IndexedFrame(8, 8, 1), IndexedFrame(9, 7, 2) }, new[] { TrueColorFrame(6, 5, 1), TrueColorFrame(7, 4, 2) });
            var act = Act(new[]
            {
                Action(4f, Frame(
                    new RoActLayer(RoActLayer.Indexed, 1, -3, -10, false, 1f, 1f, 0, White),
                    new RoActLayer(RoActLayer.TrueColor, 0, 5, -12, true, 0.5f, 2f, 90, new Color32(255, 128, 64, 200)))),
                Action(4f, Frame(RoActLayer.Simple(0))),
            });
            return (spr, act);
        }

        /// <summary>
        /// A weapon-like ACT over 4 frames: 13 animations × 8 directions = 104 actions. Every action is empty (one frame, one
        /// −1 layer) except animation 4 (standby: 2 frames drawn) and animation 10 (attack: 3 frames, the middle one empty).
        /// </summary>
        public static (RoSpr spr, RoAct act) Weapon()
        {
            var spr = Spr(Enumerable.Range(0, 4).Select(i => IndexedFrame(12, 20, i)));
            var actions = new List<RoActAction>();
            for (int anim = 0; anim < 13; anim++)
                for (int dir = 0; dir < 8; dir++)
                {
                    if (anim == 4) actions.Add(Run(0, 2, 4f, -30));
                    else if (anim == 10)
                        actions.Add(Action(4f, Frame(RoActLayer.Simple(2, 10, -40)), Frame(RoActLayer.Empty), Frame(RoActLayer.Simple(3, -10, -25))));
                    else actions.Add(Action(4f, Frame(RoActLayer.Empty)));
                }
            return (spr, Act(actions));
        }

        public const int WeaponActions = 104;

        /// <summary>A static ACT (ground item): one action, one frame, one layer at offset (0, 0).</summary>
        public static (RoSpr spr, RoAct act) Static(int offsetX = 0, int offsetY = 0)
        {
            var spr = Spr(new[] { IndexedFrame(14, 16) });
            return (spr, Act(new[] { Action(6f, Frame(RoActLayer.Simple(0, offsetX, offsetY))) }));
        }

        /// <summary>A body-like ACT with one anchor per frame (8 actions of 2 frames, anchor (1, −56 − f)).</summary>
        public static (RoSpr spr, RoAct act) Anchored()
        {
            var spr = Spr(new[] { IndexedFrame(10, 30), IndexedFrame(10, 31) });
            var actions = Enumerable.Range(0, 8).Select(dir => Action(4f,
                Frame(-1, new[] { new Vector2Int(1, -56) }, RoActLayer.Simple(0, 0, -25)),
                Frame(-1, new[] { new Vector2Int(1, -57) }, RoActLayer.Simple(1, 0, -26)))).ToList();
            return (spr, Act(actions));
        }

        /// <summary>A monster-like ACT with events: walk frame 0 plays "x_move.wav", attack frame 1 is "atk" (8 + 8 actions).</summary>
        public static (RoSpr spr, RoAct act) WithEvents()
        {
            var spr = Spr(new[] { IndexedFrame(10, 10), IndexedFrame(10, 11) });
            var actions = new List<RoActAction>();
            for (int dir = 0; dir < 8; dir++)
                actions.Add(Action(4f, Frame(1, new Vector2Int[0], RoActLayer.Simple(0)), Frame(RoActLayer.Simple(1))));
            for (int dir = 0; dir < 8; dir++)
                actions.Add(Action(4f, Frame(RoActLayer.Simple(0)), Frame(0, new Vector2Int[0], RoActLayer.Simple(1))));
            return (spr, Act(actions, "atk", "x_move.wav"));
        }

        /// <summary>An ACT whose second action points at indexed frame 5 of a 2-frame SPR (FR-8).</summary>
        public static (RoSpr spr, RoAct act) OutOfRange()
        {
            var spr = Spr(new[] { IndexedFrame(8, 8), IndexedFrame(8, 8, 1) });
            return (spr, Act(new[] { Action(4f, Frame(RoActLayer.Simple(0))), Action(4f, Frame(RoActLayer.Simple(5))) }));
        }

        /// <summary>A 100-frame SPR (frames 8 × 8 to 17 × 17) with one action that plays them all.</summary>
        public static (RoSpr spr, RoAct act) Hundred()
        {
            var spr = Spr(Enumerable.Range(0, 100).Select(i => IndexedFrame(8 + i % 10, 8 + i / 10, i)));
            return (spr, Act(new[] { Action(1f, Enumerable.Range(0, 100).Select(i => Frame(RoActLayer.Simple(i))).ToArray()) }));
        }
    }
}
