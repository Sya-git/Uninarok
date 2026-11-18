using System;

namespace Uninarok.Map
{
    /// <summary>
    /// Map-wide settings from the RSW (water level, lighting, music), filled by PLAN-0006 (SPEC-0006 FR-7). Empty after
    /// PLAN-0005 4.3, so that one map data asset per map holds both.
    /// </summary>
    [Serializable]
    public sealed class MapSettings
    {
    }
}
