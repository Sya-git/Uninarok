using System;
using Uninarok.RoImport.Core.Paths;

namespace Uninarok.RoImport.Core.Materials
{
    /// <summary>
    /// Identity of a shared material (plan 4.6, SPEC-0002 FR-9): the same texture, surface and sidedness always resolve to
    /// one material asset, however many models ask for it. A ground key (PLAN-0004 D17) is a material of its own on the
    /// ground's Clamp copy of the texture; a model never gets one.
    /// </summary>
    public readonly struct RoMaterialKey : IEquatable<RoMaterialKey>
    {
        public RoMaterialKey(RoPath texture, RoSurface surface, bool twoSided, bool ground = false)
        {
            if (texture.IsEmpty) throw new ArgumentException("A material key needs a texture.", nameof(texture));
            Texture = texture;
            Surface = surface;
            TwoSided = twoSided;
            Ground = ground;
        }

        public RoPath Texture { get; }
        public RoSurface Surface { get; }
        public bool TwoSided { get; }

        /// <summary>A ground material (PLAN-0004 D17): built on the ground's Clamp texture, never used by a model.</summary>
        public bool Ground { get; }

        /// <summary>
        /// <c>opaque</c>, <c>cutout</c> or <c>blend</c>, plus <c>_2s</c> when two-sided and <c>_ground</c> for a ground key:
        /// part of the material's file name.
        /// </summary>
        public string Suffix
        {
            get
            {
                string mode;
                switch (Surface)
                {
                    case RoSurface.Opaque: mode = "opaque"; break;
                    case RoSurface.Cutout: mode = "cutout"; break;
                    case RoSurface.Blend: mode = "blend"; break;
                    default: throw new InvalidOperationException($"Unknown surface {Surface}.");
                }
                if (TwoSided) mode += "_2s";
                return Ground ? mode + "_ground" : mode;
            }
        }

        public bool Equals(RoMaterialKey other) => Texture == other.Texture && Surface == other.Surface && TwoSided == other.TwoSided && Ground == other.Ground;
        public override bool Equals(object obj) => obj is RoMaterialKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Texture.GetHashCode();
                hash = hash * 31 + (int)Surface;
                hash = hash * 31 + (TwoSided ? 1 : 0);
                return hash * 31 + (Ground ? 1 : 0);
            }
        }

        public static bool operator ==(RoMaterialKey a, RoMaterialKey b) => a.Equals(b);
        public static bool operator !=(RoMaterialKey a, RoMaterialKey b) => !a.Equals(b);

        public override string ToString() => Texture.Canonical + "__" + Suffix;
    }
}
