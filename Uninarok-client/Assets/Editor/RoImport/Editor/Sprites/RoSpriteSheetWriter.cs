using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using Uninarok.RoImport.Core.Imaging;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Sprites;
using Uninarok.RoImport.Editor.Writing;

namespace Uninarok.RoImport.Editor.Sprites
{
    /// <summary>An extra sprite cut from a sheet: a frame's rect with its pivot moved (plan 4.4, static sprites with an offset).</summary>
    public readonly struct RoSheetSprite
    {
        public RoSheetSprite(string name, int slot, Vector2 pivot)
        {
            Name = name;
            Slot = slot;
            Pivot = pivot;
        }

        public string Name { get; }
        public int Slot { get; }
        /// <summary>Normalised pivot (Unity convention: 0, 0 is the bottom-left of the frame).</summary>
        public Vector2 Pivot { get; }
    }

    /// <summary>
    /// Writes a sheet and slices it (PLAN-0007 4.2, SPEC-0007 FR-1, FR-7, FR-9), with the calls verified by TASKS-0007 T0.3:
    /// the main PNG (RGBA32, point, clamp, no mipmaps, sRGB, uncompressed, sprite mode Multiple), an optional R8 index PNG
    /// attached as the secondary texture <c>_IndexTex</c>, and one sprite per slot named <c>f&lt;slot&gt;</c>, pivot at the
    /// frame centre, full-rect mesh. Sprite ids are derived from the names, so references survive a re-import. Identical
    /// content is not rewritten, so a second write changes no file.
    /// </summary>
    public static class RoSpriteSheetWriter
    {
        public const string IndexTextureName = "_IndexTex";

        /// <summary>Version of the sheet rules (packer, pixels, settings); a sheet written with another version is written again.</summary>
        public const int Rules = 1;

        /// <summary>The name of a static sprite moved by its layer offset (plan 4.4): <c>f&lt;slot&gt;@&lt;x&gt;_&lt;y&gt;</c>.</summary>
        public static string OffsetSpriteName(int slot, int offsetX, int offsetY) => SpriteName(slot) + "@" + offsetX + "_" + offsetY;

        public static string SpriteName(int slot) => "f" + slot;

        /// <summary>A stable sprite id from its name (T0.3): the MD5 of <c>uninarok-sprite:&lt;name&gt;</c>.</summary>
        public static GUID SpriteId(string name)
        {
            using (var md5 = MD5.Create())
            {
                var hash = md5.ComputeHash(Encoding.UTF8.GetBytes("uninarok-sprite:" + name));
                var hex = new StringBuilder(32);
                foreach (byte b in hash) hex.Append(b.ToString("x2"));
                return new GUID(hex.ToString());
            }
        }

        /// <summary>
        /// Writes the sheet of <paramref name="spr"/> at <paramref name="sheetPath"/> (and its index texture when
        /// <paramref name="withIndex"/>) and slices it. <paramref name="extra"/> adds sprites with moved pivots.
        /// </summary>
        public static RoStatus Write(RoAssetWriter writer, IRoPngCodec png, string sheetPath, RoSpr spr, RoSheetLayout layout,
                                     float pixelsPerUnit, bool withIndex, IReadOnlyList<RoSheetSprite> extra = null)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            if (png == null) throw new ArgumentNullException(nameof(png));
            if (pixelsPerUnit <= 0f) throw new ArgumentOutOfRangeException(nameof(pixelsPerUnit));

            var status = RoStatus.Skipped;
            Texture2D index = null;
            if (withIndex)
            {
                string indexPath = RoSpriteOutputPaths.IndexOf(sheetPath);
                status = Combine(status, writer.WriteBytes(indexPath, EncodeR8(RoSpriteSheetPixels.Index(spr, layout), layout.Width, layout.Height), ConfigureIndex));
                index = writer.Load<Texture2D>(indexPath);
            }

            var image = new RoImage { Width = layout.Width, Height = layout.Height, Rgba32 = RoSpriteSheetPixels.Color(spr, layout), SourceHadAlpha = true };
            status = Combine(status, writer.WriteBytes(sheetPath, png.Encode(image), importer => ConfigureMain(importer, pixelsPerUnit)));

            // Offset sprites other ACTs added to this shared sheet stay (D14).
            var wanted = new List<RoSheetSprite>(extra ?? Array.Empty<RoSheetSprite>());
            foreach (var kept in ExistingExtras(sheetPath))
                if (kept.Slot < spr.SlotCount && wanted.All(w => w.Name != kept.Name)) wanted.Add(kept);

            var rects = Rects(spr, layout, wanted);
            if (Slice(sheetPath, rects, index) && status == RoStatus.Skipped) status = RoStatus.Updated;
            return status;
        }

        /// <summary>Adds one offset sprite to an existing sheet if it has none of that name; true when the sheet changed.</summary>
        public static bool EnsureSprite(string sheetPath, RoSheetLayout layout, RoSheetSprite extra)
        {
            var provider = Provider(sheetPath);
            var rects = provider.GetSpriteRects();
            if (rects.Any(r => r.name == extra.Name)) return false;
            var secondary = provider.GetDataProvider<ISecondaryTextureDataProvider>().textures;
            var index = secondary != null && secondary.Length > 0 ? secondary[0].texture : null;
            return Slice(sheetPath, rects.Concat(new[] { Rect(extra.Name, layout.Rects[extra.Slot], layout, extra.Pivot) }).ToArray(), index);
        }

        /// <summary>The offset sprites (<c>f&lt;slot&gt;@…</c>) a sheet already has.</summary>
        static IEnumerable<RoSheetSprite> ExistingExtras(string sheetPath)
        {
            if (!(AssetImporter.GetAtPath(sheetPath) is TextureImporter)) yield break;
            foreach (var r in Provider(sheetPath).GetSpriteRects())
            {
                int at = r.name.IndexOf('@');
                if (at > 1 && r.name[0] == 'f' && int.TryParse(r.name.Substring(1, at - 1), out int slot))
                    yield return new RoSheetSprite(r.name, slot, r.pivot);
            }
        }

        /// <summary>Attaches an index texture to a sheet that has none yet (plan 4.2: palettes requested later).</summary>
        public static RoStatus AddIndex(RoAssetWriter writer, string sheetPath, RoSpr spr, RoSheetLayout layout)
        {
            string indexPath = RoSpriteOutputPaths.IndexOf(sheetPath);
            var status = writer.WriteBytes(indexPath, EncodeR8(RoSpriteSheetPixels.Index(spr, layout), layout.Width, layout.Height), ConfigureIndex);
            var provider = Provider(sheetPath);
            var rects = provider.GetSpriteRects();
            return Slice(sheetPath, rects, writer.Load<Texture2D>(indexPath)) || status != RoStatus.Skipped ? RoStatus.Updated : RoStatus.Skipped;
        }

        /// <summary>The sheet's sprites by slot (<c>f&lt;slot&gt;</c>); a slot without a sprite (zero-size frame) is null.</summary>
        public static Sprite[] LoadSprites(string sheetPath, int slotCount)
        {
            var byName = AssetDatabase.LoadAllAssetsAtPath(sheetPath).OfType<Sprite>().ToDictionary(s => s.name, StringComparer.Ordinal);
            var sprites = new Sprite[slotCount];
            for (int i = 0; i < slotCount; i++)
                sprites[i] = byName.TryGetValue(SpriteName(i), out var s) ? s : null;
            return sprites;
        }

        /// <summary>One sprite of a sheet by name, or null.</summary>
        public static Sprite LoadSprite(string sheetPath, string name) =>
            AssetDatabase.LoadAllAssetsAtPath(sheetPath).OfType<Sprite>().FirstOrDefault(s => s.name == name);

        static SpriteRect[] Rects(RoSpr spr, RoSheetLayout layout, IReadOnlyList<RoSheetSprite> extra)
        {
            var list = new List<SpriteRect>();
            for (int slot = 0; slot < spr.SlotCount; slot++)
            {
                var r = layout.Rects[slot];
                if (r.width == 0 || r.height == 0) continue;
                list.Add(Rect(SpriteName(slot), r, layout, new Vector2(0.5f, 0.5f)));
            }
            if (extra != null)
                foreach (var e in extra)
                    list.Add(Rect(e.Name, layout.Rects[e.Slot], layout, e.Pivot));
            return list.ToArray();
        }

        static SpriteRect Rect(string name, RectInt r, RoSheetLayout layout, Vector2 pivot) => new SpriteRect
        {
            name = name,
            // The layout is top-left based (rows top first); Unity's sprite rects are bottom-left based.
            rect = new Rect(r.x, layout.Height - r.y - r.height, r.width, r.height),
            alignment = pivot == new Vector2(0.5f, 0.5f) ? SpriteAlignment.Center : SpriteAlignment.Custom,
            pivot = pivot,
            border = Vector4.zero,
            spriteID = SpriteId(name)
        };

        /// <summary>Applies the sprite rects and the secondary texture when they differ; true when the sheet was re-imported.</summary>
        static bool Slice(string sheetPath, SpriteRect[] rects, Texture2D index)
        {
            var provider = Provider(sheetPath);
            var secondary = provider.GetDataProvider<ISecondaryTextureDataProvider>();
            var wantedSecondary = index != null
                ? new[] { new SecondarySpriteTexture { name = IndexTextureName, texture = index } }
                : Array.Empty<SecondarySpriteTexture>();

            if (Same(provider.GetSpriteRects(), rects) && Same(secondary.textures, wantedSecondary)) return false;

            provider.SetSpriteRects(rects);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
            secondary.textures = wantedSecondary;
            provider.Apply();
            ((AssetImporter)provider.targetObject).SaveAndReimport();
            return true;
        }

        static ISpriteEditorDataProvider Provider(string sheetPath)
        {
            var importer = AssetImporter.GetAtPath(sheetPath) as TextureImporter
                           ?? throw new InvalidOperationException($"'{sheetPath}' has no texture importer");
            var factories = new SpriteDataProviderFactories();
            factories.Init();
            var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            return provider;
        }

        static bool Same(SpriteRect[] a, SpriteRect[] b)
        {
            if (a == null || a.Length != b.Length) return false;
            var byName = a.ToDictionary(r => r.name, StringComparer.Ordinal);
            foreach (var r in b)
            {
                if (!byName.TryGetValue(r.name, out var o)) return false;
                if (o.rect != r.rect || o.spriteID != r.spriteID || o.alignment != r.alignment || o.border != r.border) return false;
                if (r.alignment == SpriteAlignment.Custom && o.pivot != r.pivot) return false;
            }
            return true;
        }

        static bool Same(SecondarySpriteTexture[] a, SecondarySpriteTexture[] b)
        {
            a = a ?? Array.Empty<SecondarySpriteTexture>();
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i].name != b[i].name || a[i].texture != b[i].texture) return false;
            return true;
        }

        static void ConfigureMain(AssetImporter importer, float pixelsPerUnit)
        {
            var t = (TextureImporter)importer;
            t.textureType = TextureImporterType.Sprite;
            t.spriteImportMode = SpriteImportMode.Multiple;
            t.spritePixelsPerUnit = pixelsPerUnit;
            t.filterMode = FilterMode.Point;
            t.mipmapEnabled = false;
            t.wrapMode = TextureWrapMode.Clamp;
            t.sRGBTexture = true;
            t.alphaSource = TextureImporterAlphaSource.FromInput;
            t.alphaIsTransparency = true;
            t.npotScale = TextureImporterNPOTScale.None;
            t.isReadable = false;
            t.maxTextureSize = RoSpriteSheetPacker.MaxSize;
            t.textureCompression = TextureImporterCompression.Uncompressed;

            var settings = new TextureImporterSettings();
            t.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteGenerateFallbackPhysicsOutline = false;
            settings.spriteExtrude = 0;
            t.SetTextureSettings(settings);

            // T0.3: with only "uncompressed", an opaque sheet imports as RGB24; RGBA32 is set explicitly (D6).
            var platform = t.GetDefaultPlatformTextureSettings();
            platform.format = TextureImporterFormat.RGBA32;
            platform.textureCompression = TextureImporterCompression.Uncompressed;
            platform.maxTextureSize = RoSpriteSheetPacker.MaxSize;
            t.SetPlatformTextureSettings(platform);
        }

        static void ConfigureIndex(AssetImporter importer)
        {
            var t = (TextureImporter)importer;
            t.textureType = TextureImporterType.Default;
            t.sRGBTexture = false;
            t.alphaSource = TextureImporterAlphaSource.None;
            t.mipmapEnabled = false;
            t.filterMode = FilterMode.Point;
            t.wrapMode = TextureWrapMode.Clamp;
            t.npotScale = TextureImporterNPOTScale.None;
            t.isReadable = false;
            t.maxTextureSize = RoSpriteSheetPacker.MaxSize;
            t.textureCompression = TextureImporterCompression.Uncompressed;

            var platform = t.GetDefaultPlatformTextureSettings();
            platform.format = TextureImporterFormat.R8;
            platform.textureCompression = TextureImporterCompression.Uncompressed;
            platform.maxTextureSize = RoSpriteSheetPacker.MaxSize;
            t.SetPlatformTextureSettings(platform);
        }

        /// <summary>Encodes R8 pixels (top row first) as an 8-bit grey PNG (T0.3). Main thread only.</summary>
        public static byte[] EncodeR8(byte[] topFirst, int width, int height)
        {
            var flipped = new byte[width * height];
            for (int y = 0; y < height; y++)
                Buffer.BlockCopy(topFirst, y * width, flipped, (height - 1 - y) * width, width);
            var texture = new Texture2D(width, height, TextureFormat.R8, false, true);
            try
            {
                texture.SetPixelData(flipped, 0);
                texture.Apply(false);
                return texture.EncodeToPNG();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        static RoStatus Combine(RoStatus a, RoStatus b)
        {
            if (a == RoStatus.Created || b == RoStatus.Created) return RoStatus.Created;
            if (a == RoStatus.Updated || b == RoStatus.Updated) return RoStatus.Updated;
            return RoStatus.Skipped;
        }
    }
}
