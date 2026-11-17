namespace Uninarok.RoImport.Core.Space
{
    /// <summary>
    /// Unit conventions shared by every converter (plan 4.3, SPEC-0002 FR-7). With these and <see cref="RoSpace"/>,
    /// these are the only places that hold the scale numbers; converters build on them instead of repeating them.
    /// </summary>
    public static class RoUnits
    {
        /// <summary>One GND ground cube edge, in Ragnarok units.</summary>
        public const float CubeRoUnits = 10f;

        /// <summary>Ragnarok unit to Unity unit, so that one ground cube is exactly one Unity unit.</summary>
        public const float Scale = 1f / CubeRoUnits;

        /// <summary>One GAT cell edge in Unity units: half a ground cube.</summary>
        public const float GatCell = CubeRoUnits * Scale / 2f;
    }
}
