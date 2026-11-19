using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Editor.Pipeline;

namespace Uninarok.RoImport.Editor.Sprites
{
    /// <summary>Options of one sprite conversion (PLAN-0007 4.4).</summary>
    public sealed class RoSpriteOptions
    {
        /// <summary>Write and attach the index texture, so palettes can recolour the sprite (FR-7).</summary>
        public bool Palettes;
    }

    /// <summary>SPR/ACT to a sheet and a <c>SpriteAnimation</c> (PLAN-0007 4.4). Implemented by TASKS-0007 T5.2.</summary>
    public sealed class RoSpriteConverter
    {
        public const string Category = "sprite";
        public const int RulesVersion = 1;

        public RoResult<UnityEngine.Object> Convert(RoImportContext ctx, RoPath act) =>
            RoResult<UnityEngine.Object>.Failed(new RoIssue(RoIssueCode.Unexpected, act.Canonical, null, "not implemented (TASKS-0007 T5.2)"));

        public static RoResult<UnityEngine.Object> Convert(RoImportContext ctx, RoPath act, RoPath spr, RoSpriteOptions options) =>
            RoResult<UnityEngine.Object>.Failed(new RoIssue(RoIssueCode.Unexpected, act.Canonical, null, "not implemented (TASKS-0007 T5.2)"));
    }
}
