using System;
using System.Collections.Generic;
using UnityEngine;

namespace Uninarok.RoImport.Core.Sprites
{
    /// <summary>
    /// An SPR file with its values as GRF reads them, unconverted (PLAN-0007 4.1, SPEC-0007 FR-1). Indexed and true-colour
    /// frames are kept in two lists in file order, because an ACT layer counts its sprite index inside its own type.
    /// </summary>
    public sealed class RoSpr
    {
        public int VersionMajor, VersionMinor;
        /// <summary>Palette frames (ACT <c>SpriteType</c> 0), file order.</summary>
        public IReadOnlyList<RoSprFrame> Indexed = Array.Empty<RoSprFrame>();
        /// <summary>True-colour frames (ACT <c>SpriteType</c> 1), file order.</summary>
        public IReadOnlyList<RoSprFrame> TrueColor = Array.Empty<RoSprFrame>();
        /// <summary>The embedded palette (256 entries); null when the file has no indexed frame and no palette.</summary>
        public RoPal Palette;

        /// <summary>Number of sheet slots: indexed frames first, then true-colour frames (plan 4.2).</summary>
        public int SlotCount => Indexed.Count + TrueColor.Count;

        /// <summary>The frame in sheet slot <paramref name="slot"/> (indexed frames first, then true colour).</summary>
        public RoSprFrame Slot(int slot) => slot < Indexed.Count ? Indexed[slot] : TrueColor[slot - Indexed.Count];

        public override string ToString() => $"RoSpr {VersionMajor}.{VersionMinor} ({Indexed.Count} indexed, {TrueColor.Count} true colour)";
    }

    /// <summary>
    /// One SPR image, top row first. Exactly one of <see cref="Indices"/> (palette indices, one byte per pixel) or
    /// <see cref="Rgba"/> (R, G, B, A, four bytes per pixel) is set.
    /// </summary>
    public sealed class RoSprFrame
    {
        public int Width, Height;
        public byte[] Indices;
        public byte[] Rgba;

        public bool IsIndexed => Indices != null;

        public static RoSprFrame FromIndices(int width, int height, byte[] indices) =>
            new RoSprFrame { Width = width, Height = height, Indices = indices };

        public static RoSprFrame FromRgba(int width, int height, byte[] rgba) =>
            new RoSprFrame { Width = width, Height = height, Rgba = rgba };
    }
}
