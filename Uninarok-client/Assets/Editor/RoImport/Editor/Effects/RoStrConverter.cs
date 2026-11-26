using System;
using System.Collections.Generic;
using UnityEngine;
using Uninarok.Effects;
using Uninarok.RoImport.Core.Effects;
using Uninarok.RoImport.Core.Formats;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Editor.Ledger;
using Uninarok.RoImport.Editor.Pipeline;

namespace Uninarok.RoImport.Editor.Effects
{
    /// <summary>
    /// One STR to one <see cref="EffectAsset"/> of kind <see cref="EffectKind.Str"/> at
    /// <c>Art/Imported/effect/&lt;canonical path&gt;.asset</c> (PLAN-0012 4.1, 4.3, 4.10; SPEC-0012 FR-2, FR-10). The STR is read
    /// through <see cref="RoStrReader"/> (GRF), flattened by <see cref="RoStrCompiler"/>, and each texture name it holds is
    /// resolved by <see cref="RoEffectTextureLookup"/> and converted through <see cref="RoImportContext.EffectTexture"/> (a
    /// texture shared by two STRs is converted once per run). A missing texture is one <see cref="RoIssueCode.TextureMissing"/>
    /// warning and the placeholder texture in its slot. A corrupt or unsupported STR is one issue and no output. Ledger
    /// category <see cref="LedgerCategory"/>: source = the STR, dependencies = every resolved texture file, plus every file a
    /// missing texture could have been (empty hash), so a changed or newly appearing texture converts its STRs again.
    /// Written in place (GUID kept).
    /// </summary>
    public sealed class RoStrConverter : IRoConverter
    {
        /// <summary>The ledger of effect assets (PLAN-0012 3): the same one the sprite entries use.</summary>
        public const string LedgerCategory = RoEffectSpriteEntryConverter.LedgerCategory;

        /// <summary>Raised when what an STR asset holds changes.</summary>
        public const int Rules = 1;

        /// <summary>The extension of an STR.</summary>
        public const string StrExtension = ".str";

        public RoJobKind Kind => RoJobKind.Effect;

        public bool Handles(RoPath path) => path.Extension == StrExtension;

        public RoResult<UnityEngine.Object> Convert(RoImportContext ctx, RoPath path)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            if (!Handles(path)) return Fail(ctx, path, RoIssueCode.SourceMissing, "not an STR file");
            if (!ctx.Source.TryGetFile(path, out var file))
                return Fail(ctx, path, RoIssueCode.SourceMissing, "no such file in the source folder");

            string hash = file.ContentHash();
            string output = RoEffectOutputPaths.Asset(ctx.Roots, path);
            var ledger = ctx.Ledger(LedgerCategory);

            bool ledgerCurrent = !ctx.Force && ledger.IsCurrent(path, hash, Rules, ctx.Source, ctx.Writer);
            if (ledgerCurrent)
            {
                var current = ctx.Writer.Load<EffectAsset>(output);
                if (current != null)
                {
                    ctx.EffectsConverted = true;
                    return RoResult<UnityEngine.Object>.Skipped(current);
                }
            }

            if (!RoStrReader.TryRead(path, file.ReadAllBytes(), out var str, out var code, out string reason))
                return Fail(ctx, path, code, reason);

            var compiled = RoStrCompiler.Compile(str);

            // Textures first (on demand), then the asset, then the ledger.
            var textures = new Texture2D[compiled.TextureNames.Length];
            var resolved = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
            var missingDependencies = new List<RoLedgerDependency>();
            RoLedgerDependency[] dependencies;
            ctx.BeginDependencies();
            try
            {
                for (int i = 0; i < textures.Length; i++)
                {
                    string name = compiled.TextureNames[i] ?? string.Empty;
                    if (!resolved.TryGetValue(name, out var texture))
                    {
                        texture = TextureOf(ctx, path, name, missingDependencies);
                        resolved.Add(name, texture);
                    }
                    textures[i] = texture;
                }
            }
            finally
            {
                dependencies = ctx.EndDependencies();
            }

            var all = new List<RoLedgerDependency>(dependencies);
            foreach (var missing in missingDependencies)
            {
                bool known = false;
                foreach (var d in all) if (d.Source == missing.Source) { known = true; break; }
                if (!known) all.Add(missing);
            }
            all.Sort((a, b) => string.CompareOrdinal(a.Source, b.Source));

            var entry = ScriptableObject.CreateInstance<EffectAsset>();
            entry.Path = path.Canonical;
            entry.Kind = EffectKind.Str;
            entry.Fps = compiled.Fps;
            entry.FrameCount = compiled.FrameCount;
            entry.Length = compiled.Fps > 0f ? compiled.FrameCount / compiled.Fps : 0f;
            entry.Layers = compiled.Layers;
            entry.Keys = compiled.Keys;
            entry.TextureNames = compiled.TextureNames;
            entry.Textures = textures;

            // In place: the GUID is kept and an unchanged asset is not touched (PLAN-0002 4.7).
            var status = ctx.Writer.UpsertObject(output, entry);
            var asset = ctx.Writer.Load<EffectAsset>(output);
            ctx.EffectsConverted = true;

            ledger.Record(new RoLedgerEntry
            {
                Source = path.Canonical, SourceHash = hash, Rules = Rules, Outputs = new[] { output }, Dependencies = all.ToArray(),
            }, ctx.Report);
            return new RoResult<UnityEngine.Object>(status == RoStatus.Skipped && !ledgerCurrent ? RoStatus.Updated : status, asset);
        }

        /// <summary>The converted texture of a name, or the placeholder (one warning) when it is nowhere; a missing name records the files that would resolve it.</summary>
        static Texture2D TextureOf(RoImportContext ctx, RoPath str, string name, List<RoLedgerDependency> missing)
        {
            if (RoEffectTextureLookup.TryResolve(ctx.Source, str, name, ctx.Report, out var texture))
                return ctx.EffectTexture(texture, str.Canonical).Value;

            foreach (var candidate in Candidates(str, name))
                missing.Add(new RoLedgerDependency(candidate.Canonical, string.Empty));
            return RoPlaceholders.MissingTexture(ctx).Value;
        }

        /// <summary>Every file <see cref="RoEffectTextureLookup.TryResolve"/> looks at for a name.</summary>
        static IEnumerable<RoPath> Candidates(RoPath str, string name)
        {
            var found = new List<RoPath>();
            if (string.IsNullOrWhiteSpace(name)) return found;
            foreach (var folder in new[] { str.Folder, RoEffectTextureLookup.EffectRoot })
            {
                try
                {
                    var candidate = folder.Combine(name);
                    if (!string.IsNullOrEmpty(candidate.Extension)) found.Add(candidate);
                    else foreach (string ext in RoEffectTextureLookup.BareNamePreference) found.Add(candidate.WithExtension(ext));
                }
                catch (RoPathException) { }
            }
            return found;
        }

        static RoResult<UnityEngine.Object> Fail(RoImportContext ctx, RoPath path, RoIssueCode code, string message)
        {
            // The batch reports a failed job as one issue (RoBatch); a warning here would make it two.
            return RoResult<UnityEngine.Object>.Failed(new RoIssue(code, path.Canonical, null, message));
        }
    }
}
