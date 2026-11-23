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
        const int FirstItemId = 500;

        readonly IUnitAssetSource _source;
        readonly HashSet<int> _reported = new HashSet<int>();

        public CatalogWeaponTypes(IUnitAssetSource source) => _source = source ?? throw new ArgumentNullException(nameof(source));

        public int TypeOf(int weapon)
        {
            if (weapon == 0) return 0;
            var tables = _source.Tables;
            if (tables == null) return 0;
            int view = weapon;
            if (weapon >= FirstItemId)
            {
                var catalog = _source.Catalog;
                if (catalog == null || !catalog.TryGetItemView(weapon, out var kind, out view) || kind != EquipmentKind.Weapon) return Unknown(weapon);
            }
            int type = tables.WeaponTypeOf(view);
            return type != 0 || view == 0 ? type : Unknown(weapon);
        }

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
