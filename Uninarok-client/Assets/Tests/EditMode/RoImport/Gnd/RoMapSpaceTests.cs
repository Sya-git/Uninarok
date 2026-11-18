using NUnit.Framework;
using UnityEngine;
using Uninarok.RoImport.Core.Gnd;

namespace Uninarok.RoImport.Tests.Gnd
{
    // TASKS-0004 T2.1 (FR-4, FR-5): the shared map space of PLAN-0004 4.2, with the row direction verified by T0.3.
    [Ignore("pending T2.2")]
    public class RoMapSpaceTests
    {
        const float Eps = 1e-5f;

        static void AssertNear(Vector3 expected, Vector3 actual, string message = null)
        {
            Assert.AreEqual(expected.x, actual.x, Eps, message + " x");
            Assert.AreEqual(expected.y, actual.y, Eps, message + " y");
            Assert.AreEqual(expected.z, actual.z, Eps, message + " z");
        }

        [Test]
        public void RowDirection_IsPlus1()
        {
            // T0.3: RSW models sit on the ground only when rows grow along +Z (PLAN-0004 4.3).
            Assert.AreEqual(1, RoMapSpace.RowDirection);
        }

        [Test]
        public void CornerRo_IsColumnAndRowTimesZoom_WithTheHeightUnchanged()
        {
            var space = new RoMapSpace(156, 196, 10f);
            Assert.AreEqual(new Vector3(0, 0, 0), space.CornerRo(0, 0, 0));
            Assert.AreEqual(new Vector3(30, -5, 20), space.CornerRo(3, 2, -5));
            Assert.AreEqual(new Vector3(1560, 7.5f, 1960), space.CornerRo(156, 196, 7.5f));
        }

        [Test]
        public void CornerUnity_IsRoSpacePointOfCornerRo()
        {
            var space = new RoMapSpace(156, 196, 10f);
            AssertNear(new Vector3(3, 0.5f, 2), space.CornerUnity(3, 2, -5), "higher ground (Y down) is up in Unity");
            AssertNear(new Vector3(156, -0.75f, 196), space.CornerUnity(156, 196, 7.5f));
        }

        [Test]
        public void Zoom10_GivesCubesOfExactly1Unit()
        {
            var space = new RoMapSpace(4, 4, 10f);
            Assert.AreEqual(1f, space.CornerUnity(1, 0, 0).x - space.CornerUnity(0, 0, 0).x, Eps);
            Assert.AreEqual(1f, space.CornerUnity(0, 1, 0).z - space.CornerUnity(0, 0, 0).z, Eps);
        }

        [Test]
        public void Zoom5_GivesCubesOfHalfAUnit_AndHeightsKeepTheirScale()
        {
            var space = new RoMapSpace(4, 4, 5f);
            Assert.AreEqual(0.5f, space.CornerUnity(1, 0, 0).x, Eps);
            Assert.AreEqual(0.5f, space.CornerUnity(0, 1, 0).z, Eps);
            Assert.AreEqual(1f, space.CornerUnity(0, 0, -10f).y, Eps, "ten Ragnarok units of height are one Unity unit at any zoom");
        }

        [Test]
        public void CentreOffsetRo_IsHalfTheMap()
        {
            AssertNear(new Vector3(780, 0, 980), new RoMapSpace(156, 196, 10f).CentreOffsetRo);
            AssertNear(new Vector3(10, 0, 7.5f), new RoMapSpace(4, 3, 5f).CentreOffsetRo);
        }

        [Test]
        public void FromRswRo_AddsTheCentreOffset()
        {
            // T0.3: column = (x + Width * Zoom / 2) / Zoom, row = (z + Height * Zoom / 2) / Zoom; the height is unchanged.
            var space = new RoMapSpace(156, 196, 10f);
            AssertNear(new Vector3(780, -3, 980), space.FromRswRo(new Vector3(0, -3, 0)));
            AssertNear(new Vector3(790, 4, 960), space.FromRswRo(new Vector3(10, 4, -20)));
        }

        [Test]
        public void TryCubeAtUnity_RoundTripsEveryCornerAndCentre_OnANonSquareMap()
        {
            foreach (var space in new[] { new RoMapSpace(5, 3, 10f), new RoMapSpace(3, 5, 10f), new RoMapSpace(5, 3, 5f) })
            for (int row = 0; row < space.Height; row++)
            for (int column = 0; column < space.Width; column++)
            {
                var corner = space.CornerUnity(column, row, -12f);
                Assert.IsTrue(space.TryCubeAtUnity(corner, out int c, out int r), $"corner {column},{row}");
                Assert.AreEqual((column, row), (c, r), $"corner {column},{row} of {space.Width}x{space.Height}");

                var centre = (corner + space.CornerUnity(column + 1, row + 1, -12f)) / 2f;
                Assert.IsTrue(space.TryCubeAtUnity(centre, out c, out r), $"centre {column},{row}");
                Assert.AreEqual((column, row), (c, r), $"centre {column},{row} of {space.Width}x{space.Height}");
            }
        }

        [Test]
        public void TryCubeAtUnity_FailsOutsideTheMap()
        {
            var space = new RoMapSpace(5, 3, 10f);
            foreach (var p in new[]
                     {
                         new Vector3(-0.01f, 0, 1), new Vector3(1, 0, -0.01f),
                         new Vector3(5.01f, 0, 1), new Vector3(1, 0, 3.01f), new Vector3(4.5f, 0, 3.5f)
                     })
                Assert.IsFalse(space.TryCubeAtUnity(p, out _, out _), p.ToString("F2"));
        }

        [Test]
        public void TryCubeAtUnity_IgnoresTheHeight()
        {
            var space = new RoMapSpace(5, 3, 10f);
            Assert.IsTrue(space.TryCubeAtUnity(new Vector3(4.5f, -100f, 2.5f), out int c, out int r));
            Assert.AreEqual((4, 2), (c, r));
        }

        [Test]
        public void CubeOfGatCell_IsHalfTheCell()
        {
            Assert.AreEqual((0, 0), RoMapSpace.CubeOfGatCell(0, 0));
            Assert.AreEqual((0, 0), RoMapSpace.CubeOfGatCell(1, 1));
            Assert.AreEqual((2, 3), RoMapSpace.CubeOfGatCell(5, 7));
            Assert.AreEqual((155, 195), RoMapSpace.CubeOfGatCell(311, 391));
        }
    }
}
