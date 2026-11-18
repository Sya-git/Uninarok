using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Uninarok.RoImport.Core.Formats;
using Uninarok.RoImport.Core.Gnd;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;

namespace Uninarok.RoImport.Tests.Gnd
{
    // TASKS-0004 T1.2 (FR-4, FR-5). The expected values come from a raw read of the files that does not use GRF
    // (TASKS-0004 T0.2, PLAN-0004 §4.1), so these tests check the adapter, not GRF against itself.
    [Category(GndTestData.Category)]
    [Ignore("pending T1.3")]
    public class RoGndReaderTests
    {
        static RoGnd Read(string map, RoReport report = null) => Read(map, GndTestData.MapBytes(map), report);

        static RoGnd Read(string map, byte[] bytes, RoReport report)
        {
            bool ok = RoGndReader.TryRead(RoPath.Parse(map + ".gnd"), bytes, report, out var gnd, out var reason);
            Assert.IsTrue(ok, $"{map}: {reason}");
            Assert.IsNotNull(gnd);
            return gnd;
        }

        // map, width, height (reference-models.md §4), textures, tiles (T0.2)
        [TestCase("prontera", 156, 196, 12, 28511)]
        [TestCase("izlude", 134, 150, 21, 20358)]
        [TestCase("prt_fild08", 200, 200, 21, 39596)]
        public void ReferenceMap_ReadsWithTheHeaderValues(string map, int width, int height, int textures, int tiles)
        {
            var report = new RoReport();
            var gnd = Read(map, report);
            Assert.AreEqual(1, gnd.VersionMajor, "major version (T0.2: all reference maps are 1.7)");
            Assert.AreEqual(7, gnd.VersionMinor, "minor version");
            Assert.AreEqual(width, gnd.Width, "width");
            Assert.AreEqual(height, gnd.Height, "height");
            Assert.AreEqual(10f, gnd.Zoom, "zoom (T0.2)");
            Assert.AreEqual(width * height, gnd.Cubes.Count, "cube table is Width * Height");
            Assert.AreEqual(textures, gnd.Textures.Count, "textures");
            Assert.AreEqual(tiles, gnd.Tiles.Count, "tiles");
            Assert.IsEmpty(report.Issues, "a reference map has no bad index (T0.2)");
        }

        [Test]
        public void Izlude_CubesHaveTheFileValues()
        {
            // Raw values recorded by T0.2 (PLAN-0004 4.1): BottomLeft, BottomRight, TopLeft, TopRight = H00, H10, H01, H11.
            var gnd = Read("izlude");
            var first = gnd.Cubes[0];
            Assert.AreEqual((59f, 59f, 59f), (first.H00, first.H10, first.H01));
            Assert.AreEqual(60.05f, first.H11, 1e-4f);
            Assert.AreEqual((0, RoGndCube.None, RoGndCube.None), (first.TileUp, first.TileFront, first.TileSide));

            var wall = gnd.CubeAt(132, 0);
            Assert.AreEqual((59f, 59f), (wall.H00, wall.H10));
            Assert.AreEqual(61.1f, wall.H01, 1e-4f);
            Assert.AreEqual(60.05f, wall.H11, 1e-4f);
            Assert.AreEqual((132, 133, RoGndCube.None), (wall.TileUp, wall.TileFront, wall.TileSide));
        }

        [Test]
        public void PrtFild08_TileZeroIsRead_AndTileCoordinatesKeepTheCornerOrder()
        {
            var gnd = Read("prt_fild08");
            var first = gnd.Cubes[0];
            Assert.AreEqual((0, 1, 2), (first.TileUp, first.TileFront, first.TileSide), "tile id 0 is a valid tile");

            // tile 20100: u = (1, 0, 1, 0), v = (1, 1, 0, 0), texture 0 (T0.2)
            var tile = gnd.Tiles[20100];
            Assert.AreEqual(new Vector2(1, 1), tile.Uv00);
            Assert.AreEqual(new Vector2(0, 1), tile.Uv10);
            Assert.AreEqual(new Vector2(1, 0), tile.Uv01);
            Assert.AreEqual(new Vector2(0, 0), tile.Uv11);
            Assert.AreEqual(0, tile.Texture);
        }

        [Test]
        public void NonSquareMap_LastCubeIsTheLastOfTheTable()
        {
            var gnd = Read("prontera");
            Assert.AreEqual(gnd.Cubes.Count - 1, gnd.IndexOf(gnd.Width - 1, gnd.Height - 1));
        }

        [Test]
        public void KoreanTextureNames_AreDecoded()
        {
            Assert.AreEqual("필드바닥\\prt_초원01.bmp", Read("prontera").Textures[0]);
        }

        [Test]
        public void Reading_LeavesGrfsDisplayEncodingAsItWas()
        {
            var before = Utilities.Services.EncodingService.DisplayEncoding;
            Read("izlude");
            Assert.AreSame(before, Utilities.Services.EncodingService.DisplayEncoding);
        }

        [Test]
        public void BadIndices_AreReportedOnce_AndTreatedAsNone()
        {
            byte[] bytes = (byte[])GndTestData.MapBytes("izlude").Clone();
            var layout = GndTestData.Layout.Of(bytes);
            Assert.AreEqual(20358, layout.TileCount, "layout check");
            layout.SetCubeTile(bytes, 0, GndTestData.Layout.CubeTileUpField, layout.TileCount + 5);   // past the table
            layout.SetCubeTile(bytes, 1, GndTestData.Layout.CubeTileSideField, -7);                   // below -1
            layout.SetTileTexture(bytes, 1, 99);                                                      // 21 textures

            var report = new RoReport();
            var gnd = Read("izlude", bytes, report);

            Assert.AreEqual(RoGndCube.None, gnd.Cubes[0].TileUp);
            Assert.AreEqual(RoGndCube.None, gnd.Cubes[1].TileSide);
            Assert.AreEqual(RoGndCube.None, gnd.Tiles[1].Texture);
            var issues = report.Issues.Where(i => i.Code == RoIssueCode.GndBadIndex).ToList();
            Assert.AreEqual(1, issues.Count, "reported once per map");
            StringAssert.Contains("3", issues[0].Message, "with the count");
            Assert.AreEqual(1, report.Issues.Count, "nothing else reported");
        }

        [Test]
        public void TruncatedFile_FailsWithAReason_AndDoesNotThrow()
        {
            byte[] bytes = GndTestData.MapBytes("izlude");
            foreach (int length in new[] { 3, 6, 20, 1000, bytes.Length / 2, bytes.Length - 10 })
            {
                var cut = new byte[length];
                Array.Copy(bytes, cut, length);
                bool ok = true; RoGnd gnd = null; string reason = null;
                Assert.DoesNotThrow(() => ok = RoGndReader.TryRead(RoPath.Parse("cut.gnd"), cut, null, out gnd, out reason));
                Assert.IsFalse(ok, $"length {length}");
                Assert.IsNull(gnd);
                Assert.IsFalse(string.IsNullOrEmpty(reason));
            }
        }

        [Test]
        public void EmptyAndNull_FailWithAReason()
        {
            foreach (var bytes in new[] { new byte[0], null })
            {
                Assert.IsFalse(RoGndReader.TryRead(RoPath.Parse("empty.gnd"), bytes, null, out var gnd, out var reason));
                Assert.IsNull(gnd);
                Assert.IsFalse(string.IsNullOrEmpty(reason));
            }
        }

        [Test]
        public void NotAGnd_FailsWithAReason()
        {
            var bytes = System.Text.Encoding.ASCII.GetBytes("GRAT this is not a ground, just some bytes that are long enough");
            Assert.IsFalse(RoGndReader.TryRead(RoPath.Parse("x.gnd"), bytes, null, out var gnd, out var reason));
            Assert.IsNull(gnd);
            Assert.IsFalse(string.IsNullOrEmpty(reason));
        }
    }
}
