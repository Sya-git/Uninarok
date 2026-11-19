using System;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Paths;

namespace Uninarok.RoImport.Editor.Sprites
{
    /// <summary>
    /// Where sprite outputs go (PLAN-0007 §3). Sheets are named by content, because one sheet serves every SPR with the
    /// same bytes (FR-2): <c>Art/sprite/_sheets/&lt;h2&gt;/&lt;name&gt;_&lt;h8&gt;.png</c>. Animations mirror their ACT,
    /// palettes their PAL.
    /// </summary>
    public static class RoSpriteOutputPaths
    {
        public const string SheetsFolder = "sprite/_sheets";
        public const string IndexSuffix = "_index";
        public const string DefaultMaterialName = "sprite_default.mat";

        /// <summary>The sheet of an SPR: <paramref name="contentHash"/> is its SHA-256 (hex), <paramref name="spr"/> the SPR that first produced it.</summary>
        public static string Sheet(RoOutputRoots roots, RoPath spr, string contentHash)
        {
            if (roots == null) throw new ArgumentNullException(nameof(roots));
            if (spr.IsEmpty) throw new ArgumentException("A sheet needs the SPR it comes from.", nameof(spr));
            if (string.IsNullOrEmpty(contentHash) || contentHash.Length < 8) throw new ArgumentException("A sheet needs the SPR's content hash.", nameof(contentHash));
            string h = contentHash.ToLowerInvariant();
            return roots.Art.TrimEnd('/') + "/" + SheetsFolder + "/" + h.Substring(0, 2) + "/" + spr.FileNameWithoutExtension + "_" + h.Substring(0, 8) + ".png";
        }

        /// <summary>The index texture beside a sheet: <c>&lt;sheet&gt;_index.png</c>.</summary>
        public static string IndexOf(string sheetPath)
        {
            if (string.IsNullOrEmpty(sheetPath) || !sheetPath.EndsWith(".png", StringComparison.Ordinal))
                throw new ArgumentException("Not a sheet path.", nameof(sheetPath));
            return sheetPath.Substring(0, sheetPath.Length - 4) + IndexSuffix + ".png";
        }

        /// <summary>The animation of an ACT: <c>Art/sprite/…/&lt;name&gt;.asset</c>.</summary>
        public static string Animation(RoOutputRoots roots, RoPath act) => RoOutputPaths.Mirror(roots, act, ".asset");

        /// <summary>The texture of a PAL: <c>Art/palette/…/&lt;name&gt;.png</c>.</summary>
        public static string Palette(RoOutputRoots roots, RoPath pal) => RoOutputPaths.Mirror(roots, pal, ".png");

        /// <summary>The shared sprite material (<c>Uninarok/RoSprite</c>): <c>Art/_materials/sprite_default.mat</c>.</summary>
        public static string DefaultMaterial(RoOutputRoots roots) =>
            roots.Art.TrimEnd('/') + "/" + RoOutputPaths.MaterialsFolder + "/" + DefaultMaterialName;
    }
}
