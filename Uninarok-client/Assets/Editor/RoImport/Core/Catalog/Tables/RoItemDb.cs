using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Uninarok.Catalog;

namespace Uninarok.RoImport.Core.Catalog.Tables
{
    /// <summary>
    /// The equipment kind of each item, from the server's item database (PLAN-0008 D21): Hercules <c>db/re/item_db.conf</c>,
    /// a libconfig file read here for three fields only, <c>Id</c>, <c>Type</c> and <c>Loc</c>. Comments and item scripts are
    /// skipped; every entry starts with its <c>Id</c>. <see cref="KindOf(string, IReadOnlyList{string})"/> is the rule.
    /// </summary>
    public sealed class RoItemDb
    {
        static readonly Regex IdLine = new Regex(@"^\s*Id:\s*(\d+)\s*$", RegexOptions.Compiled);
        static readonly Regex TypeLine = new Regex(@"^\s*Type:\s*""(\w+)""", RegexOptions.Compiled);
        static readonly Regex LocLine = new Regex(@"^\s*Loc:\s*(.+)$", RegexOptions.Compiled);
        static readonly Regex Quoted = new Regex(@"""(\w+)""", RegexOptions.Compiled);

        readonly Dictionary<int, EquipmentKind> _kinds;

        RoItemDb(Dictionary<int, EquipmentKind> kinds, int count)
        {
            _kinds = kinds;
            Count = count;
        }

        /// <summary>Items read (of any type).</summary>
        public int Count { get; }

        /// <summary>The kind of an item; <see cref="EquipmentKind.None"/> for an item the database does not have.</summary>
        public EquipmentKind KindOf(int itemId) => _kinds.TryGetValue(itemId, out var kind) ? kind : EquipmentKind.None;

        /// <summary>
        /// D21: <c>IT_WEAPON</c> → weapon; <c>IT_ARMOR</c> with <c>EQP_SHIELD</c> → shield, else with a head location →
        /// headgear, else with a garment location → garment; anything else (shadow gear, cards, usables) → none.
        /// </summary>
        public static EquipmentKind KindOf(string type, IReadOnlyList<string> locations)
        {
            if (type == "IT_WEAPON") return EquipmentKind.Weapon;
            if (type != "IT_ARMOR" || locations == null) return EquipmentKind.None;
            bool head = false, garment = false;
            foreach (string loc in locations)
            {
                if (loc == "EQP_SHIELD") return EquipmentKind.Shield;
                head |= loc == "EQP_HEAD_TOP" || loc == "EQP_HEAD_MID" || loc == "EQP_HEAD_LOW" || loc == "EQP_HELM"
                        || loc == "EQP_COSTUME_HEAD_TOP" || loc == "EQP_COSTUME_HEAD_MID" || loc == "EQP_COSTUME_HEAD_LOW";
                garment |= loc == "EQP_GARMENT" || loc == "EQP_COSTUME_GARMENT";
            }
            return head ? EquipmentKind.Headgear : garment ? EquipmentKind.Garment : EquipmentKind.None;
        }

        /// <summary>Reads the text of an item_db.conf. False with a reason when no item is found; never throws for bad data.</summary>
        public static bool TryRead(string text, out RoItemDb db, out string reason)
        {
            db = null;
            if (string.IsNullOrEmpty(text))
            {
                reason = "the item database is empty";
                return false;
            }
            var kinds = new Dictionary<int, EquipmentKind>();
            int count = 0, id = -1;
            string type = null;
            var locs = new List<string>();

            void Flush()
            {
                if (id < 0) return;
                count++;
                var kind = KindOf(type, locs);
                if (kind != EquipmentKind.None) kinds[id] = kind;
            }

            foreach (string line in Lines(text))
            {
                var m = IdLine.Match(line);
                if (m.Success)
                {
                    Flush();
                    id = int.TryParse(m.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int v) ? v : -1;
                    type = null;
                    locs.Clear();
                    continue;
                }
                if (id < 0) continue;
                if (type == null && (m = TypeLine.Match(line)).Success) type = m.Groups[1].Value;
                else if ((m = LocLine.Match(line)).Success)
                    foreach (Match q in Quoted.Matches(m.Groups[1].Value)) locs.Add(q.Groups[1].Value);
            }
            Flush();

            if (count == 0)
            {
                reason = "no item entry (Id: …) was found";
                return false;
            }
            db = new RoItemDb(kinds, count);
            reason = null;
            return true;
        }

        // The lines of the file outside comments and item scripts (<" … ">), which may hold anything.
        static IEnumerable<string> Lines(string text)
        {
            var sb = new System.Text.StringBuilder(text.Length);
            int i = 0;
            while (i < text.Length)
            {
                char c = text[i];
                if (c == '/' && i + 1 < text.Length && text[i + 1] == '*')
                {
                    int end = text.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    i = end < 0 ? text.Length : end + 2;
                    continue;
                }
                if (c == '/' && i + 1 < text.Length && text[i + 1] == '/')
                {
                    int end = text.IndexOf('\n', i);
                    i = end < 0 ? text.Length : end;
                    continue;
                }
                if (c == '<' && i + 1 < text.Length && text[i + 1] == '"')
                {
                    int end = text.IndexOf("\">", i + 2, StringComparison.Ordinal);
                    i = end < 0 ? text.Length : end + 2;
                    continue;
                }
                if (c == '"')
                {
                    int end = text.IndexOf('"', i + 1);
                    if (end < 0) end = text.Length - 1;
                    sb.Append(text, i, end - i + 1);
                    i = end + 1;
                    continue;
                }
                sb.Append(c);
                i++;
            }
            return sb.ToString().Split('\n');
        }
    }
}
