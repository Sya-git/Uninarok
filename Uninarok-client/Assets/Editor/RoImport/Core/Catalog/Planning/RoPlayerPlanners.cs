using System;
using Uninarok.Catalog;
using Uninarok.RoImport.Core.Catalog.Tables;

namespace Uninarok.RoImport.Core.Catalog.Planning
{
    /// <summary>
    /// Bodies (SPEC-0008 FR-2, FR-10; PLAN-0008 4.3.2): one record per base class (<see cref="RoCatalogKeys.Body"/>), one
    /// pair per (variant, sex) from the rows of <c>class-bodies.csv</c>, costumes from <c>costume_&lt;n&gt;/</c>, palettes by
    /// colour number per variant.
    /// </summary>
    public sealed class RoBodyPlanner : IRoCatalogPlanner
    {
        public SpriteCatalogCategory Category => SpriteCatalogCategory.Bodies;
        public RoCatalogNeeds Needs => RoCatalogNeeds.Own;

        public void Plan(RoCatalogContext ctx, RoCatalogPlan.Builder plan) => throw new NotImplementedException("pending T4.7");
    }

    /// <summary>
    /// Heads (SPEC-0008 FR-3, FR-10; PLAN-0008 4.3.3): one record per hair style number (<see cref="RoCatalogKeys.Head"/>),
    /// one pair per (sex, race), hair palettes by colour number.
    /// </summary>
    public sealed class RoHeadPlanner : IRoCatalogPlanner
    {
        public SpriteCatalogCategory Category => SpriteCatalogCategory.Heads;
        public RoCatalogNeeds Needs => RoCatalogNeeds.None;

        public void Plan(RoCatalogContext ctx, RoCatalogPlan.Builder plan) => throw new NotImplementedException("pending T4.7");
    }
}
