using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Rsw;

namespace Uninarok.RoImport.Core.Formats
{
    /// <summary>Adapter over GRF's RSW reader (PLAN-0006 4.1). Implemented by TASKS-0006 T1.3.</summary>
    public static class RoRswReader
    {
        public static bool TryRead(RoPath path, byte[] bytes, out RoRsw rsw, out string reason)
        {
            rsw = null;
            reason = "not implemented (TASKS-0006 T1.3)";
            return false;
        }
    }
}
