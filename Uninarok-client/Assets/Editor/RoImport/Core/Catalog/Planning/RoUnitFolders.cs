using System;
using Uninarok.Catalog;

namespace Uninarok.RoImport.Core.Catalog.Planning
{
    /// <summary>
    /// Which unit category and folder a job id belongs to, by id range only, never by which file exists (SPEC-0008 FR-1;
    /// PLAN-0008 4.3.1, D11): 45–999 and 10001–19999 are NPCs (<c>npc/</c>), 1001–3999 and 20000 up are monsters
    /// (<c>몬스터/</c>), 6001–6099 are homunculi (<c>homun/</c>).
    /// </summary>
    public static class RoUnitFolders
    {
        public const string NpcFolder = "npc";
        public const string MonsterFolder = "몬스터";
        public const string HomunculusFolder = "homun";

        public static bool TryGetCategory(int jobId, out SpriteCatalogCategory category) => throw new NotImplementedException("pending T4.3");

        /// <summary>The sprite folder of a unit category (relative to <c>sprite/</c>).</summary>
        public static string FolderOf(SpriteCatalogCategory unitCategory) => throw new NotImplementedException("pending T4.3");
    }
}
