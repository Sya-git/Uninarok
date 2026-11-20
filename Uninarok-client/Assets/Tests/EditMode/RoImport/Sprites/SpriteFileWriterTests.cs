using System.Linq;
using NUnit.Framework;
using Uninarok.RoImport.Core.Formats;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Sprites;

namespace Uninarok.RoImport.Tests.Sprites
{
    // TASKS-0007 T5.1: the test file writer round-trips through the GRF adapters, so the converter tests see what they wrote.
    public class SpriteFileWriterTests
    {
        [Test]
        public void Spr_RoundTrips_IndexedTrueColourAndPalette()
        {
            var (spr, _) = SpriteTestData.Mixed();

            Assert.IsTrue(RoSprReader.TryRead(RoPath.Parse("x.spr"), SpriteFileWriter.Spr(spr), out var read, out var reason), reason);

            Assert.AreEqual((2, 0), (read.VersionMajor, read.VersionMinor));
            Assert.AreEqual((2, 2), (read.Indexed.Count, read.TrueColor.Count));
            for (int i = 0; i < 2; i++)
            {
                CollectionAssert.AreEqual(spr.Indexed[i].Indices, read.Indexed[i].Indices, "indexed " + i);
                CollectionAssert.AreEqual(spr.TrueColor[i].Rgba, read.TrueColor[i].Rgba, "true colour " + i);
            }
            for (int i = 1; i < 256; i++)
                Assert.AreEqual((spr.Palette.Colors[i].r, spr.Palette.Colors[i].g, spr.Palette.Colors[i].b),
                    (read.Palette.Colors[i].r, read.Palette.Colors[i].g, read.Palette.Colors[i].b), "palette " + i);
        }

        [Test]
        public void Act_RoundTrips_EveryField()
        {
            foreach (var (_, act) in new[] { SpriteTestData.Mixed(), SpriteTestData.Weapon(), SpriteTestData.Anchored(), SpriteTestData.WithEvents() })
            {
                Assert.IsTrue(RoActReader.TryRead(RoPath.Parse("x.act"), SpriteFileWriter.Act(act), out var read, out var reason), reason);

                Assert.AreEqual((2, 5), (read.VersionMajor, read.VersionMinor));
                CollectionAssert.AreEqual(act.Events, read.Events);
                Assert.AreEqual(act.Actions.Count, read.Actions.Count);
                for (int a = 0; a < act.Actions.Count; a++)
                {
                    Assert.AreEqual(act.Actions[a].AnimationSpeed, read.Actions[a].AnimationSpeed, "speed " + a);
                    Assert.AreEqual(act.Actions[a].Frames.Count, read.Actions[a].Frames.Count);
                    for (int f = 0; f < act.Actions[a].Frames.Count; f++)
                    {
                        var e = act.Actions[a].Frames[f];
                        var r = read.Actions[a].Frames[f];
                        Assert.AreEqual(e.EventId, r.EventId);
                        CollectionAssert.AreEqual(e.Anchors, r.Anchors);
                        CollectionAssert.AreEqual(e.Layers.ToArray(), r.Layers.ToArray(), $"action {a} frame {f}");
                    }
                }
            }
        }

        [Test]
        public void Pal_RoundTrips()
        {
            var pal = SpriteTestData.Palette(3);

            Assert.IsTrue(RoPalReader.TryRead(RoPath.Parse("x.pal"), SpriteFileWriter.Pal(pal), out var read, out var reason), reason);

            Assert.AreEqual((pal.Colors[200].r, pal.Colors[200].g, pal.Colors[200].b), (read.Colors[200].r, read.Colors[200].g, read.Colors[200].b));
        }
    }
}
