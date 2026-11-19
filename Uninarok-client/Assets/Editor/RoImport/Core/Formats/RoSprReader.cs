using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Sprites;

namespace Uninarok.RoImport.Core.Formats
{
    /// <summary>Adapter over GRF's SPR reader (PLAN-0007 4.1). Implemented by TASKS-0007 T1.3.</summary>
    public static class RoSprReader
    {
        public static bool TryRead(RoPath path, byte[] bytes, out RoSpr spr, out string reason)
        {
            spr = null;
            reason = "not implemented (TASKS-0007 T1.3)";
            return false;
        }
    }
}
