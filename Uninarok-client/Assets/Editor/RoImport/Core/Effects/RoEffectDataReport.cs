using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Uninarok.RoImport.Core.Effects
{
    /// <summary>
    /// Result of <see cref="RoEffectDataCheck"/> (PLAN-0012 4.9, D30, SPEC-0012 FR-1).
    /// </summary>
    public sealed class RoEffectDataReport
    {
        /// <summary>Counts present in the working data: .str anywhere, images under texture/effect/, .spr + .act under sprite/이팩트/, .lub under the effect tool folder.</summary>
        public int Str, Textures, SprAct, ToolTables;

        /// <summary>Sorted ordinally by (File, Referrer); each File once, with its first referrer.</summary>
        public IReadOnlyList<(string File, string Referrer)> Missing = Array.Empty<(string, string)>();

        /// <summary>The three warp portal keys with present / missing; shown in <see cref="ToText"/> only.</summary>
        public IReadOnlyList<(string Key, bool Present)> WarpPortal = Array.Empty<(string, bool)>();

        /// <summary>
        /// Deterministic text: counts, missing, warp portal; invariant culture, '\n' line ends, no timestamps. Lines:
        /// <c>str: N</c>, <c>textures: N</c>, <c>sprite/act: N</c>, <c>tool tables: N</c>, <c>missing: N</c>, then one
        /// <c>missing &lt;file&gt; (referenced by &lt;referrer&gt;)</c> per entry, then <c>warp portal &lt;key&gt;: present|missing</c>.
        /// </summary>
        public string ToText()
        {
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append("str: ").Append(Str.ToString(inv)).Append('\n');
            sb.Append("textures: ").Append(Textures.ToString(inv)).Append('\n');
            sb.Append("sprite/act: ").Append(SprAct.ToString(inv)).Append('\n');
            sb.Append("tool tables: ").Append(ToolTables.ToString(inv)).Append('\n');
            sb.Append("missing: ").Append(Missing.Count.ToString(inv)).Append('\n');
            foreach (var m in Missing)
                sb.Append("missing ").Append(m.File).Append(" (referenced by ").Append(m.Referrer).Append(")\n");
            foreach (var w in WarpPortal)
                sb.Append("warp portal ").Append(w.Key).Append(": ").Append(w.Present ? "present" : "missing").Append('\n');
            return sb.ToString();
        }
    }
}
