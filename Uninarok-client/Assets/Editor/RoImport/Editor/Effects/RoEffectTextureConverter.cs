using System;
using UnityEditor;
using UnityEngine;
using Uninarok.RoImport.Core.Effects;
using Uninarok.RoImport.Core.Formats;
using Uninarok.RoImport.Core.Imaging;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Editor.Ledger;
using Uninarok.RoImport.Editor.Pipeline;

namespace Uninarok.RoImport.Editor.Effects
{
    /// <summary>
    /// Effect texture (BMP, TGA, PNG, JPG under <c>texture/effect/</c> or an STR's own folder) to a PNG at
    /// <c>Art/effect/&lt;canonical path&gt;.png</c>, the original extension kept (PLAN-0012 4.3, D7; SPEC-0012 FR-2). Own
    /// ledger category, so <c>pok1.tga</c> and <c>pok1.bmp</c> are two textures with no collision. The colour key comes from
    /// <see cref="RoEffectKeyRules"/> and is stored in the importer <c>userData</c> (<c>alpha=…;key=…</c>); a texture whose
    /// stored key differs from the rules' is converted again, the others stay skipped. Reached through
    /// <see cref="RoImportContext.EffectTexture"/>, which memoizes it.
    /// </summary>
    public static class RoEffectTextureConverter
    {
        public const string Category = "effecttexture";

        /// <summary>Rules version: bump when decoding, keying, bleeding, encoding or the import settings change.</summary>
        public const int Rules = 1;

        /// <summary>The <c>userData</c> prefix of the key.</summary>
        public const string KeyUserData = ";key=";

        /// <summary>The key recorded on a converted effect texture, or null if it has none.</summary>
        public static RoEffectKey? KeyOf(string projectPath)
        {
            string data = AssetImporter.GetAtPath(projectPath)?.userData;
            if (data == null) return null;
            int at = data.IndexOf(KeyUserData, StringComparison.Ordinal);
            if (at < 0) return null;
            return Enum.TryParse(data.Substring(at + KeyUserData.Length), out RoEffectKey key) ? key : (RoEffectKey?)null;
        }

        internal static RoResult<Texture2D> ConvertTexture(RoImportContext ctx, RoPath path, string referrer, out string hash)
        {
            hash = string.Empty;
            string item = path.Canonical;

            if (!RoImageReader.IsSupportedExtension(path.Extension))
                return Placeholder(ctx, RoIssueCode.UnsupportedTextureFormat, item, referrer,
                    $"'{path.Extension}' is not a supported texture format (BMP, PNG, TGA)");

            if (!ctx.Source.TryGetFile(path, out var file))
                return Placeholder(ctx, RoIssueCode.TextureMissing, item, referrer, "no such file in the source folder");

            hash = file.ContentHash();
            string output = RoEffectOutputPaths.Texture(ctx.Roots, path);
            var ledger = ctx.Ledger(Category);

            string owner = ledger.OwnerOf(output);
            if (owner != null && owner != item)
                return Placeholder(ctx, RoIssueCode.OutputCollision, item, referrer,
                    $"output '{output}' is already produced by '{owner}'; this file is skipped");

            var rules = ctx.EffectKeyRules;
            byte[] bytes = null;
            RoImage image = null;
            string reason = null;

            if (!ctx.Force && ledger.IsCurrent(path, hash, Rules, ctx.Source, ctx.Writer))
            {
                var current = ctx.Writer.Load<Texture2D>(output);
                var stored = current != null ? KeyOf(output) : null;
                if (stored != null)
                {
                    // The key depends on whether the source had alpha, known only by decoding; decode only when the rules differ on it.
                    var opaque = rules.For(path, false);
                    var withAlpha = rules.For(path, true);
                    bool matches;
                    if (opaque == withAlpha)
                    {
                        matches = stored.Value == opaque;
                    }
                    else
                    {
                        bytes = file.ReadAllBytes();
                        if (!RoImageReader.TryRead(path, bytes, ctx.Png, out image, out reason))
                            return Placeholder(ctx, RoIssueCode.CorruptFile, item, referrer, reason);
                        matches = stored.Value == rules.For(path, image.SourceHadAlpha);
                    }
                    if (matches) return RoResult<Texture2D>.Skipped(current);
                }
            }

            if (image == null)
            {
                bytes = file.ReadAllBytes();
                if (!RoImageReader.TryRead(path, bytes, ctx.Png, out image, out reason))
                    return Placeholder(ctx, RoIssueCode.CorruptFile, item, referrer, reason);
            }

            var keys = rules.KeySetFor(path, image.SourceHadAlpha);
            var alpha = RoColorKey.Apply(image, keys);
            RoColorKey.BleedTransparentColor(image);

            var status = ctx.Writer.WriteBytes(output, ctx.Png.Encode(image), importer => Configure(importer, alpha, keys.Key));
            ledger.Record(new RoLedgerEntry { Source = item, SourceHash = hash, Rules = Rules, Outputs = new[] { output } }, ctx.Report);

            return new RoResult<Texture2D>(status, ctx.Writer.Load<Texture2D>(output));
        }

        static RoResult<Texture2D> Placeholder(RoImportContext ctx, RoIssueCode code, string item, string referrer, string message)
        {
            ctx.Report.Warn(code, item, referrer, message);
            var placeholder = RoPlaceholders.MissingTexture(ctx).Value;
            return RoResult<Texture2D>.Failed(new RoIssue(code, item, referrer, message), placeholder);
        }

        /// <summary>Import settings of an effect texture (plan 4.3): Default type, sRGB, alpha is transparency, mipmaps, bilinear, clamp, compressed, size kept.</summary>
        internal static void Configure(AssetImporter importer, RoTextureAlpha alpha, RoEffectKey key)
        {
            var t = (TextureImporter)importer;
            t.textureType = TextureImporterType.Default;
            t.sRGBTexture = true;
            t.alphaSource = TextureImporterAlphaSource.FromInput;
            t.alphaIsTransparency = true;
            t.isReadable = false;
            t.npotScale = TextureImporterNPOTScale.None;
            t.mipmapEnabled = true;
            t.mipMapsPreserveCoverage = alpha == RoTextureAlpha.Keyed;
            t.alphaTestReferenceValue = 0.5f;
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            t.textureCompression = TextureImporterCompression.Compressed;
            t.userData = RoTextureConverter.AlphaUserData + alpha + KeyUserData + key;
        }
    }
}
