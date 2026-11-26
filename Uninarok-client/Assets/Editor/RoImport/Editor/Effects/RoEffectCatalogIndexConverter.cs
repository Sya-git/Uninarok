using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Uninarok.Effects;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Editor.Ledger;
using Uninarok.RoImport.Editor.Pipeline;

namespace Uninarok.RoImport.Editor.Effects
{
    /// <summary>
    /// The catalog index of the effect assets (PLAN-0012 4.4; SPEC-0012 FR-5, FR-10): job path <see cref="JobPath"/>, output
    /// <c>Art/Imported/effect/_index.asset</c>. It scans the generated effect folder and <c>Data/Effects/Authored</c> for
    /// <see cref="EffectAsset"/>s and writes one key per asset, sorted ordinally, with the asset's GUID: keys and references
    /// only, no effect content. Two assets with one key give one <see cref="RoIssueCode.EffectKeyDuplicate"/> warning and the
    /// ordinal-first asset path wins. An authored asset whose <c>Path</c> disagrees with its file location gives
    /// <see cref="RoIssueCode.EffectKeyPathMismatch"/> (it is still listed under its own key). <c>SourceMissing[i]</c> is true
    /// when a generated asset's source file (derived from its <c>Path</c>) is gone from the working data; authored assets have
    /// no source. Written in place, so an unchanged index is not touched; ledger <c>effect/_index</c>.
    /// </summary>
    public sealed class RoEffectCatalogIndexConverter : IRoConverter
    {
        /// <summary>Raised when what the index holds changes.</summary>
        public const int RulesVersion = 1;

        /// <summary>The job path of the index.</summary>
        public static RoPath JobPath => RoPath.Parse(RoEffectSpriteEntryConverter.LedgerCategory + "/_index");

        public RoJobKind Kind => RoJobKind.EffectIndex;

        public bool Handles(RoPath path) => path.Canonical == JobPath.Canonical;

        public RoResult<UnityEngine.Object> Convert(RoImportContext ctx, RoPath path)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            if (!Handles(path))
                return RoResult<UnityEngine.Object>.Failed(new RoIssue(RoIssueCode.SourceMissing, path.Canonical, null, $"not the effect index job ({JobPath.Canonical})"));

            string generated = RoEffectOutputPaths.Root(ctx.Roots);
            string authored = RoEffectOutputPaths.AuthoredRoot(ctx.Roots);
            string indexPath = RoEffectOutputPaths.Index(ctx.Roots);

            var folders = new[] { generated, authored }.Where(AssetDatabase.IsValidFolder).ToArray();
            var assetPaths = folders.Length == 0
                ? new List<string>()
                : AssetDatabase.FindAssets("t:EffectAsset", folders).Select(AssetDatabase.GUIDToAssetPath).Distinct().ToList();
            assetPaths.Sort(StringComparer.Ordinal);

            // key -> (asset path, source missing); the ordinal-first asset path wins.
            var byKey = new SortedDictionary<string, (string asset, bool missing)>(StringComparer.Ordinal);
            var losers = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (string assetPath in assetPaths)
            {
                var asset = AssetDatabase.LoadAssetAtPath<EffectAsset>(assetPath);
                if (asset == null) continue;
                bool isAuthored = assetPath.StartsWith(authored + "/", StringComparison.Ordinal);
                string key = asset.Path;
                if (string.IsNullOrEmpty(key))
                {
                    ctx.Report.Warn(RoIssueCode.EffectKeyPathMismatch, assetPath, null, "the effect asset has no Path (its catalog key); it is not listed");
                    continue;
                }
                if (isAuthored && assetPath != authored + "/" + key + ".asset")
                    ctx.Report.Warn(RoIssueCode.EffectKeyPathMismatch, assetPath, null, $"the asset's Path '{key}' disagrees with its location; expected {authored}/{key}.asset");

                if (byKey.ContainsKey(key))
                {
                    if (!losers.TryGetValue(key, out var list)) losers[key] = list = new List<string>();
                    list.Add(assetPath);
                    continue;
                }
                byKey.Add(key, (assetPath, !isAuthored && SourceGone(ctx, key)));
            }
            foreach (var pair in losers.OrderBy(p => p.Key, StringComparer.Ordinal))
                ctx.Report.Warn(RoIssueCode.EffectKeyDuplicate, pair.Key, null,
                    $"the key is held by {byKey[pair.Key].asset} (kept) and {string.Join(", ", pair.Value)} (ignored)");

            var index = ScriptableObject.CreateInstance<EffectCatalogIndex>();
            index.name = "_index";
            index.Keys = byKey.Keys.ToArray();
            index.Assets = byKey.Values.Select(v => new EffectAssetRef { Guid = AssetDatabase.AssetPathToGUID(v.asset) }).ToArray();
            index.SourceMissing = byKey.Values.Select(v => v.missing).ToArray();

            var status = ctx.Writer.UpsertObject(indexPath, index);
            var written = ctx.Writer.Load<EffectCatalogIndex>(indexPath);
            ctx.EffectsConverted = true;

            var ledger = ctx.Ledger(RoEffectSpriteEntryConverter.LedgerCategory);
            ledger.Record(new RoLedgerEntry { Source = path.Canonical, SourceHash = Fingerprint(written), Rules = RulesVersion, Outputs = new[] { indexPath } }, ctx.Report);
            return new RoResult<UnityEngine.Object>(status, written);
        }

        // Existence only, through the importer's source; a key that is no source path (an authored or odd key) counts as present.
        static bool SourceGone(RoImportContext ctx, string key) =>
            RoPath.TryParse(key, out var source) && !ctx.Source.TryGetFile(source, out _);

        static string Fingerprint(EffectCatalogIndex index)
        {
            var text = string.Join("\n", index.Keys.Select((k, i) => k + "\t" + index.Assets[i].Guid + "\t" + (index.SourceMissing[i] ? "1" : "0")));
            using (var sha = System.Security.Cryptography.SHA256.Create())
                return string.Concat(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text)).Select(b => b.ToString("x2")));
        }
    }
}
