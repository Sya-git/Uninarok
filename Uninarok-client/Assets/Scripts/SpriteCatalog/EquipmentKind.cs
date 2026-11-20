namespace Uninarok.Catalog
{
    /// <summary>What an equipment entry draws (SPEC-0008 FR-3, FR-4; PLAN-0008 4.4, D19). <see cref="None"/> is the default: an item that draws no equipment.</summary>
    public enum EquipmentKind : byte
    {
        None = 0,
        Headgear = 1,
        Garment = 2,
        Weapon = 3,
        Shield = 4,
    }
}
