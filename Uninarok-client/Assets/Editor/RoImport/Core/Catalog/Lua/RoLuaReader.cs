using System.Collections.Generic;

namespace Uninarok.RoImport.Core.Catalog.Lua
{
    /// <summary>
    /// Reads the Lua data subset of the client's lub tables (PLAN-0008 4.1, D7). Implemented by TASKS-0008 T2.2.
    /// </summary>
    public static class RoLuaReader
    {
        public static bool TryRead(string lua, RoLuaOptions options, IReadOnlyDictionary<string, RoLuaValue> imports,
                                   out RoLuaChunk chunk, out string reason)
        {
            chunk = null;
            reason = "not implemented (TASKS-0008 T2.2)";
            return false;
        }
    }
}
