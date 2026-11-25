using System;
using System.Collections.Generic;

namespace Uninarok.Effects.Editor
{
    /// <summary>One row of the browse list: a folder of the tree or an effect key.</summary>
    public readonly struct BrowseRow
    {
        /// <summary>Index into the key array; -1 for a folder row.</summary>
        public readonly int KeyIndex;
        public readonly string Label;
        /// <summary>For a folder row its full path ("a/b"); for a key row the folder it sits in ("" at the top, and in the flat list).</summary>
        public readonly string Folder;
        public readonly int Depth;

        public BrowseRow(int keyIndex, string label, string folder, int depth)
        {
            KeyIndex = keyIndex; Label = label; Folder = folder; Depth = depth;
        }

        public bool IsFolder => KeyIndex < 0;
    }

    /// <summary>
    /// Pure list logic of the effect preview window (SPEC-0012 FR-11, plan 4.11): the text filter on the key and the flat or
    /// folder-tree rows. No Unity state.
    /// </summary>
    public static class EffectBrowse
    {
        /// <summary>
        /// Indices of the keys that contain every whitespace-separated token of <paramref name="filter"/> (ordinal, ignoring
        /// case; a backslash counts as a slash), in key order. An empty filter keeps every key.
        /// </summary>
        public static List<int> Filter(IReadOnlyList<string> keys, string filter)
        {
            var result = new List<int>(keys.Count);
            string[] tokens = string.IsNullOrWhiteSpace(filter)
                ? Array.Empty<string>()
                : filter.Replace('\\', '/').Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < keys.Count; i++)
            {
                string key = keys[i];
                if (key == null) continue;
                bool all = true;
                for (int t = 0; t < tokens.Length && all; t++)
                    all = key.IndexOf(tokens[t], StringComparison.OrdinalIgnoreCase) >= 0;
                if (all) result.Add(i);
            }
            return result;
        }

        /// <summary>Flat list: one row per matching key, labelled with the whole key.</summary>
        public static List<BrowseRow> FlatRows(IReadOnlyList<string> keys, IReadOnlyList<int> matches)
        {
            var rows = new List<BrowseRow>(matches.Count);
            for (int m = 0; m < matches.Count; m++)
            {
                int i = matches[m];
                rows.Add(new BrowseRow(i, keys[i], "", 0));
            }
            return rows;
        }

        /// <summary>
        /// Folder tree over the matching keys (sorted ordinal inside): a folder row before the rows below it, then its keys
        /// labelled with the file name. A folder in <paramref name="collapsed"/> (full path) shows its own row and hides what
        /// is below; pass null to expand everything (a filter is active).
        /// </summary>
        public static List<BrowseRow> TreeRows(IReadOnlyList<string> keys, IReadOnlyList<int> matches, ISet<string> collapsed)
        {
            var order = new List<int>(matches);
            order.Sort((a, b) => string.CompareOrdinal(keys[a], keys[b]));

            var rows = new List<BrowseRow>(order.Count + order.Count / 4);
            var shown = new List<string>();   // folder names open on the way down, one per depth
            for (int n = 0; n < order.Count; n++)
            {
                int i = order[n];
                string[] parts = keys[i].Split('/');
                int folders = parts.Length - 1;

                int common = 0;
                while (common < shown.Count && common < folders && shown[common] == parts[common]) common++;
                shown.RemoveRange(common, shown.Count - common);

                bool hidden = false;
                string path = "";
                for (int d = 0; d < folders && !hidden; d++)
                {
                    path = d == 0 ? parts[0] : path + "/" + parts[d];
                    if (d >= common)
                    {
                        rows.Add(new BrowseRow(-1, parts[d], path, d));
                        shown.Add(parts[d]);
                    }
                    if (collapsed != null && collapsed.Contains(path)) hidden = true;
                }
                if (hidden) continue;
                string folder = folders == 0 ? "" : string.Join("/", parts, 0, folders);
                rows.Add(new BrowseRow(i, parts[folders], folder, folders));
            }
            return rows;
        }
    }
}
