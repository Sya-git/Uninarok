using System;
using Uninarok.Map;

namespace Uninarok.RoImport.Core.Gat
{
    /// <summary>GAT raw type to cell type and flags (PLAN-0005 4.2, D2). Implemented by TASKS-0005 T3.2.</summary>
    public static class RoGatRules
    {
        public const int RulesVersion = 1;

        public static MapCellType TypeOf(uint rawType) => throw new NotImplementedException();

        public static MapCellFlags FlagsOf(MapCellType type) => throw new NotImplementedException();
    }
}
