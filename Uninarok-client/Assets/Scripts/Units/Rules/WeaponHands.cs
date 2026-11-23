using System;
using Uninarok.Catalog;

namespace Uninarok.Units
{
    public enum HandKind : byte { None, Weapon, Shield, Unknown }

    /// <summary>One hand of a look (PLAN-0009 4.11; SPEC-0009 FR-17). Carries no catalog lookup.</summary>
    public readonly struct Hand
    {
        public readonly int Value;
        public readonly HandKind Kind;
        public readonly int ViewId;
        public readonly int WeaponType;

        public Hand(int value, HandKind kind, int viewId, int weaponType)
        {
            Value = value;
            Kind = kind;
            ViewId = viewId;
            WeaponType = weaponType;
        }
    }

    public enum WeaponSource : byte { None, Right, Left, Both }

    public readonly struct HandsResult
    {
        public readonly Hand Right, Left;
        public readonly int WeaponType;
        public readonly WeaponSource Weapon;
        public readonly bool DrawShield;
        public readonly bool LeftNotDrawn;

        public HandsResult(Hand right, Hand left, int weaponType, WeaponSource weapon, bool drawShield, bool leftNotDrawn)
        {
            Right = right;
            Left = left;
            WeaponType = weaponType;
            Weapon = weapon;
            DrawShield = drawShield;
            LeftNotDrawn = leftNotDrawn;
        }

        public string Describe() => throw new NotImplementedException("T10.2");
    }

    /// <summary>The both-hands rule, defined once for display and combat (PLAN-0009 4.11; SPEC-0009 FR-17).</summary>
    public static class WeaponHands
    {
        public const int FirstItemId = 500;
        public const int Katar = 16;

        public static bool IsItemId(int value) => throw new NotImplementedException("T10.2");
        public static Hand Right(int value, SpriteCatalog catalog, CompositionTables tables) => throw new NotImplementedException("T10.4");
        public static Hand Left(int value, SpriteCatalog catalog, CompositionTables tables) => throw new NotImplementedException("T10.4");
        public static int CombinedType(int typeA, int typeB) => throw new NotImplementedException("T10.2");
        public static HandsResult Combine(int job, in Hand right, in Hand left) => throw new NotImplementedException("T10.2");
        public static HandsResult Of(int job, int right, int left, SpriteCatalog catalog, CompositionTables tables) => throw new NotImplementedException("T10.4");
    }
}
