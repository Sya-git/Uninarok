using System.Linq;
using NUnit.Framework;
using Uninarok.RoImport.Core.Formats;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Sprites;

namespace Uninarok.RoImport.Tests.Sprites
{
    // TASKS-0007 T1.2 / T1.3 (FR-7, FR-8). Values are GRF's, recorded by T0.1 (plan 4.1).
    [Category(SpriteTestFiles.Category)]
    [Ignore("pending T1.3")]
    public class RoPalReaderTests
    {
        const string Body = "palette/몸/초보자_남_1.pal";

        [Test]
        public void BodyPalette_Has256Colours_RgbaWithAlpha255()
        {
            bool ok = RoPalReader.TryRead(RoPath.Parse(Body), SpriteTestFiles.Bytes(Body), out var pal, out var reason);

            Assert.IsTrue(ok, reason);
            Assert.AreEqual(RoPal.Size, pal.Colors.Length);
            Assert.AreEqual((192, 240, 188), (pal.Colors[0].r, pal.Colors[0].g, pal.Colors[0].b));
            Assert.AreEqual((255, 255, 255), (pal.Colors[1].r, pal.Colors[1].g, pal.Colors[1].b));
            Assert.AreEqual((255, 0, 255), (pal.Colors[255].r, pal.Colors[255].g, pal.Colors[255].b));
            Assert.IsTrue(pal.Colors.All(c => c.a == 255), "PAL files store alpha 0; the adapter sets 255 (index 0 is transparent by rule)");
        }

        [Test]
        public void ShortEmptyAndLongFiles_ReturnFalseWithAReason_AndNeverThrow()
        {
            byte[] full = SpriteTestFiles.Bytes(Body);
            var cases = new[]
            {
                ("empty", new byte[0]),
                ("null", (byte[])null),
                ("short", SpriteTestFiles.Slice(full, 1000)),
                ("long", full.Concat(new byte[4]).ToArray()),
            };

            foreach (var (name, bytes) in cases)
            {
                bool ok = true;
                RoPal pal = null;
                string reason = null;
                Assert.DoesNotThrow(() => ok = RoPalReader.TryRead(RoPath.Parse("x.pal"), bytes, out pal, out reason), name);
                Assert.IsFalse(ok, name);
                Assert.IsNull(pal, name);
                Assert.IsFalse(string.IsNullOrEmpty(reason), name + ": a reason");
            }
        }
    }
}
