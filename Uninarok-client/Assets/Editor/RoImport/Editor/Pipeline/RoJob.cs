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
        Ground
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
