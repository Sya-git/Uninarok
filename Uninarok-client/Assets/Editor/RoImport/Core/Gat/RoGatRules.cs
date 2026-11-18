using Uninarok.Map;

namespace Uninarok.RoImport.Core.Gat
{
    /// <summary>
    /// GAT raw type to cell type and flags (PLAN-0005 4.2, D2): the Hercules map server's <c>map_gat2cell</c>, defined for
    /// the project in <c>docs/data/GatCellType.md</c>. A raw value that is not 0 to 6 (bit 31 set, the absent cell -1,
    /// anything above 6) is <see cref="MapCellType.Other"/> with no flag, as on the server.
    /// </summary>
    public static class RoGatRules
    {
        public const int RulesVersion = 1;

        public static MapCellType TypeOf(uint rawType) => rawType <= 6u ? (MapCellType)rawType : MapCellType.Other;

        public static MapCellFlags FlagsOf(MapCellType type)
        {
            switch (type)
            {
                case MapCellType.Walkable:
                case MapCellType.Type2:
                case MapCellType.Type4:
                case MapCellType.Type6:
                    return MapCellFlags.Walkable | MapCellFlags.Shootable;
                case MapCellType.Water:
                    return MapCellFlags.Walkable | MapCellFlags.Shootable | MapCellFlags.Water;
                case MapCellType.Cliff:
                    return MapCellFlags.Shootable;
                default:
                    return MapCellFlags.None;
            }
        }
    }
}
