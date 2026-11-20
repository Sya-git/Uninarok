using Uninarok.RoImport.Core.Paths;

namespace Uninarok.RoImport.Editor.Pipeline
{
    /// <summary>The kinds of conversion (plan 4.10). Model, Ground, MapData, Map and Sprite are added by PLAN-0003 to 0007.</summary>
    public enum RoJobKind
    {
        Texture,
        /// <summary>An RSM model (PLAN-0003).</summary>
        Model,
        /// <summary>A GND ground (PLAN-0004).</summary>
        Ground,
        /// <summary>The map data of a GAT file (PLAN-0005).</summary>
        MapData,
        /// <summary>A map scene from an RSW file (PLAN-0006).</summary>
        Map,
        /// <summary>An ACT with its SPR: a sheet and an animation, or a sprite when static (PLAN-0007).</summary>
        Sprite,
        /// <summary>A PAL palette to a 256 × 1 texture (PLAN-0007).</summary>
        Palette,
        /// <summary>One record of the sprite catalog, path <c>catalog/&lt;category&gt;/&lt;key&gt;</c> (PLAN-0008 4.7).</summary>
        CatalogEntry,
        /// <summary>The index of one catalog category, path <c>catalog/&lt;category&gt;/_index</c>; after every entry (PLAN-0008 4.7, D6).</summary>
        CatalogIndex
    }

    /// <summary>One conversion request: a kind and a Ragnarok path (plan 4.10, SPEC-0002 FR-2).</summary>
    public readonly struct RoJob
    {
        public RoJob(RoJobKind kind, RoPath path)
        {
            Kind = kind;
            Path = path;
        }

        public RoJobKind Kind { get; }
        public RoPath Path { get; }

        public override string ToString() => $"{Kind} {Path}";
    }
}
