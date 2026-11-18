using System.Collections.Generic;
using System.Linq;
using Uninarok.RoImport.Core.Results;

namespace Uninarok.RoImport.Core.Rsm
{
    /// <summary>
    /// Builds the node tree of an RSM model (PLAN-0003 4.2, SPEC-0003 FR-1). Parents are resolved by name, independent of
    /// file order; the order is a stable topological sort (among nodes ready at the same time, file order).
    /// </summary>
    public static class RsmHierarchy
    {
        public static bool TryBuild(RoRsm rsm, out RsmTree tree, out RoIssue issue)
        {
            tree = null;
            issue = null;
            var nodes = rsm.Nodes;
            int count = nodes.Count;
            var warnings = new List<RoIssue>();

            // The first node of a name is the parent target (4.2).
            var firstByName = new Dictionary<string, int>();
            for (int i = 0; i < count; i++)
            {
                string name = nodes[i].Name ?? string.Empty;
                if (!firstByName.ContainsKey(name)) firstByName.Add(name, i);
            }

            var parent = new int[count];
            for (int i = 0; i < count; i++)
            {
                string parentName = nodes[i].ParentName;
                parent[i] = -1;
                if (string.IsNullOrEmpty(parentName)) continue;
                bool found = firstByName.TryGetValue(parentName, out int p);
                if (found && p != i)
                {
                    parent[i] = p;
                    continue;
                }
                string why = found
                    ? "names itself as its parent; it is made a root"
                    : $"names the parent '{parentName}', which no node has; it is made a root";
                warnings.Add(new RoIssue(RoIssueCode.RsmParentMissing, nodes[i].Name, null, why));
            }

            var children = new List<int>[count];
            for (int i = 0; i < count; i++) children[i] = new List<int>();
            for (int i = 0; i < count; i++)
                if (parent[i] >= 0) children[parent[i]].Add(i);

            // Stable topological sort: always take the ready node with the smallest file index.
            var order = new List<int>(count);
            var ready = new SortedSet<int>();
            for (int i = 0; i < count; i++)
                if (parent[i] < 0) ready.Add(i);
            while (ready.Count > 0)
            {
                int next = ready.Min;
                ready.Remove(next);
                order.Add(next);
                foreach (int c in children[next]) ready.Add(c);
            }

            if (order.Count < count)
            {
                string stuck = string.Join(", ", Enumerable.Range(0, count).Except(order).Select(i => nodes[i].Name));
                issue = new RoIssue(RoIssueCode.RsmCycle, stuck, null,
                    $"the parents of the nodes {stuck} form a cycle, so the model has no valid hierarchy");
                return false;
            }

            tree = new RsmTree
            {
                Order = order,
                Roots = Enumerable.Range(0, count).Where(i => parent[i] < 0).ToList(),
                Parent = parent,
                Children = children.Select(c => (IReadOnlyList<int>)c).ToArray(),
                UniqueNames = UniqueNames(nodes, warnings),
                Warnings = warnings
            };
            return true;
        }

        /// <summary>The first node of a name keeps it; later ones get <c>_2</c>, <c>_3</c>, skipping names already used.</summary>
        static string[] UniqueNames(IReadOnlyList<RoRsmNode> nodes, List<RoIssue> warnings)
        {
            var result = new string[nodes.Count];
            var used = new HashSet<string>(nodes.Select(n => n.Name ?? string.Empty));
            var seen = new HashSet<string>();
            var duplicated = new List<string>();
            for (int i = 0; i < nodes.Count; i++)
            {
                string name = nodes[i].Name ?? string.Empty;
                if (seen.Add(name))
                {
                    result[i] = name;
                    continue;
                }
                if (!duplicated.Contains(name)) duplicated.Add(name);
                int suffix = 2;
                while (used.Contains($"{name}_{suffix}")) suffix++;
                result[i] = $"{name}_{suffix}";
                used.Add(result[i]);
            }

            foreach (string name in duplicated)
            {
                int copies = nodes.Count(n => (n.Name ?? string.Empty) == name);
                warnings.Add(new RoIssue(RoIssueCode.RsmDuplicateName, name, null,
                    $"{copies} nodes share this name; later ones are renamed with _2, _3"));
            }
            return result;
        }
    }
}
