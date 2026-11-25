using System;
using System.Collections.Generic;
using System.Linq;
using Uninarok.Effects;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Source;

namespace Uninarok.RoImport.Core.Effects
{
    /// <summary>
    /// Read-only check of the effect data (PLAN-0012 4.9, D30, SPEC-0012 FR-1). Only asks <see cref="IRoSource"/> for
    /// existence and counts; never opens RO file bytes.
    /// </summary>
    public static class RoEffectDataCheck
    {
        const string TexturePrefix = "texture/effect/";
        const string SpritePrefix = "sprite/이팩트/";
        const string ToolPrefix = "luafiles514/lua files/effecttool/";
        const string IndexReferrer = "(index)";

        /// <summary>The three warp portal keys, in report order (PLAN-0012 4.7).</summary>
        public static string[] WarpPortalKeys => new[]
        {
            "texture/effect/_authored/warp_portal/sparkle.quad",
            "texture/effect/_authored/warp_portal/ring_outer.cylinder",
            "texture/effect/_authored/warp_portal/ring_inner.cylinder",
        };

        static string[] ImageExtensions => new[] { ".tga", ".bmp", ".png", ".jpg", ".jpeg" };

        public static RoEffectDataReport Run(IRoSource source, EffectCatalogIndex index,
            Func<EffectAssetRef, EffectAsset> load, IEnumerable<EffectAsset> authored = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var report = new RoEffectDataReport();
            Count(source, report);

            // Assets examined: the index's plus the authored ones, each Path once, in ordinal order of Path.
            var byPath = new SortedDictionary<string, EffectAsset>(StringComparer.Ordinal);
            if (index != null && index.Assets != null && load != null)
                foreach (var r in index.Assets) Add(byPath, load(r));
            if (authored != null)
                foreach (var a in authored) Add(byPath, a);

            var firstReferrer = new Dictionary<string, string>(StringComparer.Ordinal);
            var cleanKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pair in byPath)
            {
                bool clean = true;
                void Miss(string file, string referrer)
                {
                    clean = false;
                    if (!firstReferrer.ContainsKey(file)) firstReferrer[file] = referrer;
                }
                Examine(source, pair.Value, Miss);
                if (clean) cleanKeys.Add(pair.Key);
            }

            report.Missing = firstReferrer
                .Select(p => (File: p.Key, Referrer: p.Value))
                .OrderBy(m => m.File, StringComparer.Ordinal)
                .ThenBy(m => m.Referrer, StringComparer.Ordinal)
                .ToArray();
            report.WarpPortal = WarpPortalKeys.Select(k => (k, cleanKeys.Contains(k))).ToArray();
            return report;
        }

        static void Add(SortedDictionary<string, EffectAsset> byPath, EffectAsset a)
        {
            if (a != null && !string.IsNullOrEmpty(a.Path) && !byPath.ContainsKey(a.Path)) byPath[a.Path] = a;
        }

        static void Count(IRoSource source, RoEffectDataReport report)
        {
            foreach (var p in source.Enumerate(""))
            {
                string c = p.Canonical;
                string ext = p.Extension ?? string.Empty;
                if (Eq(ext, ".str")) report.Str++;
                if (c.StartsWith(TexturePrefix, StringComparison.Ordinal) && ImageExtensions.Any(e => Eq(ext, e))) report.Textures++;
                if (c.StartsWith(SpritePrefix, StringComparison.Ordinal) && (Eq(ext, ".spr") || Eq(ext, ".act"))) report.SprAct++;
                if (c.StartsWith(ToolPrefix, StringComparison.Ordinal) && Eq(ext, ".lub")) report.ToolTables++;
            }
        }

        static bool Eq(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        static void Examine(IRoSource source, EffectAsset asset, Action<string, string> miss)
        {
            string key = asset.Path;
            switch (asset.Kind)
            {
                case EffectKind.Str:
                    if (RoPath.TryParse(key, out var str))
                    {
                        if (!source.TryGetFile(str, out _)) miss(key, IndexReferrer);
                        Textures(source, str, asset, miss);
                    }
                    else miss(key, IndexReferrer);
                    break;
                case EffectKind.Quad:
                case EffectKind.Cylinder:
                case EffectKind.Procedural:
                    // no source file; names resolve from the effect root (a stand-in STR path there gives the same folder)
                    Textures(source, RoEffectTextureLookup.EffectRoot.Combine("_.str"), asset, miss);
                    break;
                case EffectKind.Sprite:
                    foreach (var ext in new[] { ".act", ".spr" })
                        RequireFile(source, SpritePrefix + asset.SpriteName + ext, key, miss);
                    break;
                case EffectKind.Model:
                    RequireFile(source, key, key, miss);
                    break;
            }
        }

        static void Textures(IRoSource source, RoPath str, EffectAsset asset, Action<string, string> miss)
        {
            if (asset.TextureNames == null) return;
            foreach (var name in asset.TextureNames)
                if (!RoEffectTextureLookup.TryResolve(source, str, name, null, out _)) miss(name ?? string.Empty, asset.Path);
        }

        static void RequireFile(IRoSource source, string file, string referrer, Action<string, string> miss)
        {
            if (!RoPath.TryParse(file, out var p) || !source.TryGetFile(p, out _)) miss(file, referrer);
        }
    }
}
