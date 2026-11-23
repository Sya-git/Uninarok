using System;
using System.Collections.Generic;
using Uninarok.Catalog;
using Uninarok.Networking.Client;
using Uninarok.Units;

namespace Uninarok.World.View
{
    /// <summary>
    /// The server's weapon value to its base weapon type, and a job to the job whose sprites it uses, from the sprite catalog and
    /// the composition tables (SPEC-0011 FR-4, FR-14; PLAN-0011 4.7, D18). A value of 500 or more is an item id: its view id is the
    /// item table's (PLAN-0010 D22); below, a view id. Unknown: 0 (bare hand), one trace line per value.
    /// </summary>
    public sealed class CatalogWeaponTypes : IWeaponTypes, IJobBases
    {
        readonly IUnitAssetSource _source;
        readonly HashSet<int> _reported = new HashSet<int>();

        public CatalogWeaponTypes(IUnitAssetSource source) => _source = source ?? throw new ArgumentNullException(nameof(source));

        public int TypeOf(int weapon)
        {
            if (weapon == 0) return 0;
            var tables = _source.Tables;
            if (tables == null) return 0;
            var hand = WeaponHands.Right(weapon, _source.Catalog, tables);
            // An item that is no weapon, or a view id without a type, is unknown; a weapon item whose view id is 0 is a bare hand.
            if (hand.Kind == HandKind.Unknown || (hand.Kind == HandKind.Weapon && hand.WeaponType == 0 && hand.ViewId != 0)) return Unknown(weapon);
            return hand.WeaponType;
        }

        /// <summary>Both hands as one weapon type, by the rule display and combat share (SPEC-0009 FR-17).</summary>
        public HandsResult HandsOf(int job, int right, int left) => WeaponHands.Of(job, right, left, _source.Catalog, _source.Tables);

        public int BaseJobOf(int job)
        {
            var tables = _source.Tables;
            return tables != null && tables.TryGetBaseJob(job, out int baseJob) ? baseJob : job;
        }

        int Unknown(int weapon)
        {
            if (_reported.Add(weapon)) NetLog.Trace("weapon value " + weapon + " has no weapon type: bare hand");
            return 0;
        }
    }
}
