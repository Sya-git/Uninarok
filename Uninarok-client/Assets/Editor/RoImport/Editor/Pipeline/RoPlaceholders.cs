using UnityEditor;
using UnityEngine;
using Uninarok.RoImport.Core.Imaging;
using Uninarok.RoImport.Core.Materials;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Editor.Ledger;
using Uninarok.RoImport.Editor.Materials;

namespace Uninarok.RoImport.Editor.Pipeline
{
    /// <summary>
    /// The visible stand-in for a texture that is missing, unsupported or corrupt (plan 4.12; SPEC-0002 FR-8, FR-10): a
    /// 16 x 16 magenta and black checker at <c>Art/_placeholders/missing_texture.png</c>, with its own opaque material.
    /// Created on first need through the normal writer, from deterministic bytes, and owned by the <c>placeholder</c> ledger.
    /// </summary>
    public static class RoPlaceholders
    {
        public const string TextureName = "missing_texture.png";
        public const string MaterialName = "missing_texture.mat";
        public const string Category = "placeholder";
        public const int Size = 16, Cell = 4;
        public const int Rules = 1;

        static readonly Color32 Magenta = new Color32(255, 0, 255, 255);
        static readonly Color32 Black = new Color32(0, 0, 0, 255);

        /// <summary>The checker image: <see cref="Cell"/>-pixel squares, magenta in the top-left corner.</summary>
        public static RoImage CheckerImage()
        {
            var image = new RoImage(Size, Size);
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
                image.SetPixel(x, y, ((x / Cell) + (y / Cell)) % 2 == 0 ? Magenta : Black);
            return image;
        }

        public static RoResult<Texture2D> MissingTexture(RoImportContext ctx)
        {
            const string memo = "placeholder:texture";
            if (ctx.TryGetMemo(memo, out Texture2D done)) return RoResult<Texture2D>.Skipped(done);

            string path = RoOutputPaths.Placeholder(ctx.Roots, TextureName);
            var status = ctx.Writer.WriteBytes(path, ctx.Png.Encode(CheckerImage()), Configure);
            var texture = ctx.Writer.Load<Texture2D>(path);
            Own(ctx, "_placeholders/missing_texture", path);
            ctx.Memo(memo, texture);
            return new RoResult<Texture2D>(status, texture);
        }

        /// <summary>An opaque, one-sided material showing the checker.</summary>
        public static RoResult<Material> MissingMaterial(RoImportContext ctx)
        {
            const string memo = "placeholder:material";
            if (ctx.TryGetMemo(memo, out Material done)) return RoResult<Material>.Skipped(done);

            var texture = MissingTexture(ctx).Value;
            var shader = Shader.Find(RoMaterialFactory.ShaderName);
            if (shader == null)
                return RoResult<Material>.Failed(new RoIssue(RoIssueCode.ShaderMissing, MaterialName, null, $"shader '{RoMaterialFactory.ShaderName}' not found"));

            string path = RoOutputPaths.Placeholder(ctx.Roots, MaterialName);
            var fresh = new Material(shader);
            RoMaterialFactory.Setup(fresh, RoSurface.Opaque, false, texture);
            var status = ctx.Writer.UpsertObject(path, fresh);
            var material = ctx.Writer.Load<Material>(path);
            Own(ctx, "_placeholders/missing_texture_material", path);
            ctx.Memo(memo, material);
            return new RoResult<Material>(status, material);
        }

        // Point filtering keeps the checker crisp; otherwise default World-like settings.
        static void Configure(AssetImporter importer)
        {
            var t = (TextureImporter)importer;
            t.textureType = TextureImporterType.Default;
            t.filterMode = FilterMode.Point;
            t.wrapMode = TextureWrapMode.Repeat;
            t.mipmapEnabled = false;
            t.isReadable = false;
        }

        // The placeholder is generated output, so the ledger owns it (D14); it is never a dependency (plan 4.12).
        static void Own(RoImportContext ctx, string id, string path) =>
            ctx.Ledger(Category).Record(new RoLedgerEntry { Source = RoPath.Parse(id).Canonical, SourceHash = string.Empty, Rules = Rules, Outputs = new[] { path } }, ctx.Report);
    }
}
