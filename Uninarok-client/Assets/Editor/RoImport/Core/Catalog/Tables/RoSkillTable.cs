using System;
using System.Collections.Generic;

namespace Uninarok.RoImport.Core.Catalog.Tables
{
    /// <summary>
    /// The skill ids of <c>skillinfoz/skillid.lub</c> (<c>SKID</c>; PLAN-0008 4.2, 4.3.8; FR-7). A skill icon is the file
    /// <c>아이템/&lt;constant in lower case&gt;.spr</c> (<c>SM_BASH</c> → <c>sm_bash</c>). Several constants can share an
    /// id (<c>TEAK</c> and <c>SCRIPT_000</c> are both 11000), so an id lists every constant, sorted.
    /// </summary>
    public sealed class RoSkillTable
    {
        public const string File = "skillid.lub";

        readonly Dictionary<int, string[]> _constantsById;

        RoSkillTable(Dictionary<string, int> ids)
        {
            Ids = ids;
            var byId = new Dictionary<int, List<string>>();
            foreach (var kv in ids)
            {
                if (!byId.TryGetValue(kv.Value, out var list)) byId[kv.Value] = list = new List<string>(1);
                list.Add(kv.Key);
            }
            _constantsById = new Dictionary<int, string[]>(byId.Count);
            foreach (var kv in byId)
            {
                kv.Value.Sort(StringComparer.Ordinal);
                _constantsById[kv.Key] = kv.Value.ToArray();
            }
        }

        /// <summary><c>SM_BASH</c> → 5.</summary>
        public IReadOnlyDictionary<string, int> Ids { get; }

        /// <summary>The distinct skill ids.</summary>
        public IEnumerable<int> SkillIds => _constantsById.Keys;

        /// <summary>The constants of a skill id, sorted ordinally; empty when the id is unknown.</summary>
        public IReadOnlyList<string> ConstantsOf(int skillId) =>
            _constantsById.TryGetValue(skillId, out var names) ? names : Array.Empty<string>();

        /// <summary>The icon's file stem of a constant: the constant in lower case (<c>SM_BASH</c> → <c>sm_bash</c>).</summary>
        public static string IconName(string constant) => constant.ToLowerInvariant();

        public static bool TryRead(string lua, out RoSkillTable table, out string reason)
        {
            table = null;
            if (!RoLuaTables.TryRead(lua, File, null, null, out var chunk, out reason)) return false;
            if (!RoLuaTables.TryTable(chunk, "SKID", File, out var skid, out reason)) return false;
            table = new RoSkillTable(RoLuaTables.Constants(skid));
            return true;
        }
    }
}
