using System;
using NUnit.Framework;
using UnityEngine;
using Uninarok.RoImport.Core.Space;

namespace Uninarok.RoImport.Tests
{
    // SPEC-0002 FR-7: correct handedness and up direction, one ground cube = one Unity unit. Plan 4.3, D6. TASKS T1.4.
    //
    // The change of basis is the reflection F = diag(1, -1, 1): Ragnarok's Y points down, Unity's up.
    // Points and directions map as F*v (points also scaled by 0.1); matrices map by conjugation F*M*F.
    public class RoSpaceTests
    {
        const float Eps = 1e-5f;

        [Test]
        public void Units_OneCubeIsOneUnit_AndAGatCellIsHalfACube()
        {
            Assert.AreEqual(10f, RoUnits.CubeRoUnits);
            Assert.AreEqual(1f, RoUnits.CubeRoUnits * RoUnits.Scale, Eps);
            Assert.AreEqual(0.5f, RoUnits.GatCell, Eps);
            Assert.AreEqual(RoUnits.CubeRoUnits * RoUnits.Scale / 2f, RoUnits.GatCell, Eps);
        }

        [Test]
        public void Mirror_IsTheYReflection()
        {
            var expected = Matrix4x4.identity;
            expected.m11 = -1f;
            Assert.AreEqual(expected, RoSpace.Mirror);
            Assert.AreEqual(-1f, RoSpace.Mirror.determinant, Eps);
        }

        [Test]
        public void Point_OfACubeEdge_IsOneUnit()
        {
            AssertVector(new Vector3(1f, 0f, 0f), RoSpace.Point(new Vector3(10f, 0f, 0f)));
            AssertVector(new Vector3(0f, 0f, 1f), RoSpace.Point(new Vector3(0f, 0f, 10f)));
            Assert.AreEqual(1f, RoSpace.Point(new Vector3(0f, 10f, 0f)).magnitude, Eps);
        }

        // Each row: a Ragnarok point and the Unity point it must become.
        [TestCase(10f, 0f, 0f, 1f, 0f, 0f, TestName = "Point: +X stays +X, scaled")]
        [TestCase(0f, 10f, 0f, 0f, -1f, 0f, TestName = "Point: RO +Y (down) becomes Unity -Y (down)")]
        [TestCase(0f, -25f, 0f, 0f, 2.5f, 0f, TestName = "Point: RO -Y (up, e.g. a roof) becomes Unity +Y (up)")]
        [TestCase(0f, 0f, 10f, 0f, 0f, 1f, TestName = "Point: +Z stays +Z, scaled")]
        [TestCase(3f, -20f, 5f, 0.3f, 2f, 0.5f, TestName = "Point: mixed")]
        public void Point_KnownValues(float rx, float ry, float rz, float ux, float uy, float uz)
        {
            AssertVector(new Vector3(ux, uy, uz), RoSpace.Point(new Vector3(rx, ry, rz)));
        }

        [Test]
        public void Direction_FlipsYAndIsNotScaled()
        {
            AssertVector(new Vector3(10f, -10f, 10f), RoSpace.Direction(new Vector3(10f, 10f, 10f)));
            AssertVector(Vector3.up, RoSpace.Direction(Vector3.down), "an RO normal pointing up (-Y) points up in Unity");
        }

        [Test]
        public void Scale_KeepsEveryComponentAndItsSign()
        {
            AssertVector(new Vector3(-1f, 2f, 3f), RoSpace.Scale(new Vector3(-1f, 2f, 3f)));
            AssertVector(new Vector3(1f, -1f, 1f), RoSpace.Scale(new Vector3(1f, -1f, 1f)));
        }

        static readonly Vector3[] Eulers =
        {
            new Vector3(90f, 0f, 0f), new Vector3(0f, 45f, 0f), new Vector3(0f, 0f, 30f),
            new Vector3(10f, 20f, 30f), new Vector3(-75f, 160f, 5f),
        };

        [Test]
        public void Matrix_OfARotation_IsAProperRotationAndRoundTrips([ValueSource(nameof(Eulers))] Vector3 euler)
        {
            var ro = Matrix4x4.Rotate(Quaternion.Euler(euler));

            var unity = RoSpace.Matrix(ro);

            Assert.AreEqual(1f, unity.determinant, Eps, "conjugation keeps a rotation proper (no inside-out mesh)");
            AssertMatrix(ro, RoSpace.Matrix(unity), "F*(F*M*F)*F = M");
        }

        // Rotations by hand: conjugating with F = diag(1,-1,1) flips the sign of every entry with exactly one index equal to Y.
        // R_y has no such non-zero entry, so a turn about Y is unchanged; R_x and R_z each have two (the sines), so their
        // angle is negated. As a quaternion (x, y, z, w) this is (-x, y, -z, w).
        [TestCase(0f, 45f, 0f, 0f, 45f, 0f, TestName = "Rotation: about Y is unchanged")]
        [TestCase(90f, 0f, 0f, -90f, 0f, 0f, TestName = "Rotation: about X is negated")]
        [TestCase(0f, 0f, 30f, 0f, 0f, -30f, TestName = "Rotation: about Z is negated")]
        public void Rotation_KnownValues(float rx, float ry, float rz, float ux, float uy, float uz)
        {
            var ro = Matrix4x4.Rotate(Quaternion.Euler(rx, ry, rz));

            var q = RoSpace.Rotation(ro);

            AssertSameRotation(Quaternion.Euler(ux, uy, uz), q);
        }

        [Test]
        public void Rotation_AsQuaternion_NegatesXAndZ([ValueSource(nameof(Eulers))] Vector3 euler)
        {
            var roQ = Quaternion.Euler(euler);

            var q = RoSpace.Rotation(Matrix4x4.Rotate(roQ));

            AssertSameRotation(new Quaternion(-roQ.x, roQ.y, -roQ.z, roQ.w), q);
        }

        // The defining property: transforming in Ragnarok space and then converting equals converting and then transforming
        // in Unity space. This is what makes a placed model land and face where the reference viewer shows it.
        [Test]
        public void Conversion_CommutesWithTransforming([ValueSource(nameof(Eulers))] Vector3 euler)
        {
            var roLocal = Matrix4x4.TRS(new Vector3(12f, -30f, 7f), Quaternion.Euler(euler), new Vector3(1f, 2f, -1f));
            var roPoint = new Vector3(4f, -9f, 2.5f);
            var roDir = new Vector3(0.3f, -0.8f, 0.5f);

            var unity = RoSpace.Matrix(roLocal);

            AssertVector(RoSpace.Point(roLocal.MultiplyPoint3x4(roPoint)), unity.MultiplyPoint3x4(RoSpace.Point(roPoint)), "points");
            AssertVector(RoSpace.Direction(roLocal.MultiplyVector(roDir)), unity.MultiplyVector(RoSpace.Direction(roDir)), "directions");
            AssertVector(RoSpace.Direction(Matrix4x4.Rotate(Quaternion.Euler(euler)).MultiplyVector(roDir)),
                RoSpace.Rotation(Matrix4x4.Rotate(Quaternion.Euler(euler))) * RoSpace.Direction(roDir), "rotation as quaternion");
        }

        [Test]
        public void Matrix_ScalesTheTranslationLikeAPoint()
        {
            var roLocal = Matrix4x4.Translate(new Vector3(10f, 20f, -30f));

            var unity = RoSpace.Matrix(roLocal);

            AssertVector(RoSpace.Point(new Vector3(10f, 20f, -30f)), unity.GetColumn(3));
            Assert.AreEqual(1f, unity.m33, Eps);
        }

        [Test]
        public void Matrix_OfAMirroredScale_StaysMirrored()
        {
            var roLocal = Matrix4x4.Scale(new Vector3(-1f, 1f, 1f));

            Assert.AreEqual(-1f, RoSpace.Matrix(roLocal).determinant, Eps, "a mirrored model keeps its mirroring; winding is not corrected for scale");
        }

        [Test]
        public void ReverseWinding_SwapsSecondAndThirdOfEachTriple()
        {
            var indices = new[] { 0, 1, 2, 3, 4, 5, 7, 8, 9 };

            RoSpace.ReverseWinding(indices);

            CollectionAssert.AreEqual(new[] { 0, 2, 1, 3, 5, 4, 7, 9, 8 }, indices);
        }

        [Test]
        public void ReverseWinding_TwiceIsTheIdentity()
        {
            var original = new[] { 5, 9, 2, 0, 1, 3 };
            var indices = (int[])original.Clone();

            RoSpace.ReverseWinding(indices);
            RoSpace.ReverseWinding(indices);

            CollectionAssert.AreEqual(original, indices);
        }

        [Test]
        public void ReverseWinding_RejectsAnIncompleteTriangleList()
        {
            Assert.Throws<ArgumentException>(() => RoSpace.ReverseWinding(new[] { 0, 1, 2, 3 }));
            Assert.Throws<ArgumentNullException>(() => RoSpace.ReverseWinding(null));
        }

        static void AssertVector(Vector3 expected, Vector3 actual, string what = null)
        {
            Assert.That(Vector3.Distance(expected, actual), Is.LessThan(1e-4f), $"{what} expected {expected:F5} but was {actual:F5}");
        }

        static void AssertMatrix(Matrix4x4 expected, Matrix4x4 actual, string what)
        {
            for (int i = 0; i < 16; i++)
                Assert.AreEqual(expected[i], actual[i], Eps, $"{what}: element {i}");
        }

        static void AssertSameRotation(Quaternion expected, Quaternion actual)
        {
            // q and -q are the same rotation.
            Assert.That(Mathf.Abs(Quaternion.Dot(expected, actual)), Is.EqualTo(1f).Within(1e-5f), $"expected {expected:F5} but was {actual:F5}");
        }
    }
}
