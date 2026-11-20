using System;
using Uninarok.Catalog;
using Uninarok.RoImport.Core.Catalog.Tables;

namespace Uninarok.RoImport.Core.Catalog.Planning
{
    /// <summary>Headgears (SPEC-0008 FR-4, FR-10; PLAN-0008 4.3.4): one record per view id of <c>AccNameTable</c>.</summary>
    public sealed class RoHeadgearPlanner : IRoCatalogPlanner
    {
        public const string Folder = "악세사리";

        public SpriteCatalogCategory Category => SpriteCatalogCategory.Headgears;
        public RoCatalogNeeds Needs => RoCatalogNeeds.ViewIds;

        public void Plan(RoCatalogContext ctx, RoCatalogPlan.Builder plan) => throw new NotImplementedException("pending T4.8");
    }

    /// <summary>
    /// Garments (SPEC-0008 FR-4, FR-10; PLAN-0008 4.3.5): one record per view id of <c>RobeNameTable</c>; each class ACT is
    /// paired with its own SPR if it has one, else the root sheet (<c>_doram</c> for a doram body).
    /// </summary>
    public sealed class RoGarmentPlanner : IRoCatalogPlanner
    {
        public const string Folder = "로브";

        public SpriteCatalogCategory Category => SpriteCatalogCategory.Garments;
        public RoCatalogNeeds Needs => RoCatalogNeeds.ViewIds | RoCatalogNeeds.Own;

        public void Plan(RoCatalogContext ctx, RoCatalogPlan.Builder plan) => throw new NotImplementedException("pending T4.9");
    }

    /// <summary>
    /// Weapons (SPEC-0008 FR-4, FR-10; PLAN-0008 4.3.6, 4.3.11, OQ-3): one record per view id, plus one per item id with its
    /// own file; trails are extra pairs of the same slot; a class without the view id's file falls back to the base type.
    /// </summary>
    public sealed class RoWeaponPlanner : IRoCatalogPlanner
    {
        public SpriteCatalogCategory Category => SpriteCatalogCategory.Weapons;
        public RoCatalogNeeds Needs => RoCatalogNeeds.ViewIds | RoCatalogNeeds.Own;

        public void Plan(RoCatalogContext ctx, RoCatalogPlan.Builder plan) => throw new NotImplementedException("pending T4.10");
    }

    /// <summary>
    /// Shields (SPEC-0008 FR-4, FR-10; PLAN-0008 4.3.7): one record per view id of <c>shield-names.csv</c>, plus one per
    /// item id with its own <c>&lt;id&gt;_방패</c> file.
    /// </summary>
    public sealed class RoShieldPlanner : IRoCatalogPlanner
    {
        public const string Folder = "방패";

        public SpriteCatalogCategory Category => SpriteCatalogCategory.Shields;
        public RoCatalogNeeds Needs => RoCatalogNeeds.Own;

        public void Plan(RoCatalogContext ctx, RoCatalogPlan.Builder plan) => throw new NotImplementedException("pending T4.11");
    }
}
