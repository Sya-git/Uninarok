using System;
using System.Collections.Generic;
using UnityEngine;
using Uninarok.Sprites;

namespace Uninarok.RoImport.Core.Sprites
{
    /// <summary>An ACT flattened into the arrays of a <see cref="SpriteAnimation"/> (PLAN-0007 4.5, D13).</summary>
    public sealed class RoCompiledAct
    {
        public int FileActionCount;
        public bool Directional;
        /// <summary>File action → unique action.</summary>
        public short[] ActionMap = Array.Empty<short>();
        public SpriteAnimation.ActionData[] Actions = Array.Empty<SpriteAnimation.ActionData>();
        public SpriteAnimation.FrameData[] Frames = Array.Empty<SpriteAnimation.FrameData>();
        public SpriteAnimation.LayerData[] Layers = Array.Empty<SpriteAnimation.LayerData>();
        public Vector2Int[] Anchors = Array.Empty<Vector2Int>();
        public string[] Events = Array.Empty<string>();
        public int MaxLayers;
        /// <summary>Layers whose image index is past their type's list: compiled as empty, they draw nothing (SPEC-0007 FR-8 rev. 4, D16).</summary>
        public int OutOfRangeLayers;
        /// <summary>Where the first of them is ("action 8, frame 4, layer 0 uses indexed image 9, the sheet has 9"); null when none.</summary>
        public string FirstOutOfRange;

        /// <summary>Copies the arrays into <paramref name="target"/> with its sprites (by slot) and pixels per unit.</summary>
        public void ApplyTo(SpriteAnimation target, Sprite[] sprites, float pixelsPerUnit)
        {
            target.Sprites = sprites ?? Array.Empty<Sprite>();
            target.PixelsPerUnit = pixelsPerUnit;
            target.FileActionCount = FileActionCount;
            target.Directional = Directional;
            target.ActionMap = ActionMap;
            target.Actions = Actions;
            target.Frames = Frames;
            target.Layers = Layers;
            target.Anchors = Anchors;
            target.Events = Events;
            target.MaxLayers = MaxLayers;
        }
    }

    /// <summary>
    /// Compiles an ACT into flat arrays (PLAN-0007 4.5; SPEC-0007 FR-3, FR-4, FR-5, FR-8). Actions are compared by value
    /// (speed, every frame's layers, event and anchors) and the first of equal ones is kept; the action map points every
    /// file action at its unique action. Empty actions and empty layers stay as data (FR-5); an empty layer is stored as
    /// slot −1 with its unused fields cleared, so equal empties compare equal. A layer that points outside its type's list
    /// draws nothing: it is compiled (and keyed) as an empty layer and counted, so the converter can warn once (FR-8
    /// revision 4, D16); an unknown image type rejects the whole file. Pure: no Unity object is created.
    /// </summary>
    public static class RoActCompiler
    {
        public static bool TryCompile(RoAct act, int indexedCount, int trueColorCount, out RoCompiledAct compiled, out string reason)
        {
            compiled = null;
            if (act == null) return Fail("no animation", out reason);
            if (act.Actions.Count > short.MaxValue) return Fail($"{act.Actions.Count} actions is more than this format stores", out reason);

            var actionMap = new short[act.Actions.Count];
            var unique = new List<RoActAction>();
            var byKey = new Dictionary<string, short>(StringComparer.Ordinal);
            var key = new System.Text.StringBuilder(256);
            int outOfRange = 0;
            string firstOutOfRange = null;

            for (int a = 0; a < act.Actions.Count; a++)
            {
                var action = act.Actions[a];
                if (!Check(action, a, indexedCount, trueColorCount, ref outOfRange, ref firstOutOfRange, out reason)) return false;
                Key(action, key, indexedCount, trueColorCount);
                string k = key.ToString();
                if (!byKey.TryGetValue(k, out short id))
                {
                    id = (short)unique.Count;
                    unique.Add(action);
                    byKey.Add(k, id);
                }
                actionMap[a] = id;
            }

            var actions = new SpriteAnimation.ActionData[unique.Count];
            var frames = new List<SpriteAnimation.FrameData>();
            var layers = new List<SpriteAnimation.LayerData>();
            var anchors = new List<Vector2Int>();
            int maxLayers = 0;
            for (int u = 0; u < unique.Count; u++)
            {
                var action = unique[u];
                actions[u] = new SpriteAnimation.ActionData
                {
                    FirstFrame = frames.Count,
                    FrameCount = action.Frames.Count,
                    FrameMs = RoSpriteRules.FrameMs(action.AnimationSpeed)
                };
                foreach (var frame in action.Frames)
                {
                    maxLayers = Math.Max(maxLayers, frame.Layers.Count);
                    frames.Add(new SpriteAnimation.FrameData
                    {
                        FirstLayer = layers.Count,
                        LayerCount = (short)frame.Layers.Count,
                        EventId = (short)(frame.EventId >= 0 && frame.EventId < act.Events.Count ? frame.EventId : -1),
                        FirstAnchor = anchors.Count,
                        AnchorCount = (short)frame.Anchors.Count
                    });
                    foreach (var l in frame.Layers) layers.Add(Layer(l, indexedCount, trueColorCount));
                    anchors.AddRange(frame.Anchors);
                }
            }

            var events = new string[act.Events.Count];
            for (int i = 0; i < events.Length; i++) events[i] = act.Events[i] ?? string.Empty;

            compiled = new RoCompiledAct
            {
                FileActionCount = act.Actions.Count,
                Directional = RoSpriteRules.IsDirectional(act.Actions.Count),
                ActionMap = actionMap,
                Actions = actions,
                Frames = frames.ToArray(),
                Layers = layers.ToArray(),
                Anchors = anchors.ToArray(),
                Events = events,
                MaxLayers = maxLayers,
                OutOfRangeLayers = outOfRange,
                FirstOutOfRange = firstOutOfRange
            };
            reason = null;
            return true;
        }

        static bool Check(RoActAction action, int a, int indexedCount, int trueColorCount, ref int outOfRange, ref string first, out string reason)
        {
            for (int f = 0; f < action.Frames.Count; f++)
            {
                var frame = action.Frames[f];
                if (frame.Layers.Count > short.MaxValue || frame.Anchors.Count > short.MaxValue)
                    return Fail($"action {a}, frame {f} has too many layers or anchors", out reason);
                for (int l = 0; l < frame.Layers.Count; l++)
                {
                    var layer = frame.Layers[l];
                    if (layer.IsEmpty) continue;
                    if (layer.SpriteType != RoActLayer.Indexed && layer.SpriteType != RoActLayer.TrueColor)
                        return Fail($"action {a}, frame {f}, layer {l} has the unknown image type {layer.SpriteType}", out reason);
                    if (!IsPastSheet(layer, indexedCount, trueColorCount)) continue;
                    outOfRange++;
                    first = first ?? $"action {a}, frame {f}, layer {l} uses {(layer.SpriteType == RoActLayer.Indexed ? "indexed" : "true-colour")} " +
                                     $"image {layer.SpriteIndex}, the sheet has {(layer.SpriteType == RoActLayer.Indexed ? indexedCount : trueColorCount)}";
                }
            }
            reason = null;
            return true;
        }

        /// <summary>A layer whose image is not in its type's list: it draws nothing (FR-8 revision 4, D16).</summary>
        static bool IsPastSheet(RoActLayer l, int indexedCount, int trueColorCount) =>
            !l.IsEmpty && l.SpriteIndex >= (l.SpriteType == RoActLayer.Indexed ? indexedCount : trueColorCount);

        static SpriteAnimation.LayerData Layer(RoActLayer l, int indexedCount, int trueColorCount)
        {
            if (l.IsEmpty || IsPastSheet(l, indexedCount, trueColorCount)) return new SpriteAnimation.LayerData { Slot = -1 };
            return new SpriteAnimation.LayerData
            {
                Slot = (short)RoSpriteRules.Slot(l.SpriteType, l.SpriteIndex, indexedCount),
                OffsetX = Clamp(l.OffsetX),
                OffsetY = Clamp(l.OffsetY),
                Mirror = l.Mirror,
                ScaleX = l.ScaleX,
                ScaleY = l.ScaleY,
                Rotation = l.Rotation,
                Tint = l.Tint
            };
        }

        static short Clamp(int v) => (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, v));

        /// <summary>A text form of everything that makes an action what it is (empty layers in their cleared form).</summary>
        static void Key(RoActAction action, System.Text.StringBuilder key, int indexedCount, int trueColorCount)
        {
            key.Clear();
            key.Append(action.AnimationSpeed.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('|');
            foreach (var frame in action.Frames)
            {
                key.Append(frame.EventId).Append(':');
                foreach (var a in frame.Anchors) key.Append(a.x).Append(',').Append(a.y).Append(';');
                key.Append(':');
                foreach (var l in frame.Layers)
                {
                    if (l.IsEmpty || IsPastSheet(l, indexedCount, trueColorCount))
                    {
                        key.Append("e;");
                        continue;
                    }
                    key.Append(l.SpriteType).Append(',').Append(l.SpriteIndex).Append(',').Append(l.OffsetX).Append(',').Append(l.OffsetY).Append(',')
                       .Append(l.Mirror ? '1' : '0').Append(',')
                       .Append(l.ScaleX.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                       .Append(l.ScaleY.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                       .Append(l.Rotation).Append(',')
                       .Append(l.Tint.r).Append(',').Append(l.Tint.g).Append(',').Append(l.Tint.b).Append(',').Append(l.Tint.a).Append(';');
                }
                key.Append('#');
            }
        }

        static bool Fail(string message, out string reason)
        {
            reason = message;
            return false;
        }
    }
}
