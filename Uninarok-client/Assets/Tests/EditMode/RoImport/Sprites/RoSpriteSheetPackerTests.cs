using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Uninarok.RoImport.Core.Sprites;

namespace Uninarok.RoImport.Tests.Sprites
{
    // TASKS-0007 T3.1 / T3.2 (FR-1, FR-9, plan 4.2): our own deterministic shelf packer, power-of-two sheets of at most
    // 4096, 2 pixels of padding around every frame.
    [Ignore("pending T3.2")]
    public class RoSpriteSheetPackerTests
    {
        const int P = RoSpriteSheetPacker.Padding;

        static Vector2Int[] Sizes(RoSpr spr) => Enumerable.Range(0, spr.SlotCount).Select(i => new Vector2Int(spr.Slot(i).Width, spr.Slot(i).Height)).ToArray();

        static RoSheetLayout Pack(IReadOnlyList<Vector2Int> sizes)
        {
            Assert.IsTrue(RoSpriteSheetPacker.TryPack(sizes, out var layout));
            Assert.AreEqual(sizes.Count, layout.Rects.Length);
            return layout;
        }

        static bool IsPowerOfTwo(int v) => v > 0 && (v & (v - 1)) == 0;

        static RectInt Padded(RectInt r) => new RectInt(r.x - P, r.y - P, r.width + 2 * P, r.height + 2 * P);

        static IEnumerable<Vector2Int[]> Samples()
        {
            yield return Sizes(SpriteTestData.Monster().spr);
            yield return Sizes(SpriteTestData.Mixed().spr);
            yield return Sizes(SpriteTestData.Hundred().spr);
            yield return new[] { new Vector2Int(300, 500), new Vector2Int(1, 1), new Vector2Int(299, 2), new Vector2Int(40, 40) };
        }

        [Test]
        public void Layouts_ArePowerOfTwo_InsideTheSheet_WithoutOverlap_PaddingIncluded()
        {
            foreach (var sizes in Samples())
            {
                var layout = Pack(sizes);

                Assert.IsTrue(IsPowerOfTwo(layout.Width) && IsPowerOfTwo(layout.Height), $"{layout.Width} x {layout.Height}");
                Assert.LessOrEqual(Mathf.Max(layout.Width, layout.Height), RoSpriteSheetPacker.MaxSize);
                for (int i = 0; i < sizes.Length; i++)
                {
                    var r = layout.Rects[i];
                    Assert.AreEqual(sizes[i], r.size, "slot " + i + " keeps its size");
                    var p = Padded(r);
                    Assert.IsTrue(p.xMin >= 0 && p.yMin >= 0 && p.xMax <= layout.Width && p.yMax <= layout.Height, $"slot {i} {p} inside");
                    for (int j = i + 1; j < sizes.Length; j++)
                        Assert.IsFalse(p.Overlaps(Padded(layout.Rects[j])), $"slots {i} and {j} overlap");
                }
            }
        }

        [Test]
        public void TheSameSizes_GiveTheSameLayout()
        {
            var sizes = Sizes(SpriteTestData.Hundred().spr);

            var a = Pack(sizes);
            var b = Pack(sizes.ToArray());

            Assert.AreEqual((a.Width, a.Height), (b.Width, b.Height));
            CollectionAssert.AreEqual(a.Rects, b.Rects);
        }

        [Test]
        public void HundredFrames_FitOneSheet()
        {
            var layout = Pack(Sizes(SpriteTestData.Hundred().spr));

            Assert.AreEqual(100, layout.Rects.Length);
            Assert.LessOrEqual(layout.Width * layout.Height, 4 * 128 * 128, "packed, not one frame per row");
        }

        [Test]
        public void TheLargestFrameOfTheDataSet_Fits()
        {
            var sizes = Enumerable.Repeat(new Vector2Int(300, 500), 40).ToArray();   // like c_amdarais: 6 M pixels

            var layout = Pack(sizes);

            Assert.LessOrEqual(Mathf.Max(layout.Width, layout.Height), 4096);
        }

        [Test]
        public void ZeroSizeFrames_TakeNoSpace()
        {
            var layout = Pack(new[] { new Vector2Int(0, 0), new Vector2Int(8, 8) });

            Assert.AreEqual(Vector2Int.zero, layout.Rects[0].size);
        }

        [Test]
        public void TooLargeForOneSheet_ReturnsFalse()
        {
            Assert.IsFalse(RoSpriteSheetPacker.TryPack(new[] { new Vector2Int(4096, 10) }, out _), "padding makes it wider than 4096");
            Assert.IsFalse(RoSpriteSheetPacker.TryPack(Enumerable.Repeat(new Vector2Int(2000, 2000), 5).ToArray(), out _));
        }
    }
}
