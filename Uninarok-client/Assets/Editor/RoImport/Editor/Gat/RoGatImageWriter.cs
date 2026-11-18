using UnityEditor;
using UnityEngine;
using Uninarok.RoImport.Core.Imaging;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Editor.Pipeline;

namespace Uninarok.RoImport.Editor.Gat
{
    /// <summary>Writes the optional cell image (PLAN-0005 4.5, FR-8): PNG, point filtered, uncompressed, not readable.</summary>
    public static class RoGatImageWriter
    {
        public static RoStatus Write(RoImportContext ctx, string projectPath, RoImage image) =>
            ctx.Writer.WriteBytes(projectPath, ctx.Png.Encode(image), Configure);

        static void Configure(AssetImporter importer)
        {
            var t = (TextureImporter)importer;
            t.textureType = TextureImporterType.Default;
            t.npotScale = TextureImporterNPOTScale.None;
            t.mipmapEnabled = false;
            t.filterMode = FilterMode.Point;
            t.wrapMode = TextureWrapMode.Clamp;
            t.textureCompression = TextureImporterCompression.Uncompressed;
            t.isReadable = false;
        }
    }
}
