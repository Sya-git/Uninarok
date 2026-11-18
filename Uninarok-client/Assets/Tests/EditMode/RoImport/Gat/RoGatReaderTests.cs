using System;
using NUnit.Framework;
using Uninarok.RoImport.Core.Formats;
using Uninarok.RoImport.Core.Gat;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Tests.Gnd;

namespace Uninarok.RoImport.Tests.Gat
{
    // TASKS-0005 T2.2 / T2.3 (FR-1). The expected values come from a raw read of the files that does not use GRF
    // (TASKS-0005 T0.2, PLAN-0005 4.1), so these tests check the adapter, not GRF against itself.
    [Category(GndTestData.Category)]
    public class RoGatReaderTests
    {
        static RoGat Read(string map)
        {
            bool ok = RoGatReader.TryRead(RoPath.Parse(map + ".gat"), GatTestData.MapBytes(map), out var gat, out var reason);
            Assert.IsTrue(ok, $"{map}: {reason}");
            Assert.IsNotNull(gat);
            return gat;
        }

        static void AssertCell(RoGat gat, int index, float h00, float h10, float h01, float h11, uint type)
        {
            int h = index * 4;
            Assert.AreEqual(h00, gat.Heights[h], 1e-4f, $"cell {index} H00");
            Assert.AreEqual(h10, gat.Heights[h + 1], 1e-4f, $"cell {index} H10");
            Assert.AreEqual(h01, gat.Heights[h + 2], 1e-4f, $"cell {index} H01");
            Assert.AreEqual(h11, gat.Heights[h + 3], 1e-4f, $"cell {index} H11");
            Assert.AreEqual(type, gat.Types[index], $"cell {index} type");
        }

        // map, GND width and height in cubes (reference-models.md §4)
        [TestCase("prontera", 156, 196)]
        [TestCase("izlude", 134, 150)]
        [TestCase("prt_fild08", 200, 200)]
        public void ReferenceMap_IsTwiceItsGround_AndVersion12(string map, int gndWidth, int gndHeight)
        {
            var gat = Read(map);

            Assert.AreEqual((1, 2), (gat.VersionMajor, gat.VersionMinor), "T0.2: every reference map is GAT 1.2");
            Assert.AreEqual(2 * gndWidth, gat.Width);
            Assert.AreEqual(2 * gndHeight, gat.Height);
            Assert.AreEqual(gat.Width * gat.Height, gat.Types.Length);
            Assert.AreEqual(gat.Width * gat.Height * 4, gat.Heights.Length);
        }

        [Test]
        public void Prontera_CellsHaveTheFileValues()
        {
            var gat = Read("prontera");
            AssertCell(gat, 0, 0, 0, 0, 0, 1);
            AssertCell(gat, gat.IndexOf(311, 0), 0, 0, 0, 0, 0);
            AssertCell(gat, gat.IndexOf(99, 386), 15, 30, 7.5f, 15, 1);
        }

        [Test]
        public void Izlude_CellsHaveTheFileValues()
        {
            var gat = Read("izlude");
            AssertCell(gat, 0, 59, 59, 59, 59.2625f, 1);
            AssertCell(gat, gat.IndexOf(133, 1), 60.75f, 60.75f, 62.5f, 62.5f, 1);
            AssertCell(gat, gat.CellCount - 1, 59.32813f, 0, 0, 0, 0);
        }

        [Test]
        public void PrtFild08_CellsHaveTheFileValues_IncludingACliff()
        {
            var gat = Read("prt_fild08");
            AssertCell(gat, gat.IndexOf(1, 1), 0, 0, 0, -40, 1);
            AssertCell(gat, gat.IndexOf(107, 34), -1.5f, -3, -1, -1.5f, 5);
            AssertCell(gat, gat.IndexOf(200, 200), 3, 4.5f, 3, 4.5f, 0);
        }

        [Test]
        public void TruncatedEmptyAndForeignFiles_ReturnFalseWithAReason_AndNeverThrow()
        {
            byte[] full = GatTestData.MapBytes("izlude");
            var cases = new[]
            {
                ("empty", new byte[0]),
                ("null", (byte[])null),
                ("header only", Slice(full, 14)),
                ("truncated", Slice(full, 1000)),
                ("one byte short", Slice(full, full.Length - 1)),
                ("short header", Slice(full, 9)),
                ("not a GAT", new byte[] { (byte)'G', (byte)'R', (byte)'G', (byte)'N', 1, 7, 0, 0, 0, 0, 0, 0, 0, 0 })
            };

            foreach (var (name, bytes) in cases)
            {
                bool ok = true;
                RoGat gat = null;
                string reason = null;
                Assert.DoesNotThrow(() => ok = RoGatReader.TryRead(RoPath.Parse("izlude.gat"), bytes, out gat, out reason), name);
                Assert.IsFalse(ok, name);
                Assert.IsNull(gat, name);
                Assert.IsFalse(string.IsNullOrEmpty(reason), name);
            }
        }

        static byte[] Slice(byte[] bytes, int length)
        {
            var copy = new byte[length];
            Array.Copy(bytes, copy, length);
            return copy;
        }
    }
}
