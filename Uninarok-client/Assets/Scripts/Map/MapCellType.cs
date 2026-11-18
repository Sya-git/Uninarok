using System;

namespace Uninarok.Map
{
    /// <summary>
    /// The terrain type of one GAT cell (SPEC-0005 FR-1, PLAN-0005 4.2, D2). The values and names follow the Hercules map
    /// server's <c>map_gat2cell</c>; the definition is <c>docs/data/GatCellType.md</c>.
    /// </summary>
    public enum MapCellType : byte
    {
        /// <summary>Walkable ground (raw 0).</summary>
        Walkable = 0,
        /// <summary>Wall: neither walking nor attacks pass (raw 1).</summary>
        Wall = 1,
        /// <summary>No name on the server; behaves as walkable ground (raw 2).</summary>
        Type2 = 2,
        /// <summary>Walkable water (raw 3).</summary>
        Water = 3,
        /// <summary>No name on the server; behaves as walkable ground (raw 4).</summary>
        Type4 = 4,
        /// <summary>Cliff or gap: not walkable, attacks pass over (raw 5).</summary>
        Cliff = 5,
        /// <summary>No name on the server; behaves as walkable ground (raw 6).</summary>
        Type6 = 6,
        /// <summary>Any other raw value; blocked and not shootable, as on the server.</summary>
        Other = 255
    }

    /// <summary>The server's cell flags, stored per cell (PLAN-0005 4.2, <c>docs/data/GatCellType.md</c>).</summary>
    [Flags]
    public enum MapCellFlags : byte
    {
        None = 0,
        /// <summary>A character can stand on and move through the cell.</summary>
        Walkable = 1,
        /// <summary>Ranged attacks and spells pass over the cell.</summary>
        Shootable = 2,
        /// <summary>The cell is water.</summary>
        Water = 4
    }
}
