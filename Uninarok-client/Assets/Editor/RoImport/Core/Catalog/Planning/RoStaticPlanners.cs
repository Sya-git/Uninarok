using System;
using Uninarok.Catalog;
using Uninarok.RoImport.Core.Catalog.Tables;

namespace Uninarok.RoImport.Core.Catalog.Planning
{
    /// <summary>
    /// Ground items (SPEC-0008 FR-5, FR-10; PLAN-0008 4.3.8): one record per distinct resource name of the item table
    /// (identified and unidentified), keyed by the name, pair <c>아이템/&lt;resource&gt;</c>.
    /// </summary>
    public sealed class RoGroundItemPlanner : IRoCatalogPlanner
    {
        public const string Folder = "아이템";

        public SpriteCatalogCategory Category => SpriteCatalogCategory.GroundItems;
        public RoCatalogNeeds Needs => RoCatalogNeeds.Items;

        public void Plan(RoCatalogContext ctx, RoCatalogPlan.Builder plan) => throw new NotImplementedException("pending T4.4");
    }

    /// <summary>
    /// Skill icons (SPEC-0008 FR-7, FR-10; PLAN-0008 4.3.8): one record per skill id whose lower-case <c>SKID</c> constant
    /// has a file in <c>아이템/</c>, keyed by the id. Monster skills (<c>NPC_*</c>) have none: one summary gap, not one each.
    /// </summary>
    public sealed class RoSkillIconPlanner : IRoCatalogPlanner
    {
        public const string MonsterSkillPrefix = "NPC_";

        public SpriteCatalogCategory Category => SpriteCatalogCategory.SkillIcons;
        public RoCatalogNeeds Needs => RoCatalogNeeds.Skills;

        public void Plan(RoCatalogContext ctx, RoCatalogPlan.Builder plan) => throw new NotImplementedException("pending T4.4");
    }

    /// <summary>
    /// Effects (SPEC-0008 FR-6, FR-10; PLAN-0008 4.3.9, OQ-5): one record per sprite in <c>이팩트/</c> by its name relative
    /// to the folder; <c>emotion</c> also carries the emote ids (action = <c>ET_*</c> id).
    /// </summary>
    public sealed class RoEffectPlanner : IRoCatalogPlanner
    {
        public const string Folder = "이팩트";
        public const string EmoteSet = "emotion";
        /// <summary>The value name prefix of an emote id on the emote set record (<c>emote:ET_SURPRISE</c> = 0).</summary>
        public const string EmoteValuePrefix = "emote:";

        public SpriteCatalogCategory Category => SpriteCatalogCategory.Effects;
        public RoCatalogNeeds Needs => RoCatalogNeeds.Emotes;

        public void Plan(RoCatalogContext ctx, RoCatalogPlan.Builder plan) => throw new NotImplementedException("pending T4.5");
    }
}
