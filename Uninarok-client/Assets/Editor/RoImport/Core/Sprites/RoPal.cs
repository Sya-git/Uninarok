using UnityEngine;

namespace Uninarok.RoImport.Core.Sprites
{
    /// <summary>A 256-colour table (PAL file or SPR palette). Index 0 is transparent by rule (plan 4.3), whatever its alpha.</summary>
    public sealed class RoPal
    {
        public const int Size = 256;
        public Color32[] Colors;
    }
}
