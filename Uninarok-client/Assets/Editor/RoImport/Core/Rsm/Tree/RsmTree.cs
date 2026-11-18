using System;
using System.Collections.Generic;
using Uninarok.RoImport.Core.Results;

namespace Uninarok.RoImport.Core.Rsm
{
    /// <summary>
    /// The node tree of an RSM model, resolved by name independent of file order (PLAN-0003 4.2, SPEC-0003 FR-1).
    /// Node indices are positions in <see cref="RoRsm.Nodes"/>.
    /// </summary>
    public sealed class RsmTree
    {
        /// <summary>Node indices, every parent before its children; among nodes ready at the same time, file order.</summary>
        public IReadOnlyList<int> Order = Array.Empty<int>();
        /// <summary>Root indices in file order.</summary>
        public IReadOnlyList<int> Roots = Array.Empty<int>();
        /// <summary>Parent index per node; -1 for roots.</summary>
        public int[] Parent = Array.Empty<int>();
        /// <summary>Children per node, in file order.</summary>
        public IReadOnlyList<int>[] Children = Array.Empty<IReadOnlyList<int>>();
        /// <summary>The original names made unique: the first keeps its name, later ones get <c>_2</c>, <c>_3</c>.</summary>
        public string[] UniqueNames = Array.Empty<string>();
        /// <summary>
        /// Expected conditions met while building (missing parent, duplicate name), one per condition. Their
        /// <see cref="RoIssue.Item"/> is the node name; the converter reports them with the model as referrer.
        /// </summary>
        public IReadOnlyList<RoIssue> Warnings = Array.Empty<RoIssue>();
    }
}
