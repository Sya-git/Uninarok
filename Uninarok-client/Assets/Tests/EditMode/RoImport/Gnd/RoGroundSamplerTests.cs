using NUnit.Framework;
using UnityEngine;
using Uninarok.RoImport.Core.Gnd;

namespace Uninarok.RoImport.Tests.Gnd
{
    // TASKS-0005 T3.3 (FR-3): the ground height at a point, by the top quads and diagonal of the ground (PLAN-0005 4.4).
    public class RoGroundSamplerTests
    {
        const float Eps = 1e-4f;

        static RoGndRules Rules
        {
            get
            {
                Assert.IsTrue(RoGndRules.TryGet(1, 7, out var rules));
                return rules;
            }
        }

        /// <summary>A Ragnarok map-space position at fractional cube coordinates.</summary>
        static Vector3 At(RoGnd gnd, float column, float row) =>
            new Vector3(column * gnd.Zoom, 0f, row * gnd.Zoom * RoMapSpace.RowDirection);

        static float HeightAt(RoGnd gnd, float column, float row)
        {
            Assert.IsTrue(RoGroundSampler.HeightAt(gnd, Rules, At(gnd, column, row), out float h), $"({column}, {row})");
            return h;
        }

        [Test]
        public void FlatGround_IsZeroEverywhere()
        {
            var gnd = GndTestMaps.Flat();

            Assert.AreEqual(0f, HeightAt(gnd, 0.3f, 0.7f), Eps);
            Assert.AreEqual(0f, HeightAt(gnd, 3.99f, 3.99f), Eps);
            Assert.AreEqual(0f, HeightAt(gnd, 4f, 4f), Eps, "the far edge belongs to the last cube");
        }

        [Test]
        public void Ramp_IsLinearAlongColumns()
        {
            var gnd = GndTestMaps.Ramp();   // -2.5 per cube along columns

            Assert.AreEqual(-2.5f * 1.25f, HeightAt(gnd, 1.25f, 2.6f), Eps);
            Assert.AreEqual(-2.5f * 3.9f, HeightAt(gnd, 3.9f, 0.1f), Eps);
        }

        [Test]
        public void Cliff_KeepsEachCubesOwnTop()
        {
            var gnd = GndTestMaps.CliffWithGap();   // rows 2 and 3 are 20 units higher

            Assert.AreEqual(0f, HeightAt(gnd, 2.5f, 1.99f), Eps);
            Assert.AreEqual(-20f, HeightAt(gnd, 2.5f, 2.01f), Eps);
        }

        [Test]
        public void AGapOrAPositionOutside_ReturnsFalse()
        {
            var gnd = GndTestMaps.CliffWithGap();

            Assert.IsFalse(RoGroundSampler.HeightAt(gnd, Rules, At(gnd, GndTestMaps.GapColumn + 0.5f, GndTestMaps.GapRow + 0.5f), out _), "gap");
            Assert.IsFalse(RoGroundSampler.HeightAt(gnd, Rules, At(gnd, -0.1f, 1f), out _));
            Assert.IsFalse(RoGroundSampler.HeightAt(gnd, Rules, At(gnd, 1f, 4.1f), out _));
            Assert.IsFalse(RoGroundSampler.HeightAt(gnd, Rules, new Vector3(float.NaN, 0f, 0f), out _));
        }

        [TestCase(0.25f, 0.25f, 0f)]
        [TestCase(0.75f, 0.75f, -5f)]
        [TestCase(0.9f, 0.4f, -3f)]
        public void TheDiagonal_IsTheGroundRulesH10ToH01(float u, float v, float expected)
        {
            // one cube whose corner H11 is 10 units up: with the diagonal H10 to H01 the triangle (00, 10, 01) is flat
            var gnd = GndTestMaps.Build(2, 2, (c, r, dx, dz) => c == 0 && r == 0 && dx == 1 && dz == 1 ? -10f : 0f);
            Assert.AreEqual(RoGndDiagonal.H10ToH01, Rules.Diagonal);

            Assert.AreEqual(expected, HeightAt(gnd, u, v), Eps);
        }
    }
}
