using System;
using UnityEditor;
using UnityEngine;
using Uninarok.RoImport.Core.Effects;
using Uninarok.RoImport.Core.Formats;
using Uninarok.RoImport.Core.Imaging;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Editor.Ledger;

namespace Uninarok.RoImport.Editor.Pipeline
{
    /// <summary>
    /// Source texture (BMP, TGA, PNG) to a PNG asset at the mirrored path (plan 4.4, 4.5, D7, D8; SPEC-0002 FR-8, FR-5,
    /// FR-10): decode through <see cref="RoImageReader"/>, key and bleed with <see cref="RoColorKey"/>, encode, write with the
    /// import settings of its usage, record in the <c>texture</c> ledger. A missing, unsupported or corrupt source, or an
    /// output owned by another source, gives one warning and a <see cref="RoStatus.Failed"/> result whose value is the
    /// placeholder, so whatever needed the texture still converts.
    /// </summary>
    public sealed class RoTextureConverter : IRoConverter
    {
        public const string Category = "texture";

        /// <summary>Ledger category of the ground's Clamp copies (PLAN-0004 D17).</summary>
        public const string GroundCategory = "groundtexture";

        /// <summary>Rules version: bump when decoding, keying, bleeding, encoding or the import settings change (plan 4.8).</summary>
        public const int Rules = 2;   // 2: World textures keep their original size (npotScale none, D20)

        /// <summary>Prefix of the importer <c>userData</c> that stores the texture's <see cref="RoTextureAlpha"/>.</summary>
        public const string AlphaUserData = "alpha=";

        public RoJobKind Kind => RoJobKind.Texture;

        /// <summary>Canonical folder prefix of effect textures, which belong to the effect category, not here (plan 7, D7, FR-2).</summary>
        static readonly string EffectFolderPrefix = RoEffectTextureLookup.EffectRoot.Canonical + "/";

        /// <summary>BMP, PNG, JPG and TGA files outside <c>texture/effect/</c>.</summary>
        public bool Handles(RoPath path) =>
            RoImageReader.IsSupportedExtension(path.Extension)
            && !path.Canonical.StartsWith(EffectFolderPrefix, StringComparison.OrdinalIgnoreCase);

        public RoResult<UnityEngine.Object> Convert(RoImportContext ctx, RoPath path)
        {
            var result = ctx.Texture(path);
            return new RoResult<UnityEngine.Object>(result.Status, result.Value, result.Failure);
        }

        /// <summary>The <see cref="RoTextureAlpha"/> recorded on a converted texture, or null if it has none.</summary>
        public static RoTextureAlpha? AlphaOf(string projectPath)
        {
            string data = AssetImporter.GetAtPath(projectPath)?.userData;
            if (data == null || !data.StartsWith(AlphaUserData, StringComparison.Ordinal)) return null;
            return Enum.TryParse(data.Substring(AlphaUserData.Length), out RoTextureAlpha alpha) ? alpha : (RoTextureAlpha?)null;
        }

        /// <summary>
        /// One texture, converted or skipped. Called through <see cref="RoImportContext.Texture"/>, which memoizes it, or with
        /// <paramref name="ground"/> through <see cref="RoImportContext.GroundTexture"/>: the same image written as the
        /// ground's Clamp copy, in its own ledger (PLAN-0004 D17).
        /// </summary>
        internal static RoResult<Texture2D> ConvertTexture(RoImportContext ctx, RoPath path, string referrer, out string hash, bool ground = false)
        {
            hash = string.Empty;
            string item = path.Canonical;

            if (!RoImageReader.IsSupportedExtension(path.Extension))
                return Placeholder(ctx, RoIssueCode.UnsupportedTextureFormat, item, referrer,
                    $"'{path.Extension}' is not a supported texture format (BMP, PNG, TGA)");

            if (!ctx.Source.TryGetFile(path, out var file))
                return Placeholder(ctx, RoIssueCode.TextureMissing, item, referrer, "no such file in the source folder");

            // The .bmp is preferred when a .tga or .png has the same name and so the same output (plan 4.5, D19).
            bool isBmp = path.Extension == RoImageReader.Bmp;
            var bmp = path.WithExtension(RoImageReader.Bmp);
            if (!isBmp && ctx.Source.TryGetFile(bmp, out _))
            {
                ctx.Report.Warn(RoIssueCode.OutputCollision, item, referrer,
                    $"'{bmp.Canonical}' has the same output and is preferred; it is used instead");
                var preferred = ground ? ctx.GroundTexture(bmp, referrer) : ctx.Texture(bmp, referrer);
                return preferred.Status == RoStatus.Failed ? preferred : RoResult<Texture2D>.Skipped(preferred.Value);
            }

            hash = file.ContentHash();
            string output = ground ? RoOutputPaths.GroundTexture(ctx.Roots, path) : RoOutputPaths.Texture(ctx.Roots, path);
            var ledger = ctx.Ledger(ground ? GroundCategory : Category);

            string owner = ledger.OwnerOf(output);
            if (owner != null && owner != item)
            {
                if (isBmp)
                    ledger.Forget(RoPath.Parse(owner));        // recorded before the .bmp rule: the .bmp takes its output over
                else
                    return Placeholder(ctx, RoIssueCode.OutputCollision, item, referrer,
                        $"output '{output}' is already produced by '{owner}'; this file is skipped");
            }

            if (!ctx.Force && ledger.IsCurrent(path, hash, Rules, ctx.Source, ctx.Writer))
            {
                var current = ctx.Writer.Load<Texture2D>(output);
                if (current != null) return RoResult<Texture2D>.Skipped(current);
            }

            if (!RoImageReader.TryRead(path, file.ReadAllBytes(), ctx.Png, out var image, out string reason))
                return Placeholder(ctx, RoIssueCode.CorruptFile, item, referrer, reason);

            var alpha = RoColorKey.Apply(image);
            RoColorKey.BleedTransparentColor(image);
            var usage = RoTextureUsageRules.For(path);

            var status = ctx.Writer.WriteBytes(output, ctx.Png.Encode(image), importer => Configure(importer, usage, alpha, ground));
            ledger.Record(new RoLedgerEntry { Source = item, SourceHash = hash, Rules = Rules, Outputs = new[] { output } }, ctx.Report);

            var texture = ctx.Writer.Load<Texture2D>(output);
            return new RoResult<Texture2D>(status, texture);
        }

        static RoResult<Texture2D> Placeholder(RoImportContext ctx, RoIssueCode code, string item, string referrer, string message)
        {
            ctx.Report.Warn(code, item, referrer, message);
            var placeholder = RoPlaceholders.MissingTexture(ctx).Value;
            return RoResult<Texture2D>.Failed(new RoIssue(code, item, referrer, message), placeholder);
        }

        /// <summary>
        /// Import settings by usage (plan 4.4 table). Applied by the writer only when they differ (plan 4.7). A ground copy
        /// is a world texture with Clamp wrap (PLAN-0004 D17).
        /// </summary>
        internal static void Configure(AssetImporter importer, RoTextureUsage usage, RoTextureAlpha alpha, bool ground = false)
        {
            var t = (TextureImporter)importer;
            t.sRGBTexture = true;
            t.alphaSource = TextureImporterAlphaSource.FromInput;
            t.alphaIsTransparency = true;
            t.isReadable = false;
            t.userData = AlphaUserData + alpha;

            if (usage == RoTextureUsage.Ui)
            {
                t.textureType = TextureImporterType.Sprite;
                t.spriteImportMode = SpriteImportMode.Single;
                t.mipmapEnabled = false;
                t.mipMapsPreserveCoverage = false;
                t.wrapMode = TextureWrapMode.Clamp;
                t.filterMode = FilterMode.Point;
                t.textureCompression = TextureImporterCompression.Uncompressed;
            }
            else
            {
                t.textureType = TextureImporterType.Default;
                t.npotScale = TextureImporterNPOTScale.None;    // keep the original size (D20)
                t.mipmapEnabled = true;
                t.mipMapsPreserveCoverage = alpha == RoTextureAlpha.Keyed;
                t.alphaTestReferenceValue = 0.5f;            // matches the cutout material's _Cutoff (plan 4.6)
                t.wrapMode = ground ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
                t.filterMode = FilterMode.Bilinear;
                t.textureCompression = TextureImporterCompression.Compressed;
            }
        }
    }
}
