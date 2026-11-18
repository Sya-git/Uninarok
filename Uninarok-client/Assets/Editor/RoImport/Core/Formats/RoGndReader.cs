using Uninarok.RoImport.Core.Gnd;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;

namespace Uninarok.RoImport.Core.Formats
{
    /// <summary>
    /// Adapter over GRF's GND reader (PLAN-0004 4.1, D1): the only GND code that touches GRF types. It never throws for
    /// bad input: every failure is <c>false</c> with a reason.
    /// </summary>
    public static class RoGndReader
    {
        public static bool TryRead(RoPath path, byte[] bytes, RoReport report, out RoGnd gnd, out string reason)
        {
            gnd = null;
            reason = "not implemented yet (TASKS-0004 T1.3)";
            return false;
        }
    }
}
