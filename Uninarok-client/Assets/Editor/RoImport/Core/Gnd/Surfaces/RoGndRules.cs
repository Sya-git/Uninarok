using System;

namespace Uninarok.RoImport.Core.Gnd
{
    /// <summary>
    /// The ground rules verified against the reference viewer (PLAN-0004 4.3, TASKS-0004 T0.3), one row per GND version.
    /// </summary>
    public readonly struct RoGndRules
    {
        /// <summary>Heights closer than this, in Ragnarok units, are equal: no wall between them (PLAN-0004 4.3).</summary>
        public const float Epsilon = 0.0001f;

        /// <summary>The rules row of a GND version; false when the table has none.</summary>
        public static bool TryGet(int versionMajor, int versionMinor, out RoGndRules rules) =>
            throw new NotImplementedException("TASKS-0004 T3.2");
    }
}
