using System;

namespace Uninarok.RoImport.Core.Catalog.Lua
{
    /// <summary>Options of <see cref="RoLuaReader"/> (PLAN-0008 4.1).</summary>
    public sealed class RoLuaOptions
    {
        /// <summary>
        /// Called with the name of every <c>name = value</c> field inside a table; when it returns true the value is
        /// skipped while parsing and never built (the item table's display names and descriptions). Null keeps every field.
        /// </summary>
        public Func<string, bool> SkipField;
    }
}
