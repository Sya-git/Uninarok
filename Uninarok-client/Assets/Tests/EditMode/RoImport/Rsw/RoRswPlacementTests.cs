using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Uninarok.RoImport.Core.Gnd;
using Uninarok.RoImport.Core.Rsw;
using Uninarok.RoImport.Core.Space;

namespace Uninarok.RoImport.Tests.Rsw
{
    // TASKS-0006 T3.1 / T3.2 (FR-3): one matrix chain in Ragnarok space, converted once (PLAN-0006 4.3, D1, D13).
    // Rules verified by T0.3: position relative to the map centre, rotation Rz·Rx·Ry with all signs +1, scale as stored.
    [Ignore("pending T3.2")]
    public class RoRswPlacementTests
    {
        const float Eps = 1e-4f;

        static void AssertNear(Vector3 expected, Vector3 actual, string what)
        {
            Assert.AreEqual(expected.x, actual.x, Eps, what + " x");
            Assert.AreEqual(expected.y, actual.y, Eps, what + " y");
            Assert.AreEqual(expected.z, actual.z, Eps, what + " z");
        }

        static (Vector3 p, Quaternion r, Vector3 s) Place(RswTestMap map, string name)
        {
            var space = new RoMapSpace(map.Width, map.Height, map.Zoom);
            var model = map.Rsw.Models.Single(m => m.Name == name);
            return Place(model, space);
        }

        static (Vector3 p, Quaternion r, Vector3 s) Place(RoRswModel model, RoMapSpace space)
        {
            var ro = RoRswPlacement.ModelMatrixRo(model, space, RoRswRules.V1);
            RoRswPlacement.ToUnity(ro, out var p, out var r, out var s);
            return (p, r, s);
        }

        static (Vector3 p, Quaternion r, Vector3 s) PlaceAt(Vector3 rotation, Vector3? scale = null) =>
            Place(RswTestMaps.Model("m", Vector3.zero, rotation, scale), new RoMapSpace(10, 10, 10f));

        [Test]
        public void RulesV1_AreTheVerifiedOnes()
        {
            var rules = RoRswRules.V1;
            Assert.AreEqual(1f, rules.PositionScale);
            Assert.AreEqual(RoAxisOrder.ZXY, rules.RotationOrder);
            Assert.AreEqual((1f, 1f, 1f), (rules.RotationSignX, rules.RotationSignY, rules.RotationSignZ));
            Assert.AreEqual(1f, rules.ScaleYSign, "T0.3: -1 floats izlude's mirrored tools above their bench");
            Assert.IsFalse(rules.NodeNameSelectsRoot);
        }

        [Test]
        public void RulesTable_HasOnlyTheVerifiedVersion()
        {
            Assert.IsTrue(RoRswRules.TryGet(2, 1, out var rules));
            Assert.AreEqual(RoRswRules.V1.RotationOrder, rules.RotationOrder);
            Assert.IsFalse(RoRswRules.TryGet(2, 6, out _), "no row is guessed");
            Assert.IsFalse(RoRswRules.TryGet(1, 9, out _));
        }

        [Test]
        public void AModelAtTheMapCentre_IsAtTheGroundCentre()
        {
            AssertNear(new Vector3(5, 0, 5), Place(RswTestMaps.Full(), "centre").p, "10 x 10 cubes of 1 unit");
        }

        [TestCase("corner_sw", 0f, 0f)]
        [TestCase("corner_se", 10f, 0f)]
        [TestCase("corner_nw", 0f, 10f)]
        [TestCase("corner_ne", 10f, 10f)]
        public void AModelAtACorner_IsAtThatGroundCorner(string name, float x, float z)
        {
            AssertNear(new Vector3(x, 0, z), Place(RswTestMaps.Full(), name).p, name);
        }

        [TestCase("corner_sw", 0f, 0f)]
        [TestCase("corner_se", 6f, 0f)]
        [TestCase("corner_nw", 0f, 4f)]
        [TestCase("corner_ne", 6f, 4f)]
        public void ANonSquareMap_IsNotSwapped(string name, float x, float z)
        {
            AssertNear(new Vector3(x, 0, z), Place(RswTestMaps.NonSquare(), name).p, name);
        }

        [Test]
        public void Positions_AreRoMapSpaceFromRswRo_ThroughRoSpace()
        {
            var space = new RoMapSpace(156, 196, 10f);
            foreach (var position in new[] { new Vector3(0, 0, 0), new Vector3(-660, 0, 770), new Vector3(84.4f, -6f, -157.9f) })
                AssertNear(RoSpace.Point(space.FromRswRo(position)), Place(RswTestMaps.Model("m", position), space).p, position.ToString());
        }

        [Test]
        public void HeightPointsDownInTheFile_AndUpInUnity()
        {
            Assert.AreEqual(1f, Place(RswTestMaps.Model("m", new Vector3(0, -10, 0)), new RoMapSpace(4, 4, 10f)).p.y, Eps);
        }

        [Test]
        public void RotationAboutY_TurnsTheModelXAxisTowardMinusZ()
        {
            AssertNear(new Vector3(0, 0, -1), PlaceAt(new Vector3(0, 90, 0)).r * Vector3.right, "Ry(90) x");
        }

        [Test]
        public void RotationAboutX_TurnsTheModelZAxisUp()
        {
            // Rx(90) maps Ragnarok +Z to Ragnarok -Y, which is Unity up.
            AssertNear(Vector3.up, PlaceAt(new Vector3(90, 0, 0)).r * Vector3.forward, "Rx(90) z");
        }

        [Test]
        public void RotationAboutZ_TurnsTheModelXAxisDown()
        {
            // Rz(90) maps Ragnarok +X to Ragnarok +Y, which is Unity down.
            AssertNear(Vector3.down, PlaceAt(new Vector3(0, 0, 90)).r * Vector3.right, "Rz(90) x");
        }

        [Test]
        public void RotationOrder_IsYThenXThenZ()
        {
            // Y first: x -> -z; then X: -z -> Ragnarok +Y (Unity down). The reverse order would give -z.
            AssertNear(Vector3.down, PlaceAt(new Vector3(90, 90, 0)).r * Vector3.right, "Rz·Rx·Ry, x then y");
            // Y first: x -> -z; Z leaves -z alone. X first would leave x alone and Z turn it down.
            AssertNear(new Vector3(0, 0, -1), PlaceAt(new Vector3(0, 90, 90)).r * Vector3.right, "Rz·Rx·Ry, y then z");
        }

        [Test]
        public void TheDecomposition_RebuildsTheConvertedMatrix()
        {
            var space = new RoMapSpace(10, 10, 10f);
            var model = RswTestMaps.Model("m", new Vector3(12, -3, -7), new Vector3(315, 90, 90), new Vector3(0.66f, -0.66f, 0.66f));
            var ro = RoRswPlacement.ModelMatrixRo(model, space, RoRswRules.V1);
            RoRswPlacement.ToUnity(ro, out var p, out var r, out var s);

            var expected = RoSpace.Matrix(ro);
            var rebuilt = Matrix4x4.TRS(p, r, s);
            for (int i = 0; i < 16; i++) Assert.AreEqual(expected[i], rebuilt[i], Eps, $"element {i}");
        }

        [Test]
        public void MirroredOnOneAxis_KeepsANegativeScale_AndANegativeDeterminant()
        {
            var (_, r, s) = Place(RswTestMaps.Full(), "mirror_x");
            AssertNear(new Vector3(-1, 1, 1), s, "scale");
            Assert.AreEqual(1f, Mathf.Abs(Quaternion.Dot(r, Quaternion.identity)), Eps, "no rotation");
            Assert.Less(Matrix4x4.TRS(Vector3.zero, r, s).determinant, 0f, "Unity flips the culling, so the model is not inside out");
        }

        [Test]
        public void MirroredOnThreeAxes_IsAReflectionThatRebuildsTheMatrix()
        {
            var (_, r, s) = Place(RswTestMaps.Full(), "mirror_xyz");
            var m = Matrix4x4.TRS(Vector3.zero, r, s);
            Assert.Less(m.determinant, 0f);
            AssertNear(new Vector3(-1, 0, 0), m.MultiplyVector(Vector3.right), "x");
            AssertNear(new Vector3(0, -1, 0), m.MultiplyVector(Vector3.up), "y");
            AssertNear(new Vector3(0, 0, -1), m.MultiplyVector(Vector3.forward), "z");
        }

        [Test]
        public void ScaleIsKeptAsStored()
        {
            AssertNear(new Vector3(1.16f, 1.16f, 1.16f), PlaceAt(Vector3.zero, new Vector3(1.16f, 1.16f, 1.16f)).s, "uniform");
            AssertNear(new Vector3(1, 2, 3), PlaceAt(Vector3.zero, new Vector3(1, 2, 3)).s, "per axis");
        }
    }
}
