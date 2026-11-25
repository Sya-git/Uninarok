using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using Uninarok.Effects;
using Uninarok.RoImport.Core.Effects;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Source;
using Uninarok.RoImport.Editor.Pipeline;
using Uninarok.RoImport.Editor.Ui;
using RoImportApi = Uninarok.RoImport.Editor.Pipeline.RoImport;   // the class name equals our root namespace

namespace Uninarok.RoImport.Editor.Effects
{
    /// <summary>
    /// The effect folder import (PLAN-0012 4.10; SPEC-0012 FR-2, FR-7): the STRs under the prefix lines, then the textures that
    /// authored entries name in their <c>TextureNames</c>, through the same effect texture category and ledger rules, in one
    /// report. Callable from code; the menu <c>Convert Effect Folder</c> is a thin wrapper.
    /// </summary>
    public static class RoEffectFolderImport
    {
        /// <summary>One authored entry's texture names: <see cref="Entry"/> is its key (<c>texture/effect/_authored/warp_portal/sparkle.quad</c>).</summary>
        public readonly struct AuthoredTextures
        {
            public AuthoredTextures(string entry, IReadOnlyList<string> names) { Entry = entry; Names = names; }
            public string Entry { get; }
            public IReadOnlyList<string> Names { get; }
        }

        /// <summary>The texture names of every <c>EffectAsset</c> under the authored root of <paramref name="roots"/>.</summary>
        public static List<AuthoredTextures> LoadAuthored(RoOutputRoots roots)
        {
            roots = roots ?? new RoOutputRoots();
            string folder = RoEffectOutputPaths.AuthoredRoot(roots);
            var list = new List<AuthoredTextures>();
            if (!AssetDatabase.IsValidFolder(folder)) return list;
            foreach (string guid in AssetDatabase.FindAssets("t:EffectAsset", new[] { folder }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<EffectAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset == null || string.IsNullOrEmpty(asset.Path) || asset.TextureNames == null || asset.TextureNames.Length == 0) continue;
                list.Add(new AuthoredTextures(asset.Path, asset.TextureNames));
            }
            list.Sort((a, b) => string.CompareOrdinal(a.Entry, b.Entry));
            return list;
        }

        /// <summary>
        /// Converts the STRs under <paramref name="prefixes"/> (null: the defaults; <paramref name="includeStr"/> false: none),
        /// then every texture <paramref name="authored"/> names (null: those of <see cref="LoadAuthored"/>). A name that is a
        /// path is taken as it is; a bare name is looked up in the entry's own folder, then in <c>texture/effect</c>.
        /// </summary>
        public static RoReport Run(RoImportOptions options, string prefixes = null, IReadOnlyList<AuthoredTextures> authored = null, bool includeStr = true)
        {
            options = options ?? new RoImportOptions();
            var report = new RoReport();
            var source = options.Source ?? new RoFolderSource(RoSourceLocation.Resolve());
            var run = options.Clone();
            run.Source = source;

            if (includeStr)
            {
                if (string.IsNullOrWhiteSpace(prefixes)) prefixes = "texture/effect/\nsprite/이팩트/";
                RoBatch.Run(RoImportJobs.Expand(prefixes, RoJobKind.Effect, source).Jobs, run, report);
            }

            authored = authored ?? LoadAuthored(run.Roots);
            using (var session = new RoImportApi.Session(run, report))
            {
                var ctx = session.Context;
                foreach (var entry in authored)
                {
                    if (entry.Names == null || !RoPath.TryParse(entry.Entry, out var entryPath)) continue;
                    foreach (string name in entry.Names)
                    {
                        if (!TryFind(source, entryPath, name, report, out var texture)) continue;   // already one TextureMissing warning
                        var result = ctx.EffectTexture(texture, entryPath.Canonical);
                        switch (result.Status)
                        {
                            case RoStatus.Created: report.Created++; break;
                            case RoStatus.Updated: report.Updated++; break;
                            case RoStatus.Skipped: report.Skipped++; break;
                            case RoStatus.Failed:
                                report.Fail(texture, result.Failure ?? new RoIssue(RoIssueCode.Unexpected, texture.Canonical, entryPath.Canonical, "failed without a reason"));
                                break;
                        }
                    }
                }
            }
            UnityEngine.Debug.Log(report.Summarize());
            return report;
        }

        static bool TryFind(IRoSource source, RoPath entry, string name, RoReport report, out RoPath texture)
        {
            texture = default;
            if (!string.IsNullOrWhiteSpace(name) && (name.IndexOf('/') >= 0 || name.IndexOf('\\') >= 0)
                && RoPath.TryParse(name, out var full) && source.TryGetFile(full, out _))
            {
                texture = full;
                return true;
            }
            return RoEffectTextureLookup.TryResolve(source, entry, name, report, out texture);
        }
    }
}
