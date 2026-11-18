using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Uninarok.Map;
using Uninarok.RoImport.Core.Gat;
using Uninarok.RoImport.Core.Gnd;
using Uninarok.RoImport.Core.Results;

namespace Uninarok.RoImport.Tests.Gat
{
    // TASKS-0005 T3.1 / T3.2 (FR-1, FR-3, FR-5): the arrays and geometry of the map data, from RoMapSpace and RoGndRules.
    [Ignore("pending T3.2")]
    public class RoGatBuilderTests
    {
        const float Eps = 1e-5f;
        const string Source = "test.gat";

        static RoGndRules Rules
        {
            get
            {
                Assert.IsTrue(RoGndRules.TryGet(1, 7, out var rules));
                return rules;
            }
        }

        static RoMapDataArrays Build(RoGat gat, int gndWidth, int gndHeight, RoReport report = null, float zoom = 10f) =>
            RoGatBuilder.Build(Source, gat, new RoMapSpace(gndWidth, gndHeight, zoom), Rules, report ?? new RoReport());

        [Test]
        public void A4x3CubeMap_BuildsHeightsTypesAndFlagsPerCell()
        {
            var gat = GatTestMaps.Asymmetric8x6();
            var report = new RoReport();

            var data = Build(gat, 4, 3, report);

            Assert.IsNotNull(data);
            Assert.IsEmpty(report.Issues);
            Assert.AreEqual((8, 6), (data.CellsX, data.CellsZ));
            Assert.AreEqual(48 * 4, data.Heights.Length);
            Assert.AreEqual(48, data.Types.Length);
            for (int i = 0; i < 48; i++)
            {
                var type = RoGatRules.TypeOf(gat.Types[i]);
                Assert.AreEqual((byte)type, data.Types[i], $"type {i}");
                Assert.AreEqual((byte)RoGatRules.FlagsOf(type), data.Flags[i], $"flags {i}");
                Assert.AreEqual((byte)gat.Types[i], data.RawTypes[i], $"raw {i}");
                for (int k = 0; k < 4; k++)
                    Assert.AreEqual(-gat.Heights[i * 4 + k] * 0.1f, data.Heights[i * 4 + k], Eps, "Unity up, scaled: one cube is one unit");
            }
            Assert.AreEqual((byte)MapCellType.Wall, data.Types[7]);
            Assert.AreEqual((byte)MapCellType.Cliff, data.Types[5 * 8]);
            Assert.AreEqual((byte)(MapCellFlags.Walkable | MapCellFlags.Shootable | MapCellFlags.Water), data.Flags[2 * 8 + 1]);
        }

        [Test]
        public void TheGeometry_ComesFromTheMapSpace_AndTheGroundRules()
        {
            var data = Build(GatTestMaps.Asymmetric8x6(), 4, 3);
            var space = new RoMapSpace(4, 3, 10f);

            Assert.AreEqual(0.5f, data.CellSize, Eps, "half a cube at zoom 10");
            Assert.AreEqual(RoMapSpace.RowDirection, data.RowSign);
            Assert.IsFalse(data.DiagonalFlip, "GND 1.7: diagonal H10 to H01");
            // a known corner: cell (6, 4) starts at grid corner (3, 2) of the ground
            var corner = space.CornerUnity(3, 2, 0f);
            Assert.AreEqual(corner.x, data.Origin.x + 6 * data.CellSize, Eps);
            Assert.AreEqual(corner.z, data.Origin.y + 4 * data.CellSize * data.RowSign, Eps);
        }

        [Test]
        public void TheCellSize_FollowsTheZoom()
        {
            var data = Build(GatTestMaps.Flat4x4(), 2, 2, zoom: 5f);

            Assert.AreEqual(0.25f, data.CellSize, Eps);
        }

        [Test]
        public void NonSquareMaps_AreNotSwapped()
        {
            var wide = Build(GatTestMaps.Asymmetric8x6(), 4, 3);
            var tall = Build(GatTestMaps.Asymmetric6x8(), 3, 4);

            Assert.AreEqual((8, 6), (wide.CellsX, wide.CellsZ));
            Assert.AreEqual((6, 8), (tall.CellsX, tall.CellsZ));
            Assert.AreEqual((byte)MapCellType.Wall, tall.Types[5], "cell (W-1, 0) of the 6 x 8 map");
            Assert.AreEqual((byte)MapCellType.Cliff, tall.Types[7 * 6], "cell (0, H-1) of the 6 x 8 map");
        }

        [TestCase(4, 4)]   // GAT is 8 x 6: rows do not match
        [TestCase(3, 3)]   // columns do not match
        [TestCase(8, 6)]   // the GAT's own size (one cell per cube)
        public void AGridThatIsNotTwiceTheGround_GivesGatGndMismatch_AndNoOutput(int gndWidth, int gndHeight)
        {
            var report = new RoReport();

            var data = Build(GatTestMaps.Asymmetric8x6(), gndWidth, gndHeight, report);

            Assert.IsNull(data);
            Assert.AreEqual(1, report.Issues.Count);
            Assert.AreEqual(RoIssueCode.GatGndMismatch, report.Issues[0].Code);
        }

        [Test]
        public void UnknownTypes_AreReportedOncePerMap_WithACount()
        {
            var gat = GatTestMaps.Build(4, 4, (x, z) => 0f, (x, z) => x == 0 ? 0x80000003u : x == 1 ? 9u : 0u);
            var report = new RoReport();

            var data = Build(gat, 2, 2, report);

            Assert.IsNotNull(data, "unknown types do not stop the conversion");
            Assert.AreEqual(8, data.UnknownCells);
            var issues = report.Issues.Where(i => i.Code == RoIssueCode.GatUnknownType).ToArray();
            Assert.AreEqual(1, issues.Length);
            StringAssert.Contains("8 cells", issues[0].Message);
            StringAssert.Contains("0x80000003", issues[0].Message);
            StringAssert.Contains("0x9", issues[0].Message);
            Assert.AreEqual((byte)MapCellType.Other, data.Types[0]);
            Assert.AreEqual((byte)MapCellFlags.None, data.Flags[0]);
            Assert.AreEqual(3, data.RawTypes[0], "the raw byte keeps the low byte");
        }

        [Test]
        public void HeightsAreConvertedToUnityUp()
        {
            var data = Build(GatTestMaps.Cliff4x4(), 2, 2);

            int i = (1 * 4 + 2) * 4;
            Assert.AreEqual(2f, data.Heights[i + 3], Eps, "the cliff corner, 20 Ragnarok units up, is 2 Unity units up");
            Assert.AreEqual(0f, data.Heights[i], Eps);
        }
    }
}
