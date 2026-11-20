using System;
using Uninarok.Catalog;
using Uninarok.RoImport.Core.Catalog.Tables;

namespace Uninarok.RoImport.Core.Catalog.Planning
{
    /// <summary>
    /// Monsters, NPCs or homunculi (SPEC-0008 FR-1, FR-10; PLAN-0008 4.3.1): one record per job id of
    /// <c>JobNameTable</c> in the category's id range, keyed by the id, pair <c>&lt;folder&gt;/&lt;name&gt;</c>, values the
    /// id, the name and the display scale (default 1).
    /// </summary>
    public sealed class RoUnitPlanner : IRoCatalogPlanner
    {
        public RoUnitPlanner(SpriteCatalogCategory category)
        {
            if (category != SpriteCatalogCategory.Monsters && category != SpriteCatalogCategory.Npcs && category != SpriteCatalogCategory.Homunculi)
                throw new ArgumentException($"{category} is not a unit category", nameof(category));
            Category = category;
        }

        public SpriteCatalogCategory Category { get; }
        public RoCatalogNeeds Needs => RoCatalogNeeds.Jobs | RoCatalogNeeds.Scales;

        public void Plan(RoCatalogContext ctx, RoCatalogPlan.Builder plan) => throw new NotImplementedException("pending T4.3");
    }
}
