using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Sprites;
using Uninarok.RoImport.Editor.Sprites;
using Uninarok.RoImport.Editor.Writing;

namespace Uninarok.RoImport.Tests.Sprites
{
    // TASKS-0007 T3.3 (FR-1, FR-7, FR-9, plan 4.2): a synthetic sheet written, sliced and read back; a second write changes
    // no file and no sprite id. Writes only under Assets/__RoImportTests.
    public class RoSpriteSheetWriterTests
    {
        const float Ppu = 70f;
        const string Hash = "ab12cd34ef567890ab12cd34ef567890ab12cd34ef567890ab12cd34ef567890";

        RoOutputRoots _roots;
        RoAssetWriter _writer;
        RoPngCodec _png;
        RoSpr _spr;
        RoSheetLayout _layout;
        string _sheet;

        [SetUp]
        public void SetUp()
        {
            _roots = RoImportTestRoot.Create();
            _writer = new RoAssetWriter(_roots);
            _png = new RoPngCodec();
            _spr = SpriteTestData.Mixed().spr;
            var sizes = Enumerable.Range(0, _spr.SlotCount).Select(i => new Vector2Int(_spr.Slot(i).Width, _spr.Slot(i).Height)).ToArray();
            Assert.IsTrue(RoSpriteSheetPacker.TryPack(sizes, out _layout));
            _sheet = RoSpriteOutputPaths.Sheet(_roots, RoPath.Parse("sprite/몬스터/mixed.spr"), Hash);
        }

        [TearDown]
        public void TearDown() => RoImportTestRoot.Delete();

        RoStatus Write(bool index = true) =>
            RoSpriteSheetWriter.Write(_writer, _png, _sheet, _spr, _layout, Ppu, index, new[] { new RoSheetSprite("f0@0_-7", 0, new Vector2(0.5f, (4f - 7f) / 8f)) });

        static byte[] Bytes(string projectPath) => File.ReadAllBytes(RoImportTestRoot.FullPath(projectPath));

        static long FileId(Object o)
        {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o, out string _, out long id);
            return id;
        }

        [Test]
        public void TheSheetPath_IsNamedByContent()
        {
            StringAssert.EndsWith("/Art/sprite/_sheets/ab/mixed_ab12cd34.png", _sheet);
            StringAssert.EndsWith("/Art/sprite/_sheets/ab/mixed_ab12cd34_index.png", RoSpriteOutputPaths.IndexOf(_sheet));
        }

        [Test]
        public void TheSheet_IsAPointFilteredUncompressedSpriteSheet()
        {
            Assert.AreEqual(RoStatus.Created, Write());

            var t = (TextureImporter)AssetImporter.GetAtPath(_sheet);
            Assert.AreEqual((TextureImporterType.Sprite, SpriteImportMode.Multiple), (t.textureType, t.spriteImportMode));
            Assert.AreEqual((FilterMode.Point, false, TextureWrapMode.Clamp, true), (t.filterMode, t.mipmapEnabled, t.wrapMode, t.sRGBTexture));
            Assert.AreEqual(TextureImporterFormat.RGBA32, t.GetDefaultPlatformTextureSettings().format);
            Assert.AreEqual(Ppu, t.spritePixelsPerUnit);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(_sheet);
            Assert.AreEqual(TextureFormat.RGBA32, texture.format);
            Assert.AreEqual((_layout.Width, _layout.Height), (texture.width, texture.height));

            var index = (TextureImporter)AssetImporter.GetAtPath(RoSpriteOutputPaths.IndexOf(_sheet));
            Assert.AreEqual(TextureImporterFormat.R8, index.GetDefaultPlatformTextureSettings().format);
            Assert.IsFalse(index.sRGBTexture);
            Assert.AreEqual(TextureFormat.R8, AssetDatabase.LoadAssetAtPath<Texture2D>(RoSpriteOutputPaths.IndexOf(_sheet)).format);
        }

        [Test]
        public void EverySlot_IsASpriteNamedBySlot_WithItsRect_PivotAtTheCentre_AndTheIndexTexture()
        {
            Write();

            var sprites = RoSpriteSheetWriter.LoadSprites(_sheet, _spr.SlotCount);
            for (int slot = 0; slot < _spr.SlotCount; slot++)
            {
                var s = sprites[slot];
                Assert.IsNotNull(s, "slot " + slot);
                Assert.AreEqual("f" + slot, s.name);
                var r = _layout.Rects[slot];
                Assert.AreEqual(new Rect(r.x, _layout.Height - r.y - r.height, r.width, r.height), s.rect, "rect, bottom-left based");
                Assert.AreEqual(new Vector2(r.width / 2f, r.height / 2f), s.pivot, "pivot at the centre, in pixels");
                Assert.AreEqual(Ppu, s.pixelsPerUnit);
                var secondary = new SecondarySpriteTexture[s.GetSecondaryTextureCount()];
                s.GetSecondaryTextures(secondary);
                Assert.AreEqual(1, secondary.Length);
                Assert.AreEqual("_IndexTex", secondary[0].name);
                Assert.AreEqual(Path.GetFileNameWithoutExtension(RoSpriteOutputPaths.IndexOf(_sheet)), secondary[0].texture.name);
            }

            var extra = RoSpriteSheetWriter.LoadSprite(_sheet, "f0@0_-7");
            Assert.IsNotNull(extra);
            Assert.AreEqual(sprites[0].rect, extra.rect);
            Assert.AreEqual(4f - 7f, extra.pivot.y, 1e-4f, "pivot moved by the offset");
        }

        [Test]
        public void ThePixels_AreTheSheetPixels()
        {
            Write();

            Assert.IsTrue(_png.TryDecode(Bytes(_sheet), out var decoded));
            CollectionAssert.AreEqual(RoSpriteSheetPixels.Color(_spr, _layout), decoded.Rgba32);
        }

        [Test]
        public void ASecondWrite_ChangesNoFileAndNoSpriteId()
        {
            Write();
            var sheetBytes = Bytes(_sheet);
            var indexBytes = Bytes(RoSpriteOutputPaths.IndexOf(_sheet));
            var meta = File.ReadAllText(RoImportTestRoot.FullPath(_sheet) + ".meta");
            var ids = RoSpriteSheetWriter.LoadSprites(_sheet, _spr.SlotCount).Select(FileId).ToArray();

            Assert.AreEqual(RoStatus.Skipped, Write());

            CollectionAssert.AreEqual(sheetBytes, Bytes(_sheet));
            CollectionAssert.AreEqual(indexBytes, Bytes(RoSpriteOutputPaths.IndexOf(_sheet)));
            Assert.AreEqual(meta, File.ReadAllText(RoImportTestRoot.FullPath(_sheet) + ".meta"), "importer and sprite data unchanged");
            CollectionAssert.AreEqual(ids, RoSpriteSheetWriter.LoadSprites(_sheet, _spr.SlotCount).Select(FileId).ToArray());
        }

        [Test]
        public void ASheetWithoutIndex_HasNoSecondaryTexture_AndGetsOneLater()
        {
            Write(index: false);
            Assert.IsFalse(File.Exists(RoImportTestRoot.FullPath(RoSpriteOutputPaths.IndexOf(_sheet))));
            Assert.AreEqual(0, RoSpriteSheetWriter.LoadSprites(_sheet, 1)[0].GetSecondaryTextureCount());
            long id = FileId(RoSpriteSheetWriter.LoadSprites(_sheet, 1)[0]);

            Assert.AreEqual(RoStatus.Updated, RoSpriteSheetWriter.AddIndex(_writer, _sheet, _spr, _layout));

            var s = RoSpriteSheetWriter.LoadSprites(_sheet, 1)[0];
            Assert.AreEqual(1, s.GetSecondaryTextureCount());
            Assert.AreEqual(id, FileId(s), "same sprite");
        }
    }
}
