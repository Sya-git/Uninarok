using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Rsm;

namespace Uninarok.RoImport.Core.Formats
{
    /// <summary>
    /// Adapter over GRF's RSM reader (PLAN-0003 4.1, D2): the only RSM code that touches GRF types. It never throws for
    /// bad input: every failure is <c>false</c> with a reason.
    /// </summary>
    public static class RoRsmReader
    {
        public static bool TryRead(RoPath path, byte[] bytes, out RoRsm rsm, out string reason)
        {
            rsm = null;
            reason = "not implemented yet (TASKS-0003 T1.3)";
            return false;
        }
    }
}
