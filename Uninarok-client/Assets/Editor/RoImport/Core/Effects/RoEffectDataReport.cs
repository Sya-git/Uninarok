using System;
using System.Collections.Generic;

namespace Uninarok.RoImport.Core.Effects
{
    /// <summary>
    /// Result of <see cref="RoEffectDataCheck"/> (PLAN-0012 4.9, D30, SPEC-0012 FR-1). Stub until T6.2.
    /// </summary>
    public sealed class RoEffectDataReport
    {
        /// <summary>Counts present in the working data: .str anywhere, images under texture/effect/, .spr + .act under sprite/이팩트/, .lub under the effect tool folder.</summary>
        public int Str, Textures, SprAct, ToolTables;

        /// <summary>Sorted ordinally by (File, Referrer); each File once, with its first referrer.</summary>
        public IReadOnlyList<(string File, string Referrer)> Missing;

        /// <summary>The three warp portal keys with present / missing; shown in <see cref="ToText"/> only.</summary>
        public IReadOnlyList<(string Key, bool Present)> WarpPortal;

        /// <summary>Deterministic text: counts, missing, warp portal; invariant culture, '\n' line ends.</summary>
        public string ToText() => throw new NotImplementedException("T6.2");
    }
}
