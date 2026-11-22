using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Uninarok.Catalog;
using Uninarok.RoImport.Core.Catalog.Composition;
using Uninarok.RoImport.Core.Catalog.Tables;
using Uninarok.RoImport.Core.Formats;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Editor.Ledger;
using Uninarok.RoImport.Editor.Pipeline;
using Uninarok.Sprites;
using Uninarok.Units;

namespace Uninarok.RoImport.Editor.Catalog
{
    /// <summary>
    /// The Composition import (PLAN-0009 4.1, D1, D20; SPEC-0009 FR-3, FR-4, FR-11): one job, <c>catalog/composition</c>. It
    /// reads the client's composition tables through <see cref="RoCompositionTablesBuilder"/>, converts the ground shadow
    /// sprite and writes <c>Art/catalog/composition.asset</c>, addressable as <see cref="CompositionTablesKey.Address"/>. A
    /// missing or unreadable table gives one warning and stays empty.
    /// </summary>
    public sealed class RoCompositionTablesConverter : IRoConverter
    {
        public const string Job = RoCatalogRun.JobRoot + "composition";
        public const string ShadowAct = "sprite/shadow.act";

        /// <summary>Raised when what this job writes changes.</summary>
        public const int RulesVersion = 1;

        /// <summary>The table groups the builder needs.</summary>
        public const RoCatalogNeeds Needs = RoCatalogNeeds.Jobs | RoCatalogNeeds.ViewIds;

        public RoJobKind Kind => RoJobKind.Composition;

        public bool Handles(RoPath path) => path.Canonical == Job;

        /// <summary><c>Art/catalog/composition.asset</c>.</summary>
        public static string AssetPath(RoOutputRoots roots) => RoCatalogOutputPaths.Root(roots) + "/composition.asset";

        public RoResult<UnityEngine.Object> Convert(RoImportContext ctx, RoPath path)
        {
            var run = ctx.CatalogPlan;
            if (run == null)
                return RoResult<UnityEngine.Object>.Failed(new RoIssue(RoIssueCode.Unexpected, path.Canonical, null, "a catalog job outside a catalog run (use RoImport.Catalog)"));

            string assetPath = AssetPath(ctx.Roots);
            var act = RoPath.Parse(ShadowAct);
            var shadow = ctx.Source.TryGetFile(act, out _) ? ctx.Sprite(act, act.WithExtension(".spr")) : default;
            bool hasShadow = shadow.Value != null && shadow.Status != RoStatus.Failed;
            if (!hasShadow) ctx.Report.Warn(RoIssueCode.SourceMissing, ShadowAct, path.Canonical, "no ground shadow sprite: units get no shadow");

            string hash = Hash(ctx);
            var ledger = ctx.Ledger(RoCatalogEntryConverter.LedgerCategory);
            bool current = !ctx.Force && ledger.IsCurrent(path, hash, RulesVersion, ctx.Source, ctx.Writer);
            var existing = ctx.Writer.Load<CompositionTables>(assetPath);
            if (current && existing != null && (!hasShadow || shadow.Status == RoStatus.Skipped))
            {
                EnsureAddressable(ctx, run, assetPath, shadow.Value as SpriteAnimation);
                return RoResult<UnityEngine.Object>.Skipped(existing);
            }

            var (data, gaps) = RoCompositionTablesBuilder.Build(run.Tables, f => TextOf(ctx, f));
            foreach (var g in gaps)
                ctx.Report.Warn(g.Missing ? RoIssueCode.SourceMissing : RoIssueCode.CorruptFile, RoCatalogTables.TablesFolder + g.File, path.Canonical, g.Message);

            var fresh = ScriptableObject.CreateInstance<CompositionTables>();
            fresh.name = "composition";
            data.ApplyTo(fresh);
            fresh.Shadow = hasShadow ? RefOf(shadow.Value) : new SpriteRef { Animation = new SpriteAnimationReference(string.Empty), Still = new AssetReferenceSprite(string.Empty) };
            var status = ctx.Writer.UpsertObject(assetPath, fresh);
            ledger.Record(new RoLedgerEntry { Source = path.Canonical, SourceHash = hash, Rules = RulesVersion, Outputs = new[] { assetPath } }, ctx.Report);
            EnsureAddressable(ctx, run, assetPath, shadow.Value as SpriteAnimation);

            var written = ctx.Writer.Load<CompositionTables>(assetPath);
            switch (status)
            {
                case RoStatus.Created: return RoResult<UnityEngine.Object>.Created(written);
                case RoStatus.Updated: return RoResult<UnityEngine.Object>.Updated(written);
                default: return RoResult<UnityEngine.Object>.Skipped(written);
            }
        }

        static SpriteRef RefOf(UnityEngine.Object value)
        {
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(value));
            return value is SpriteAnimation
                ? new SpriteRef { Animation = new SpriteAnimationReference(guid), Still = new AssetReferenceSprite(string.Empty) }
                : new SpriteRef { Animation = new SpriteAnimationReference(string.Empty), Still = new AssetReferenceSprite(guid) { SubObjectName = value.name } };
        }

        static string TextOf(RoImportContext ctx, string file)
        {
            var path = RoPath.Parse(RoCatalogTables.TablesFolder + file);
            if (!ctx.Source.TryGetFile(path, out var source)) return null;
            return RoLubReader.TryRead(path, source.ReadAllBytes(), RoLubEncoding.Cp949, out string lua, out _) ? lua : null;
        }

        // Every lub the builder reads and the identity tables behind its constants.
        static string Hash(RoImportContext ctx)
        {
            var files = RoCompositionTablesBuilder.Files.Concat(new[]
            {
                "datainfo/" + RoJobTables.NpcIdentityFile, "datainfo/" + RoJobTables.JobIdentityFile, "datainfo/" + RoJobTables.JobNameFile,
                "datainfo/" + RoViewIdTables.AccessoryIdFile, "datainfo/" + RoViewIdTables.RobeIdFile,
            });
            var text = new StringBuilder();
            foreach (string f in files)
                text.Append(f).Append('\t').Append(ctx.Source.TryGetFile(RoPath.Parse(RoCatalogTables.TablesFolder + f), out var file) ? file.ContentHash() : "-").Append('\n');
            using (var sha = System.Security.Cryptography.SHA256.Create())
                return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())).Select(b => b.ToString("x2")));
        }

        // The asset under its own key: a single entry, not a folder. The shadow is static today, so its sheet is already in the
        // sheets folder entry; an animation at the sprite root would need an entry of its own (D20).
        static void EnsureAddressable(RoImportContext ctx, RoCatalogRun run, string assetPath, SpriteAnimation shadow)
        {
            if (ctx.Writer.InBatch) return;
            run.Addressables = run.Addressables ?? RoCatalogAddressables.ProjectSettings();
            bool changed = Entry(run.Addressables, "catalog-tables", assetPath, CompositionTablesKey.Address);
            string shadowPath = shadow != null ? AssetDatabase.GetAssetPath(shadow) : null;
            if (shadowPath != null) changed |= Entry(run.Addressables, "sprites-effect", shadowPath, shadowPath);
            if (!changed) return;
            run.Addressables.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
            AssetDatabase.SaveAssets();
        }

        static bool Entry(AddressableAssetSettings settings, string groupName, string assetPath, string address)
        {
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid)) return false;
            var group = settings.FindGroup(groupName)
                        ?? settings.CreateGroup(groupName, false, false, false, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var entry = settings.FindAssetEntry(guid);
            if (entry != null && entry.parentGroup == group && entry.address == address) return false;
            entry = settings.CreateOrMoveEntry(guid, group, false, false);
            entry.SetAddress(address, false);
            return true;
        }
    }
}
