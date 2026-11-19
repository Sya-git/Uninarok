using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Uninarok.RoImport.Core.Sprites;

namespace Uninarok.RoImport.Tests.Sprites
{
    // TASKS-0007 T3.1 / T3.2 (FR-1, FR-7, plan 4.2, 4.3): the sheet's colour and index pixels, top row first.
    [Ignore("pending T3.2")]
    public class RoSpriteSheetPixelsTests
    {
        static RoSheetLayout Layout(RoSpr spr)
        {
            var sizes = Enumerable.Range(0, spr.SlotCount).Select(i => new Vector2Int(spr.Slot(i).Width, spr.Slot(i).Height)).ToArray();
            Assert.IsTrue(RoSpriteSheetPacker.TryPack(sizes, out var layout));
            return layout;
        }

        static Color32 At(byte[] rgba, RoSheetLayout layout, int x, int y)
        {
            int i = (y * layout.Width + x) * 4;
            return new Color32(rgba[i], rgba[i + 1], rgba[i + 2], rgba[i + 3]);
        }

        [Test]
        public void IndexedPixels_TakeTheirPaletteColour_AndIndex0IsTransparent()
        {
            var (spr, _) = SpriteTestData.Mixed();
            Assert.AreEqual(255, spr.Palette.Colors[0].a, "the sample's index 0 is opaque in its palette");
            var layout = Layout(spr);

            var rgba = RoSpriteSheetPixels.Color(spr, layout);

            Assert.AreEqual(layout.Width * layout.Height * 4, rgba.Length);
            for (int slot = 0; slot < spr.Indexed.Count; slot++)
            {
                var f = spr.Indexed[slot];
                var r = layout.Rects[slot];
                for (int y = 0; y < f.Height; y++)
                    for (int x = 0; x < f.Width; x++)
                    {
                        int k = f.Indices[y * f.Width + x];
                        var c = At(rgba, layout, r.x + x, r.y + y);
                        if (k == 0) Assert.AreEqual(0, c.a, $"slot {slot} ({x}, {y}): index 0 is transparent");
                        else
                        {
                            var p = spr.Palette.Colors[k];
                            Assert.AreEqual((p.r, p.g, p.b, (byte)255), (c.r, c.g, c.b, c.a), $"slot {slot} ({x}, {y})");
                        }
                    }
            }
        }

        [Test]
        public void TrueColourPixels_AreStoredAsIs_AlphaIncluded_AfterTheIndexedSlots()
        {
            var (spr, _) = SpriteTestData.Mixed();
            var layout = Layout(spr);

            var rgba = RoSpriteSheetPixels.Color(spr, layout);

            for (int t = 0; t < spr.TrueColor.Count; t++)
            {
                var f = spr.TrueColor[t];
                var r = layout.Rects[spr.Indexed.Count + t];
                for (int y = 0; y < f.Height; y++)
                    for (int x = 0; x < f.Width; x++)
                    {
                        int i = (y * f.Width + x) * 4;
                        var c = At(rgba, layout, r.x + x, r.y + y);
                        Assert.AreEqual((f.Rgba[i], f.Rgba[i + 1], f.Rgba[i + 2], f.Rgba[i + 3]), (c.r, c.g, c.b, c.a), $"true colour {t} ({x}, {y})");
                    }
            }
        }

        [Test]
        public void TheIndexTexture_HoldsIndicesOnlyForIndexedPixels()
        {
            var (spr, _) = SpriteTestData.Mixed();
            var layout = Layout(spr);

            var index = RoSpriteSheetPixels.Index(spr, layout);

            Assert.AreEqual(layout.Width * layout.Height, index.Length);
            var f = spr.Indexed[1];
            var r = layout.Rects[1];
            for (int y = 0; y < f.Height; y++)
                for (int x = 0; x < f.Width; x++)
                    Assert.AreEqual(f.Indices[y * f.Width + x], index[(r.y + y) * layout.Width + r.x + x]);

            var tr = layout.Rects[spr.Indexed.Count];
            for (int y = 0; y < tr.height; y++)
                for (int x = 0; x < tr.width; x++)
                    Assert.AreEqual(0, index[(tr.y + y) * layout.Width + tr.x + x], "true colour has index 0");
        }

        [Test]
        public void ThePadding_RepeatsTheEdgePixels()
        {
            var (spr, _) = SpriteTestData.Mixed();
            var layout = Layout(spr);
            var rgba = RoSpriteSheetPixels.Color(spr, layout);
            var index = RoSpriteSheetPixels.Index(spr, layout);

            int slot = spr.Indexed.Count + 1;   // a true-colour frame: its edges are not transparent
            var r = layout.Rects[slot];
            for (int d = 1; d <= RoSpriteSheetPacker.Padding; d++)
            {
                Assert.AreEqual(At(rgba, layout, r.x, r.y + 1), At(rgba, layout, r.x - d, r.y + 1), "left");
                Assert.AreEqual(At(rgba, layout, r.xMax - 1, r.y + 1), At(rgba, layout, r.xMax - 1 + d, r.y + 1), "right");
                Assert.AreEqual(At(rgba, layout, r.x + 1, r.y), At(rgba, layout, r.x + 1, r.y - d), "top");
                Assert.AreEqual(At(rgba, layout, r.x + 1, r.yMax - 1), At(rgba, layout, r.x + 1, r.yMax - 1 + d), "bottom");
                Assert.AreEqual(At(rgba, layout, r.x, r.y), At(rgba, layout, r.x - d, r.y - d), "corner");
            }

            var ir = layout.Rects[1];
            Assert.AreEqual(index[(ir.y + 3) * layout.Width + ir.x], index[(ir.y + 3) * layout.Width + ir.x - 1], "index padding too");
        }

        [Test]
        public void AFrameWithoutPalette_IsTrueColourOnly()
        {
            var (spr, _) = SpriteTestData.TrueColorEffect();
            var layout = Layout(spr);

            Assert.DoesNotThrow(() => RoSpriteSheetPixels.Color(spr, layout));
            Assert.IsTrue(RoSpriteSheetPixels.Index(spr, layout).All(b => b == 0));
        }
    }
}
