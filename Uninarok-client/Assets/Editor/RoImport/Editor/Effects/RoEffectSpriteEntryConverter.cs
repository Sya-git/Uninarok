using System;
using System.Collections.Generic;
using UnityEngine;
using Uninarok.Effects;
using Uninarok.RoImport.Core.Catalog.Planning;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Source;
using Uninarok.RoImport.Editor.Ledger;
using Uninarok.RoImport.Editor.Pipeline;
using Uninarok.RoImport.Editor.Ui;

namespace Uninarok.RoImport.Editor.Effects
{
    /// <summary>
    /// One catalog entry per effect sprite (PLAN-0012 4.4; SPEC-0012 FR-4, FR-5): job path <c>sprite/이팩트/&lt;name&gt;.act</c>,
    /// output <c>Art/Imported/effect/sprite/이팩트/&lt;name&gt;.act.asset</c>, an <see cref="EffectAsset"/> of kind
    /// <see cref="EffectKind.Sprite"/> whose <see cref="EffectAsset.Path"/> is the canonical key (the ACT path) and whose
    /// <see cref="EffectAsset.SpriteName"/> is the SPEC-0008 effect name (the path under <c>sprite/이팩트/</c> without
    /// extension, as <c>RoEffectPlanner</c> names it). It references the sprite by name and imports nothing: the ACT and
    /// SPR content are never opened, only their presence is checked. An ACT without its SPR has no SPEC-0008 name, so it is
    /// one warning and no entry. Ledger category <see cref="LedgerCategory"/>, hash = the name, written in place (GUID kept).
    /// </summary>
    public sealed class RoEffectSpriteEntryConverter : IRoConverter
    {
        /// <summary>The ledger of effect assets (PLAN-0012 3): the same one the STR assets use.</summary>
        public const string LedgerCategory = "effect";

        /// <summary>Raised when what an entry writes changes.</summary>
        public const int RulesVersion = 1;

        /// <summary>The folder the SPEC-0008 effect names are relative to.</summary>
        public static string Prefix => RoFileNames.SpriteFolder(RoEffectPlanner.Folder);

        public RoJobKind Kind => RoJobKind.EffectSprites;

        /// <summary>The <c>.act</c> files under <c>sprite/이팩트/</c>.</summary>
        public bool Handles(RoPath path) =>
            path.Extension == RoFileNames.ActExtension && path.Canonical.StartsWith(Prefix, StringComparison.Ordinal);

        /// <summary>Every effect sprite ACT of a source, in the importer's job order.</summary>
        public static List<RoJob> Jobs(IRoSource source) => RoImportJobs.Expand(Prefix, RoJobKind.EffectSprites, source).Jobs;

        public RoResult<UnityEngine.Object> Convert(RoImportContext ctx, RoPath path)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            if (!Handles(path))
                return Fail(path, RoIssueCode.SourceMissing, $"not an ACT under {Prefix}");
            if (!ctx.Source.TryGetFile(path, out _))
                return Fail(path, RoIssueCode.SourceMissing, "no such file in the source folder");

            var spr = path.WithExtension(RoFileNames.SprExtension);
            if (!ctx.Source.TryGetFile(spr, out _))
            {
                // SPEC-0008 names a sprite only when it has both files (FR-10): no name, no entry; the run goes on.
                ctx.Report.Warn(RoIssueCode.CatalogOrphanAct, path.Canonical, null, "the ACT has no SPR, so it has no effect sprite name; no entry is written");
                return RoResult<UnityEngine.Object>.Skipped(null);
            }

            string name = RoFileNames.StemUnder(path, Prefix);
            string output = RoEffectOutputPaths.Asset(ctx.Roots, path);
            var ledger = ctx.Ledger(LedgerCategory);
            bool ledgerCurrent = !ctx.Force && ledger.IsCurrent(path, name, RulesVersion, ctx.Source, ctx.Writer);

            var entry = ScriptableObject.CreateInstance<EffectAsset>();
            entry.Path = path.Canonical;
            entry.Kind = EffectKind.Sprite;
            entry.SpriteName = name;
            entry.SpriteAction = 0;

            // In place: the GUID is kept and an unchanged entry is not touched (PLAN-0002 4.7).
            var status = ctx.Writer.UpsertObject(output, entry);
            var asset = ctx.Writer.Load<EffectAsset>(output);
            ctx.EffectsConverted = true;
            if (ledgerCurrent && status == RoStatus.Skipped) return RoResult<UnityEngine.Object>.Skipped(asset);

            ledger.Record(new RoLedgerEntry { Source = path.Canonical, SourceHash = name, Rules = RulesVersion, Outputs = new[] { output } }, ctx.Report);
            return new RoResult<UnityEngine.Object>(status == RoStatus.Skipped ? RoStatus.Updated : status, asset);
        }

        static RoResult<UnityEngine.Object> Fail(RoPath path, RoIssueCode code, string message) =>
            RoResult<UnityEngine.Object>.Failed(new RoIssue(code, path.Canonical, null, message));
    }
}
