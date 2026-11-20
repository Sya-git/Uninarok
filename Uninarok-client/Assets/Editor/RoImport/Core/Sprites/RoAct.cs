using System;
using System.Collections.Generic;
using UnityEngine;

namespace Uninarok.RoImport.Core.Sprites
{
    /// <summary>
    /// An ACT file with its values as GRF reads them, unconverted (PLAN-0007 4.1, SPEC-0007 FR-3). An ACT names no SPR: the
    /// caller pairs it with an image set (act-reference §6).
    /// </summary>
    public sealed class RoAct
    {
        public int VersionMajor, VersionMinor;
        /// <summary>Actions in file order; for directional files action = animation × 8 + direction.</summary>
        public IReadOnlyList<RoActAction> Actions = Array.Empty<RoActAction>();
        /// <summary>The event table (<c>atk</c>, sound names); a frame's <see cref="RoActFrame.EventId"/> indexes it.</summary>
        public IReadOnlyList<string> Events = Array.Empty<string>();

        public override string ToString() => $"RoAct {VersionMajor}.{VersionMinor} ({Actions.Count} actions, {Events.Count} events)";
    }

    public sealed class RoActAction
    {
        /// <summary>Frame delay factor: frame time = <c>AnimationSpeed × 25</c> ms (act-reference §4).</summary>
        public float AnimationSpeed;
        public IReadOnlyList<RoActFrame> Frames = Array.Empty<RoActFrame>();
    }

    public sealed class RoActFrame
    {
        /// <summary>Layers in draw order (later on top).</summary>
        public IReadOnlyList<RoActLayer> Layers = Array.Empty<RoActLayer>();
        /// <summary>Index into <see cref="RoAct.Events"/>, or −1 for none.</summary>
        public int EventId = -1;
        /// <summary>Attach points in pixels, Y down, as in the file.</summary>
        public IReadOnlyList<Vector2Int> Anchors = Array.Empty<Vector2Int>();
    }

    /// <summary>One layer of a frame, raw (plan 4.1). <see cref="SpriteIndex"/> −1 is an empty layer that draws nothing.</summary>
    public readonly struct RoActLayer : IEquatable<RoActLayer>
    {
        public const int Indexed = 0, TrueColor = 1;

        public readonly int SpriteType, SpriteIndex;
        public readonly int OffsetX, OffsetY;
        public readonly bool Mirror;
        public readonly float ScaleX, ScaleY;
        /// <summary>Degrees.</summary>
        public readonly int Rotation;
        public readonly Color32 Tint;

        public RoActLayer(int spriteType, int spriteIndex, int offsetX, int offsetY, bool mirror, float scaleX, float scaleY,
            int rotation, Color32 tint)
        {
            SpriteType = spriteType;
            SpriteIndex = spriteIndex;
            OffsetX = offsetX;
            OffsetY = offsetY;
            Mirror = mirror;
            ScaleX = scaleX;
            ScaleY = scaleY;
            Rotation = rotation;
            Tint = tint;
        }

        /// <summary>A plain layer: indexed type, no mirror, scale 1, no rotation, white tint.</summary>
        public static RoActLayer Simple(int spriteIndex, int offsetX = 0, int offsetY = 0, int spriteType = Indexed) =>
            new RoActLayer(spriteType, spriteIndex, offsetX, offsetY, false, 1f, 1f, 0, new Color32(255, 255, 255, 255));

        /// <summary>An empty layer as the files store it (index −1, zero scale and tint).</summary>
        public static RoActLayer Empty => new RoActLayer(Indexed, -1, 0, 0, false, 0f, 0f, 0, default);

        public bool IsEmpty => SpriteIndex < 0;

        public bool Equals(RoActLayer o) =>
            SpriteType == o.SpriteType && SpriteIndex == o.SpriteIndex && OffsetX == o.OffsetX && OffsetY == o.OffsetY &&
            Mirror == o.Mirror && ScaleX.Equals(o.ScaleX) && ScaleY.Equals(o.ScaleY) && Rotation == o.Rotation &&
            Tint.r == o.Tint.r && Tint.g == o.Tint.g && Tint.b == o.Tint.b && Tint.a == o.Tint.a;

        public override bool Equals(object obj) => obj is RoActLayer o && Equals(o);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = SpriteType;
                h = h * 31 + SpriteIndex;
                h = h * 31 + OffsetX;
                h = h * 31 + OffsetY;
                h = h * 31 + (Mirror ? 1 : 0);
                h = h * 31 + ScaleX.GetHashCode();
                h = h * 31 + ScaleY.GetHashCode();
                h = h * 31 + Rotation;
                h = h * 31 + (Tint.r | Tint.g << 8 | Tint.b << 16 | Tint.a << 24);
                return h;
            }
        }
    }
}
