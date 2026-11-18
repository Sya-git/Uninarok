using System;
using Uninarok.RoImport.Core.Gat;
using Uninarok.RoImport.Core.Paths;

namespace Uninarok.RoImport.Core.Formats
{
    /// <summary>Adapter over GRF's GAT reader (PLAN-0005 4.1). Implemented by TASKS-0005 T2.3.</summary>
    public static class RoGatReader
    {
        public static bool TryRead(RoPath path, byte[] bytes, out RoGat gat, out string reason) => throw new NotImplementedException();
    }
}
