using System;
using System.Collections.Generic;
using Uninarok.Catalog;
using Uninarok.Networking.Client;
using Uninarok.Units;

namespace Uninarok.World.View
{
    /// <summary>
    /// The server's hand values to one weapon type, and a job to the job and sex of its attack row, from the sprite catalog, the
    /// composition tables and <see cref="JobFamilies"/> (SPEC-0011 FR-4, FR-14, FR-15; PLAN-0011 4.7, D20, D21). A value of 500 or
    /// more is an item id: its view id is the item table's (PLAN-0010 D22); below, a view id. Unknown: 0 (bare hand), one trace
    /// line per value.
    /// </summary>
    public sealed class CatalogWeaponTypes : IWeaponTypes, IJobRows
    {
        readonly IUnitAssetSource _source;
        readonly HashSet<int> _reported = new HashSet<int>();

        public CatalogWeaponTypes(IUnitAssetSource source) => _source = source ?? throw new ArgumentNullException(nameof(source));

        /// <summary>One value as a right hand (tests, trace).</summary>
        public int TypeOf(int weapon)
        {
            if (weapon == 0) return 0;
            var tables = _source.Tables;
            if (tables == null) return 0;
            var hand = WeaponHands.Right(weapon, _source.Catalog, tables);
            if (IsUnknown(hand)) return Unknown(hand.Value);
            return hand.WeaponType;
        }

        /// <summary>Both hands as one type: <see cref="HandsOf"/>'s, the rule the unit display uses (FR-15, D21).</summary>
        public int TypeOf(int job, int right, int left)
        {
            if (_source.Tables == null) return 0;
            var hands = HandsOf(job, right, left);
            if (IsUnknown(hands.Right)) Unknown(hands.Right.Value);
            if (IsUnknown(hands.Left)) Unknown(hands.Left.Value);
            return hands.WeaponType;
        }

        /// <summary>Both hands as one weapon type, by the rule display and combat share (SPEC-0009 FR-17).</summary>
        public HandsResult HandsOf(int job, int right, int left) => WeaponHands.Of(job, right, left, _source.Catalog, _source.Tables);

        public int RowJobOf(int job) => JobFamilies.RowJobOf(job);

        public byte RowSexOf(int job, byte sex) => JobFamilies.RowSexOf(job, sex);

        // An item that is no weapon or shield, or a weapon view id without a type, is unknown; a weapon item whose view id is 0 is
        // a bare hand.
        static bool IsUnknown(in Hand hand) =>
            hand.Kind == HandKind.Unknown || (hand.Kind == HandKind.Weapon && hand.WeaponType == 0 && hand.ViewId != 0);

        int Unknown(int value)
        {
            if (_reported.Add(value)) NetLog.Trace("weapon value " + value + " has no weapon type: bare hand");
            return 0;
        }
    }
}
