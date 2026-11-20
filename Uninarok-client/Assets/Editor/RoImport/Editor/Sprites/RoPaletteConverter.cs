using System;
using UnityEditor;
using UnityEngine;
using Uninarok.RoImport.Core.Formats;
using Uninarok.RoImport.Core.Imaging;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Sprites;
using Uninarok.RoImport.Editor.Ledger;
using Uninarok.RoImport.Editor.Pipeline;
using Object = UnityEngine.Object;

namespace Uninarok.RoImport.Editor.Sprites
{
    /// <summary>
    /// A PAL file to a 256 × 1 palette texture (PLAN-0007 4.4, 4.7; SPEC-0007 FR-7): point, clamp, no mipmaps, uncompressed,
    /// sRGB, entry 0 transparent by rule. <c>SpritePalette</c> hands it to the shader <c>Uninarok/RoSprite</c>. Written in
    /// place (GUID kept); ledger category <see cref="Category"/>.
    /// </summary>
    public sealed class RoPaletteConverter : IRoConverter
    {
        public const string Category = "palette";
        public const int RulesVersion = 1;

        public RoJobKind Kind => RoJobKind.Palette;

        /// <summary><c>.pal</c> files.</summary>
        public bool Handles(RoPath path) => path.Extension == ".pal";

        public RoResult<Object> Convert(RoImportContext ctx, RoPath path)
        {
            var result = ConvertPalette(ctx, path);
            return new RoResult<Object>(result.Status, result.Value, result.Failure);
        }

        public static RoResult<Texture2D> ConvertPalette(RoImportContext ctx, RoPath path)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            string output = RoSpriteOutputPaths.Palette(ctx.Roots, path);
            if (!ctx.Source.TryGetFile(path, out var file))
                return Fail(ctx, path, RoIssueCode.SourceMissing, "no such file in the source folder");

            string hash = file.ContentHash();
            var ledger = ctx.Ledger(Category);
            if (!ctx.Force && ledger.IsCurrent(path, hash, RulesVersion, ctx.Source, ctx.Writer))
            {
                var current = ctx.Writer.Load<Texture2D>(output);
                if (current != null) return RoResult<Texture2D>.Skipped(current);
            }

            if (!RoPalReader.TryRead(path, file.ReadAllBytes(), out var pal, out string reason))
                return Fail(ctx, path, RoIssueCode.CorruptFile, reason);

            try
            {
                var status = ctx.Writer.WriteBytes(output, ctx.Png.Encode(Image(pal)), Configure);
                var texture = ctx.Writer.Load<Texture2D>(output);
                ledger.Record(new RoLedgerEntry { Source = path.Canonical, SourceHash = hash, Rules = RulesVersion, Outputs = new[] { output } }, ctx.Report);
                return new RoResult<Texture2D>(status, texture);
            }
            catch (Exception e)
            {
                ledger.Forget(path);
                return Fail(ctx, path, RoIssueCode.Unexpected, $"writing the palette failed: {e.GetType().Name}: {e.Message}");
            }
        }

        /// <summary>The 256 × 1 image of a palette: entry i at x = i, entry 0 fully transparent (plan 4.3).</summary>
        public static RoImage Image(RoPal pal)
        {
            var image = new RoImage(RoPal.Size, 1, sourceHadAlpha: true);
            for (int i = 0; i < RoPal.Size; i++)
            {
                var c = pal.Colors[i];
                image.SetPixel(i, 0, i == 0 ? new Color32(c.r, c.g, c.b, 0) : new Color32(c.r, c.g, c.b, 255));
            }
            return image;
        }

        static void Configure(AssetImporter importer)
        {
            var t = (TextureImporter)importer;
            t.textureType = TextureImporterType.Default;
            t.sRGBTexture = true;
            t.alphaSource = TextureImporterAlphaSource.FromInput;
            t.alphaIsTransparency = false;
            t.mipmapEnabled = false;
            t.filterMode = FilterMode.Point;
            t.wrapMode = TextureWrapMode.Clamp;
            t.npotScale = TextureImporterNPOTScale.None;
            t.isReadable = false;
            t.textureCompression = TextureImporterCompression.Uncompressed;
            var platform = t.GetDefaultPlatformTextureSettings();
            platform.format = TextureImporterFormat.RGBA32;
            platform.textureCompression = TextureImporterCompression.Uncompressed;
            t.SetPlatformTextureSettings(platform);
        }

        static RoResult<Texture2D> Fail(RoImportContext ctx, RoPath path, RoIssueCode code, string message)
        {
            ctx.Report.Warn(code, path.Canonical, null, message);
            return RoResult<Texture2D>.Failed(new RoIssue(code, path.Canonical, null, message));
        }
    }
}
