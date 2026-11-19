using NUnit.Framework;
using Uninarok.RoImport.Core.Formats;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Sprites;

namespace Uninarok.RoImport.Tests.Sprites
{
    // TASKS-0007 T1.2 / T1.3 (FR-1, FR-8). Values are GRF's, recorded by T0.1 (plan 4.1).
    [Category(SpriteTestFiles.Category)]
    public class RoSprReaderTests
    {
        static RoSpr Read(string file)
        {
            bool ok = RoSprReader.TryRead(RoPath.Parse(file), SpriteTestFiles.Bytes(file), out var spr, out var reason);
            Assert.IsTrue(ok, $"{file}: {reason}");
            Assert.IsNotNull(spr);
            return spr;
        }

        // file, indexed, true colour, first frame width, height
        [TestCase("sprite/몬스터/poring.spr", 49, 0, 37, 36)]
        [TestCase("sprite/몬스터/elemetal_master_s3.spr", 0, 113, 175, 250)]
        [TestCase("sprite/몬스터/aries.spr", 38, 9, -1, -1)]
        [TestCase("sprite/npc/4_f_agentkafra.spr", 2, 0, 41, 89)]
        [TestCase("sprite/인간족/몸통/남/초보자_남.spr", 110, 0, 38, 74)]
        [TestCase("sprite/인간족/머리통/남/1_남.spr", 15, 0, 29, 27)]
        [TestCase("sprite/로브/천사날개/천사날개.spr", 9, 0, 59, 55)]
        [TestCase("sprite/아이템/사과.spr", 1, 0, 14, 16)]
        [TestCase("sprite/이팩트/emotion.spr", 255, 0, 29, 23)]
        public void ReferenceSpr_HasItsFrameCounts_AndVersion21(string file, int indexed, int trueColor, int width, int height)
        {
            var spr = Read(file);

            Assert.AreEqual((2, 1), (spr.VersionMajor, spr.VersionMinor));
            Assert.AreEqual(indexed, spr.Indexed.Count, "indexed");
            Assert.AreEqual(trueColor, spr.TrueColor.Count, "true colour");
            if (width > 0) Assert.AreEqual((width, height), (spr.Slot(0).Width, spr.Slot(0).Height));
            foreach (var f in spr.Indexed) Assert.AreEqual(f.Width * f.Height, f.Indices.Length);
            foreach (var f in spr.TrueColor) Assert.AreEqual(f.Width * f.Height * 4, f.Rgba.Length);
        }

        [Test]
        public void Poring_IndicesAreTopRowFirst_AndThePaletteIsRgba()
        {
            var spr = Read("sprite/몬스터/poring.spr");

            var f = spr.Indexed[0];
            Assert.AreEqual(67, f.Indices[32], "first non-zero pixel is (32, 0), on the top row");
            for (int i = 0; i < 32; i++) Assert.AreEqual(0, f.Indices[i]);
            Assert.AreEqual(256, spr.Palette.Colors.Length);
            var c = spr.Palette.Colors[67];
            Assert.AreEqual((232, 125, 125), ((int)c.r, (int)c.g, (int)c.b));
        }

        [Test]
        public void TrueColourMonster_IsRgbaTopRowFirst_WithoutAPalette()
        {
            var spr = Read("sprite/몬스터/elemetal_master_s3.spr");

            Assert.IsNull(spr.Palette, "no indexed frame, no palette");
            var f = spr.TrueColor[0];
            int i = (28 * f.Width + 87) * 4;   // first opaque pixel (87, 28); GRF gives B, G, R, A = 0, 162, 255, 255
            Assert.AreEqual((255, 162, 0, 255), ((int)f.Rgba[i], (int)f.Rgba[i + 1], (int)f.Rgba[i + 2], (int)f.Rgba[i + 3]));
        }

        [Test]
        public void TruncatedEmptyAndForeignFiles_ReturnFalseWithAReason_AndNeverThrow()
        {
            byte[] full = SpriteTestFiles.Bytes("sprite/몬스터/poring.spr");
            var cases = new[]
            {
                ("empty", new byte[0]),
                ("null", (byte[])null),
                ("header only", SpriteTestFiles.Slice(full, 6)),
                ("counts only", SpriteTestFiles.Slice(full, 12)),
                ("half", SpriteTestFiles.Slice(full, full.Length / 2)),
                ("palette cut", SpriteTestFiles.Slice(full, full.Length - 1024)),
                ("not an SPR", new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }),
            };

            foreach (var (name, bytes) in cases)
            {
                bool ok = true;
                RoSpr spr = null;
                string reason = null;
                Assert.DoesNotThrow(() => ok = RoSprReader.TryRead(RoPath.Parse("x.spr"), bytes, out spr, out reason), name);
                Assert.IsFalse(ok, name);
                Assert.IsNull(spr, name);
                Assert.IsFalse(string.IsNullOrEmpty(reason), name + ": a reason");
            }
        }
    }
}
