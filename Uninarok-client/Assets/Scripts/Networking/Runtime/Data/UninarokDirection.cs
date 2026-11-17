namespace Uninarok.Networking.Data
{
    /// <summary>Facing direction (docs/data/UninarokPosition.md). 0-7 are used; 8-15 are legal on the wire and kept as-is.</summary>
    public enum UninarokDirection : byte
    {
        North = 0,
        NorthWest = 1,
        West = 2,
        SouthWest = 3,
        South = 4,
        SouthEast = 5,
        East = 6,
        NorthEast = 7,
    }
}
