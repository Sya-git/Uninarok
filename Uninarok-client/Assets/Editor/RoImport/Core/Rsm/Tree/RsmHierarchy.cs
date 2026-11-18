using Uninarok.RoImport.Core.Results;

namespace Uninarok.RoImport.Core.Rsm
{
    /// <summary>Builds the node tree of an RSM model (PLAN-0003 4.2, SPEC-0003 FR-1).</summary>
    public static class RsmHierarchy
    {
        public static bool TryBuild(RoRsm rsm, out RsmTree tree, out RoIssue issue)
        {
            tree = null;
            issue = new RoIssue(RoIssueCode.Unexpected, string.Empty, null, "not implemented yet (TASKS-0003 T2.2)");
            return false;
        }
    }
}
