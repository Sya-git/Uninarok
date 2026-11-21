using System.Collections.Generic;
using Uninarok.RoImport.Core.Catalog.Lua;

namespace Uninarok.RoImport.Core.Catalog.Tables
{
    /// <summary>
    /// The display scale of units from <c>System/monster_size_effect_new.lub</c> (<c>tbl[jobId].MonsterSize</c>;
    /// PLAN-0008 4.2, 4.3.1; FR-1). A job id that is not in the table, or has no size, draws at scale 1.
    /// </summary>
    public sealed class RoMonsterScaleTable
    {
        public const string File = "monster_size_effect_new.lub";
        public const float DefaultScale = 1f;

        RoMonsterScaleTable(Dictionary<int, float> scales) => Scales = scales;

        /// <summary>Job id → scale, for the ids the table lists with a size.</summary>
        public IReadOnlyDictionary<int, float> Scales { get; }

        public float ScaleOf(int jobId) => Scales.TryGetValue(jobId, out float s) ? s : DefaultScale;

        public static bool TryRead(string lua, out RoMonsterScaleTable table, out string reason)
        {
            table = null;
            if (!RoLuaTables.TryRead(lua, File, null, null, out var chunk, out reason)) return false;
            if (!RoLuaTables.TryTable(chunk, "tbl", File, out var tbl, out reason)) return false;

            var scales = new Dictionary<int, float>(tbl.Indexed.Count);
            foreach (var kv in tbl.Indexed)
                if (kv.Value is RoLuaTable entry && entry.TryGet("MonsterSize", out var size) && size.TryGetNumber(out double s))
                    scales[(int)kv.Key] = (float)s;
            table = new RoMonsterScaleTable(scales);
            return true;
        }
    }
}
