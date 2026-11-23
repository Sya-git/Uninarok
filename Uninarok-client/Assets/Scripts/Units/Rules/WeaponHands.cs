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

        /// <summary>One readable line for the report and the composer. Allocates: not for the frame loop.</summary>
        public string Describe()
        {
            string text = "right " + DescribeHand(Right) + " + left " + DescribeHand(Left) + " -> ";
            switch (Weapon)
            {
                case WeaponSource.Both:
                    text += "type " + WeaponType + " (assassin family)";
                    break;
                case WeaponSource.Right:
                    text += "type " + WeaponType + " (right weapon)";
                    break;
                case WeaponSource.Left:
                    text += "type " + WeaponType + " (left weapon)";
                    break;
                default:
                    text += "no weapon";
                    break;
            }
            if (DrawShield) text += ", shield drawn";
            if (LeftNotDrawn) text += ", left weapon not drawn";
            return text;
        }

        static string DescribeHand(in Hand hand)
        {
            switch (hand.Kind)
            {
                case HandKind.Weapon: return "weapon type " + hand.WeaponType + " (" + hand.Value + ")";
                case HandKind.Shield: return "shield view " + hand.ViewId + " (" + hand.Value + ")";
                case HandKind.Unknown: return "unknown (" + hand.Value + ")";
                default: return "none";
            }
        }
    }

    /// <summary>The both-hands rule, defined once for display and combat (PLAN-0009 4.11; SPEC-0009 FR-17).</summary>
    public static class WeaponHands
    {
        public const int FirstItemId = 500;
        public const int Katar = 16;

        public static bool IsItemId(int value) => value >= FirstItemId;
        /// <summary>
        /// The right hand: a view id is a weapon of its type; an item id is a weapon by the item table, any other kind (a shield
        /// included) or an item the table does not have is Unknown. No catalog or no tables: items Unknown, types 0. No allocation.
        /// </summary>
        public static Hand Right(int value, SpriteCatalog catalog, CompositionTables tables)
        {
            if (value <= 0) return new Hand(value, HandKind.None, 0, 0);
            if (!IsItemId(value)) return new Hand(value, HandKind.Weapon, value, TypeOf(value, tables));
            if (TryItem(value, catalog, tables, out var kind, out int view) && kind == EquipmentKind.Weapon)
                return new Hand(value, HandKind.Weapon, view, TypeOf(view, tables));
            return new Hand(value, HandKind.Unknown, 0, 0);
        }

        /// <summary>
        /// The left hand: a view id is a shield (ambiguous, FR-17; R14); an item id is a weapon or a shield by the item table,
        /// any other kind or an item the table does not have is Unknown. No catalog or no tables: items Unknown. No allocation.
        /// </summary>
        public static Hand Left(int value, SpriteCatalog catalog, CompositionTables tables)
        {
            if (value <= 0) return new Hand(value, HandKind.None, 0, 0);
            if (!IsItemId(value)) return new Hand(value, HandKind.Shield, value, 0);
            if (TryItem(value, catalog, tables, out var kind, out int view))
            {
                if (kind == EquipmentKind.Weapon) return new Hand(value, HandKind.Weapon, view, TypeOf(view, tables));
                if (kind == EquipmentKind.Shield) return new Hand(value, HandKind.Shield, view, 0);
            }
            return new Hand(value, HandKind.Unknown, 0, 0);
        }

        static bool TryItem(int item, SpriteCatalog catalog, CompositionTables tables, out EquipmentKind kind, out int view)
        {
            if (catalog != null && tables != null && catalog.TryGetItemView(item, out kind, out view)) return true;
            kind = EquipmentKind.None;
            view = 0;
            return false;
        }

        static int TypeOf(int viewId, CompositionTables tables) => tables != null ? tables.WeaponTypeOf(viewId) : 0;

        const int ShortSword = 1, Sword = 2, Axe = 6;

        /// <summary>Sprite type of two one-handed weapons together (25..30, either order), else 0.</summary>
        public static int CombinedType(int typeA, int typeB)
        {
            int lo = typeA < typeB ? typeA : typeB;
            int hi = typeA < typeB ? typeB : typeA;
            if (lo == ShortSword && hi == ShortSword) return 25;
            if (lo == Sword && hi == Sword) return 26;
            if (lo == Axe && hi == Axe) return 27;
            if (lo == ShortSword && hi == Sword) return 28;
            if (lo == ShortSword && hi == Axe) return 29;
            if (lo == Sword && hi == Axe) return 30;
            return 0;
        }

        /// <summary>Pure case table of PLAN-0009 4.11. Unknown hands count as empty for the type.</summary>
        public static HandsResult Combine(int job, in Hand right, in Hand left)
        {
            bool rightWeapon = right.Kind == HandKind.Weapon;
            bool leftWeapon = left.Kind == HandKind.Weapon;
            bool leftShield = left.Kind == HandKind.Shield;

            if (rightWeapon)
            {
                if (right.WeaponType == Katar)
                    return new HandsResult(right, left, Katar, WeaponSource.Right, false, false);
                if (leftShield)
                    return new HandsResult(right, left, right.WeaponType, WeaponSource.Right, true, false);
                if (leftWeapon)
                {
                    int combined = JobFamilies.IsAssassinFamily(job) ? CombinedType(right.WeaponType, left.WeaponType) : 0;
                    if (combined != 0)
                        return new HandsResult(right, left, combined, WeaponSource.Both, false, false);
                    return new HandsResult(right, left, right.WeaponType, WeaponSource.Right, false, true);
                }
                return new HandsResult(right, left, right.WeaponType, WeaponSource.Right, false, false);
            }

            if (leftWeapon)
                return new HandsResult(right, left, left.WeaponType, WeaponSource.Left, false, false);
            return new HandsResult(right, left, 0, WeaponSource.None, leftShield, false);
        }
        /// <summary>Both hands classified (Right, Left) and combined (Combine). No allocation, no exception.</summary>
        public static HandsResult Of(int job, int right, int left, SpriteCatalog catalog, CompositionTables tables)
            => Combine(job, Right(right, catalog, tables), Left(left, catalog, tables));
    }
}
