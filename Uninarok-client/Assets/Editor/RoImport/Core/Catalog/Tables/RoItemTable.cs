using System;
using System.Collections.Generic;
using Uninarok.RoImport.Core.Catalog.Lua;

namespace Uninarok.RoImport.Core.Catalog.Tables
{
    /// <summary>Which language's item table feeds ground items and equipment lookups (SPEC-0008 FR-12, OQ-4; PLAN-0008 4.8).</summary>
    public enum RoItemTableLanguage
    {
        Portuguese,
        English,
        Spanish,
    }

    /// <summary>The three fields of one item the catalog uses (PLAN-0008 4.2).</summary>
    public readonly struct RoItemInfo
    {
        public RoItemInfo(string identifiedResource, string unidentifiedResource, int classNum)
        {
            IdentifiedResource = identifiedResource;
            UnidentifiedResource = unidentifiedResource;
            ClassNum = classNum;
        }

        /// <summary><c>identifiedResourceName</c>: the sprite name of the identified item (<c>빨간포션</c>), or null.</summary>
        public string IdentifiedResource { get; }
        /// <summary><c>unidentifiedResourceName</c>, or null.</summary>
        public string UnidentifiedResource { get; }
        /// <summary><c>ClassNum</c>: the view id of a weapon, headgear, garment or shield, 0 when none (sprite-catalog.md §2.1).</summary>
        public int ClassNum { get; }
    }

    /// <summary>
    /// The client's item table (<c>System/iteminfo_new.lub</c> and its <c>english/</c>, <c>spanish/</c> copies; global
    /// <c>tbl</c>, T0.3), reduced while parsing to <c>identifiedResourceName</c>, <c>unidentifiedResourceName</c> and
    /// <c>ClassNum</c> (<see cref="RoLuaOptions.SkipField"/>; FR-4, FR-5, FR-12). Resource names are shared between items.
    /// </summary>
    public sealed class RoItemTable
    {
        const string Identified = "identifiedResourceName";
        const string Unidentified = "unidentifiedResourceName";
        const string ClassNumField = "ClassNum";

        readonly int[] _ids;
        readonly RoItemInfo[] _items;

        RoItemTable(int[] ids, RoItemInfo[] items)
        {
            _ids = ids;
            _items = items;
        }

        /// <summary>The item ids, ascending.</summary>
        public IReadOnlyList<int> ItemIds => _ids;

        public int Count => _ids.Length;

        /// <summary>The file of a language's table, relative to the <c>System</c> folder.</summary>
        public static string FileName(RoItemTableLanguage language)
        {
            switch (language)
            {
                case RoItemTableLanguage.Portuguese: return "iteminfo_new.lub";
                case RoItemTableLanguage.English: return "english/iteminfo_new.lub";
                case RoItemTableLanguage.Spanish: return "spanish/iteminfo_new.lub";
                default: throw new ArgumentOutOfRangeException(nameof(language), language, null);
            }
        }

        public bool TryGet(int itemId, out RoItemInfo info)
        {
            int i = Array.BinarySearch(_ids, itemId);
            info = i >= 0 ? _items[i] : default;
            return i >= 0;
        }

        public static bool TryRead(string lua, string file, out RoItemTable table, out string reason)
        {
            table = null;
            var options = new RoLuaOptions { SkipField = name => name != Identified && name != Unidentified && name != ClassNumField };
            if (!RoLuaTables.TryRead(lua, file, options, null, out var chunk, out reason)) return false;
            if (!RoLuaTables.TryTable(chunk, "tbl", file, out var tbl, out reason)) return false;

            var names = new Dictionary<string, string>(StringComparer.Ordinal);
            var ids = new List<int>(tbl.Indexed.Count);
            foreach (var key in tbl.Indexed.Keys)
                if (key >= 0 && key <= int.MaxValue) ids.Add((int)key);
            ids.Sort();

            var items = new RoItemInfo[ids.Count];
            for (int i = 0; i < ids.Count; i++)
            {
                if (!(tbl.Indexed[ids[i]] is RoLuaTable item))
                {
                    reason = $"{file}: item {ids[i]} is not a table";
                    return false;
                }
                int classNum = item.TryGet(ClassNumField, out var c) && c.TryGetInteger(out long n) ? (int)n : 0;
                items[i] = new RoItemInfo(Shared(item, Identified, names), Shared(item, Unidentified, names), classNum);
            }
            table = new RoItemTable(ids.ToArray(), items);
            return true;
        }

        static string Shared(RoLuaTable item, string field, Dictionary<string, string> names)
        {
            if (!item.TryGet(field, out var v) || !v.TryGetString(out string s) || s.Length == 0) return null;
            if (names.TryGetValue(s, out string shared)) return shared;
            names[s] = s;
            return s;
        }
    }
}
