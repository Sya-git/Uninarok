using System;

namespace Uninarok.Catalog
{
    /// <summary>One colour of a palette set: the colour number and its palette texture (SPEC-0008 FR-2, FR-3; PLAN-0008 4.4).</summary>
    [Serializable]
    public struct PaletteRef
    {
        public int Colour;
        public PaletteReference Palette;
    }

    /// <summary>The palettes of a body or a head, sorted by <see cref="PaletteRef.Colour"/> (PLAN-0008 4.4).</summary>
    [Serializable]
    public struct PaletteSet
    {
        public PaletteRef[] Colours;
    }
}
