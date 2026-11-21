using Uninarok.Catalog;
using Uninarok.RoImport.Core.Catalog.Tables;
using Uninarok.RoImport.Core.Results;

namespace Uninarok.RoImport.Core.Catalog.Planning
{
    /// <summary>
    /// Palettes (SPEC-0008 FR-2, FR-3, FR-9; PLAN-0008 4.3.10): one record per <c>.pal</c> under <c>palette/</c>, keyed by its
    /// stem relative to the folder (<c>몸/기사_남_1</c>), value the path. A palette has no ACT/SPR pair; it goes through
    /// <c>RoPaletteConverter</c> and the same ledger as the palettes the body and head planners attach, so it is converted
    /// once. Any other file in <c>palette/</c> is a stray.
    /// </summary>
    public sealed class RoPalettePlanner : IRoCatalogPlanner
    {
        public SpriteCatalogCategory Category => SpriteCatalogCategory.Palettes;
        public RoCatalogNeeds Needs => RoCatalogNeeds.None;

        public void Plan(RoCatalogContext ctx, RoCatalogPlan.Builder plan)
        {
            var files = ctx.Files(RoFileNames.PaletteRoot);
            foreach (var path in files.All)
            {
                if (path.Extension != RoFileNames.PalExtension)
                {
                    plan.Gap(RoIssueCode.CatalogStrayFile, Category, path.Canonical, "not a palette");
                    continue;
                }
                plan.Add(Category, RoFileNames.StemUnder(path, RoFileNames.PaletteRoot), null, new[] { RoCatalogValue.Of("palette", path.Canonical) });
            }
        }
    }
}
