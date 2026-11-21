using System;
using System.Collections.Generic;
using Uninarok.Catalog;
using Uninarok.RoImport.Core.Catalog.Tables;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Source;

namespace Uninarok.RoImport.Core.Catalog.Planning
{
    /// <summary>One category's planner (PLAN-0008 4.3, D1): pure, joins the tables with the files, writes nothing.</summary>
    public interface IRoCatalogPlanner
    {
        SpriteCatalogCategory Category { get; }

        /// <summary>The table groups the planner reads; a run loads the union (FR-9).</summary>
        RoCatalogNeeds Needs { get; }

        void Plan(RoCatalogContext ctx, RoCatalogPlan.Builder plan);
    }

    /// <summary>
    /// What the planners share in one run (PLAN-0008 4.3): the data source, the tables, and the file lists of the folders
    /// they walk, each enumerated once (the ground item and skill icon planners share <c>sprite/아이템/</c>).
    /// </summary>
    public sealed class RoCatalogContext
    {
        readonly Dictionary<string, FileSet> _folders = new Dictionary<string, FileSet>(StringComparer.Ordinal);

        public RoCatalogContext(IRoSource data, RoCatalogTables tables)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            Tables = tables ?? throw new ArgumentNullException(nameof(tables));
        }

        public IRoSource Data { get; }
        public RoCatalogTables Tables { get; }

        /// <summary>Every file under <paramref name="folderPrefix"/> (canonical, ends with <c>/</c>), enumerated on first use.</summary>
        public FileSet Files(string folderPrefix)
        {
            string key = RoFileNames.Prefix(folderPrefix);
            if (!_folders.TryGetValue(key, out var set))
            {
                set = new FileSet(key, Data.Enumerate(key));
                _folders.Add(key, set);
            }
            return set;
        }

        /// <summary>The files of one folder prefix, in ordinal order, with O(1) membership.</summary>
        public sealed class FileSet
        {
            readonly HashSet<RoPath> _set;

            internal FileSet(string prefix, IEnumerable<RoPath> files)
            {
                Prefix = prefix;
                var list = new List<RoPath>(files);
                list.Sort();
                All = list;
                _set = new HashSet<RoPath>(list);
            }

            public string Prefix { get; }
            public IReadOnlyList<RoPath> All { get; }
            public int Count => All.Count;
            public bool Contains(RoPath path) => _set.Contains(path);

            /// <summary>The files whose canonical path starts with <paramref name="prefix"/>, by binary search over the sorted list.</summary>
            public IEnumerable<RoPath> Under(string prefix)
            {
                int lo = 0, hi = All.Count;
                while (lo < hi)
                {
                    int mid = (lo + hi) >> 1;
                    if (string.CompareOrdinal(All[mid].Canonical, prefix) < 0) lo = mid + 1;
                    else hi = mid;
                }
                for (int i = lo; i < All.Count && All[i].Canonical.StartsWith(prefix, StringComparison.Ordinal); i++)
                    yield return All[i];
            }
        }
    }
}
