using System.Collections.Generic;
using Uninarok.RoImport.Core.Catalog.Lua;

namespace Uninarok.RoImport.Core.Catalog.Tables
{
    /// <summary>Small helpers shared by the table readers (PLAN-0008 4.2): read a chunk, find a global table, convert maps.</summary>
    static class RoLuaTables
    {
        public static bool TryRead(string lua, string file, RoLuaOptions options, IReadOnlyDictionary<string, RoLuaValue> imports,
                                   out RoLuaChunk chunk, out string reason)
        {
            if (RoLuaReader.TryRead(lua, options, imports, out chunk, out reason)) return true;
            reason = $"{file}: {reason}";
            return false;
        }

        public static bool TryTable(RoLuaChunk chunk, string name, string file, out RoLuaTable table, out string reason)
        {
            if (chunk.TryGetTable(name, out table))
            {
                reason = null;
                return true;
            }
            reason = $"{file}: no table '{name}'";
            return false;
        }

        /// <summary>The named fields whose value is an integer (<c>JT_PORING = 1002</c>); other fields are left out.</summary>
        public static Dictionary<string, int> Constants(RoLuaTable table)
        {
            var map = new Dictionary<string, int>(table.Fields.Count, System.StringComparer.Ordinal);
            foreach (var kv in table.Fields)
                if (kv.Value.TryGetInteger(out long id) && id >= int.MinValue && id <= int.MaxValue) map[kv.Key] = (int)id;
            return map;
        }

        /// <summary>The integer-keyed fields whose value is a string (<c>[1002] = "PORING"</c>).</summary>
        public static Dictionary<int, string> Names(RoLuaTable table)
        {
            var map = new Dictionary<int, string>(table.Indexed.Count);
            foreach (var kv in table.Indexed)
                if (kv.Key >= int.MinValue && kv.Key <= int.MaxValue && kv.Value.TryGetString(out string s)) map[(int)kv.Key] = s;
            return map;
        }

        /// <summary>The integer-keyed fields whose value is an integer (<c>[Main_Gauche] = SHORTSWORD</c>).</summary>
        public static Dictionary<int, int> IntegerMap(RoLuaTable table)
        {
            var map = new Dictionary<int, int>(table.Indexed.Count);
            foreach (var kv in table.Indexed)
                if (kv.Key >= int.MinValue && kv.Key <= int.MaxValue && kv.Value.TryGetInteger(out long v) && v >= int.MinValue && v <= int.MaxValue)
                    map[(int)kv.Key] = (int)v;
            return map;
        }

        public static Dictionary<string, RoLuaValue> Imports(params RoLuaChunk[] chunks)
        {
            var imports = new Dictionary<string, RoLuaValue>(System.StringComparer.Ordinal);
            foreach (var chunk in chunks)
                foreach (var kv in chunk.Globals)
                    imports[kv.Key] = kv.Value;
            return imports;
        }
    }
}
