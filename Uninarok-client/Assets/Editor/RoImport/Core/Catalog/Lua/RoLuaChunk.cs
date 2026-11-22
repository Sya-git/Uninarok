using System.Collections.Generic;

namespace Uninarok.RoImport.Core.Catalog.Lua
{
    /// <summary>
    /// A top-level call statement (<c>name(args)</c>, <c>a.b(args)</c>) with its literal arguments and resolved references
    /// (PLAN-0009 D4, SPEC-0009 FR-4). An argument that is <c>nil</c> is null.
    /// </summary>
    public sealed class RoLuaCall
    {
        public RoLuaCall(string name, IReadOnlyList<RoLuaValue> arguments)
        {
            Name = name;
            Arguments = arguments;
        }

        /// <summary>The called name as written: <c>SetHeadSprEditInfo</c>, <c>OffsetItemPos.AddOffsetForDoram</c>.</summary>
        public string Name { get; }
        public IReadOnlyList<RoLuaValue> Arguments { get; }

        public bool TryGetInteger(int index, out int value)
        {
            value = 0;
            if (index >= Arguments.Count || Arguments[index] == null || !Arguments[index].TryGetInteger(out long n) || n < int.MinValue || n > int.MaxValue) return false;
            value = (int)n;
            return true;
        }
    }

    /// <summary>What one lub file defines (PLAN-0008 4.1): its global assignments, by name, in the order first set, and its top-level calls in file order (PLAN-0009 D4).</summary>
    public sealed class RoLuaChunk
    {
        public RoLuaChunk(IReadOnlyDictionary<string, RoLuaValue> globals, IReadOnlyList<RoLuaCall> calls = null)
        {
            Globals = globals;
            Calls = calls ?? System.Array.Empty<RoLuaCall>();
        }

        public IReadOnlyDictionary<string, RoLuaValue> Globals { get; }

        /// <summary>The call statements whose arguments are data; a call with other arguments is skipped, as before.</summary>
        public IReadOnlyList<RoLuaCall> Calls { get; }

        public bool TryGetTable(string name, out RoLuaTable table)
        {
            table = Globals.TryGetValue(name, out var v) ? v as RoLuaTable : null;
            return table != null;
        }
    }
}
