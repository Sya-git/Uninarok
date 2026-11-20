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

        /// <summary>The palette of colour number <paramref name="colour"/>; false when the set has none.</summary>
        public bool TryGet(int colour, out PaletteReference palette)
        {
            var colours = Colours;
            int lo = 0, hi = (colours?.Length ?? 0) - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                int c = colours[mid].Colour;
                if (c == colour)
                {
                    palette = colours[mid].Palette;
                    return true;
                }
                if (c < colour) lo = mid + 1;
                else hi = mid - 1;
            }
            palette = null;
            return false;
        }
    }
}
