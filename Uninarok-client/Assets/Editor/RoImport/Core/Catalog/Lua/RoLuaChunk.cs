using System.Collections.Generic;

namespace Uninarok.RoImport.Core.Catalog.Lua
{
    /// <summary>What one lub file defines (PLAN-0008 4.1): its global assignments, by name, in the order first set.</summary>
    public sealed class RoLuaChunk
    {
        public RoLuaChunk(IReadOnlyDictionary<string, RoLuaValue> globals) => Globals = globals;

        public IReadOnlyDictionary<string, RoLuaValue> Globals { get; }

        public bool TryGetTable(string name, out RoLuaTable table)
        {
            table = Globals.TryGetValue(name, out var v) ? v as RoLuaTable : null;
            return table != null;
        }
    }
}
