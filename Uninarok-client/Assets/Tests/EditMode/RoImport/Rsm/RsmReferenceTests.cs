using NUnit.Framework;
using UnityEngine;
using Uninarok.RoImport.Core.Formats;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Rsm;
using Uninarok.RoImport.Core.Space;

namespace Uninarok.RoImport.Tests.Rsm
{
    // TASKS-0003 T3.3 (FR-2): the four reported models and the control group (reference-models.md §1, §2) against the
    // R1 reference values recorded in PLAN-0003 4.4 ("R1 reference values"), computed by the T0.3 scratch code that
    // matched the reference viewers. Ragnarok space, after recentering.
    [Category(RsmTestData.Category)]
    public class RsmReferenceTests
    {
        const float Eps = 1e-4f;

        public sealed class Expected
        {
            public string Model;
            public Vector3 Position, Scale, Min, Max;
            public Quaternion Rotation;
            public override string ToString() => Model;
        }

        static Expected E(string model, Vector3 position, Quaternion rotation, Vector3 scale, Vector3 min, Vector3 max) =>
            new Expected { Model = model, Position = position, Rotation = rotation, Scale = scale, Min = min, Max = max };

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        static Quaternion Q(float x, float y, float z, float w) => new Quaternion(x, y, z, w);

        static Expected[] RestValues() => new[]
        {
            // §1 reported wrong
            E("나무잡초꽃/버섯02.rsm", V(0.046991f, 1.665913f, -0.145184f), Q(0f, -0.923878f, 0f, -0.382688f), V(0.0901f, 0.0901f, 0.0901f), V(-4.358802f, -5.739874f, -3.774851f), V(4.358802f, 0f, 3.774849f)),
            E("나무잡초꽃/버섯01.rsm", V(0.046991f, 1.665913f, -0.145184f), Q(0f, -0.923878f, 0f, -0.382688f), V(0.0901f, 0.0901f, 0.0901f), V(-4.358802f, -5.739874f, -3.774851f), V(4.358802f, 0f, 3.774849f)),
            E("나무잡초꽃/넝쿨01.rsm", V(0.226999f, -40.2408f, -0.000001f), Q(0.707108f, 0f, 0f, 0.707106f), V(1f, 1f, 1f), V(-20f, -80f, -0.000148f), V(20f, 0f, 0.000148f)),
            E("나무잡초꽃/덤불02.rsm", V(-8.305627f, 17.98348f, -3.509289f), Q(0.02982f, -0.208807f, 0.056931f, 0.975843f), V(0.1811f, 0.1811f, 0.1811f), V(-25.04809f, -14.98955f, -24.90288f), V(25.04809f, 0f, 24.90288f)),
            // §2 control group
            E("나무잡초꽃/덤불01.rsm", V(-8.305627f, 19.50137f, -1.240438f), Q(0.02982f, -0.208807f, 0.056931f, 0.975843f), V(0.1811f, 0.1811f, 0.1811f), V(-25.04809f, -13.47166f, -22.63403f), V(25.04809f, 0f, 22.63403f)),
            E("나무잡초꽃/야자수01.rsm", V(4.419601f, 0f, -8.387604f), Q(0f, -0.500001f, 0f, 0.866025f), V(1f, 1f, 1f), V(-21.95475f, -67.95193f, -21.04791f), V(21.95475f, 0f, 21.04791f)),
            E("프론테라/성_기둥.rsm", V(0.155977f, -193.0966f, 3.696754f), Q(0f, 0.831471f, 0f, 0.555568f), V(0.2658f, 0.2395f, 0.2658f), V(-17.86841f, -102.102f, -19.08291f), V(17.86841f, 0f, 19.08291f)),
            E("프론테라/휘장.rsm", V(2.689514f, -7.367931f, -0.000003f), Q(0.707108f, 0f, 0f, 0.707106f), V(0.6908f, 0.6908f, 0.6908f), V(-5.785612f, -24.3201f, -0.000045f), V(5.785612f, 0f, 0.000045f)),
            E("외부소품/이정표01.rsm", V(-0.183999f, -15.70174f, 6.458787f), Q(0.707108f, 0f, 0f, 0.707106f), V(1f, 1f, 1f), V(-21.9134f, -24.42023f, -3.504345f), V(21.9134f, 0f, 3.504345f)),
        };

        static void Near(Vector3 expected, Vector3 actual, string what) =>
            Assert.That((expected - actual).magnitude, Is.LessThan(Eps), $"{what}: expected {expected:F6}, got {actual:F6}");

        [TestCaseSource(nameof(RestValues))]
        public void RootRestTransformAndBounds_MatchTheRecordedValues(Expected e)
        {
            byte[] bytes = RsmTestData.ModelBytes(e.Model);
            Assert.IsTrue(RoRsmReader.TryRead(RoPath.Parse("model/" + e.Model), bytes, out var rsm, out var reason), reason);
            Assert.IsTrue(RsmRules.TryGet(rsm.VersionMajor, rsm.VersionMinor, out var rules));
            Assert.IsTrue(RsmHierarchy.TryBuild(rsm, out var tree, out var issue), issue?.ToString());
            var rest = RsmPose.ComputeRest(rsm, tree, rules);

            Assert.AreEqual(1, tree.Roots.Count);
            var root = rest.Local[tree.Roots[0]];
            Near(e.Position, root.Position, "root position");
            Assert.That(Mathf.Abs(Quaternion.Dot(e.Rotation.normalized, root.Rotation)), Is.GreaterThan(1f - Eps), $"root rotation {root.Rotation:F6}");
            Near(e.Scale, root.Scale, "root scale");
            Near(e.Min, rest.BoundsMin, "bounds min");
            Near(e.Max, rest.BoundsMax, "bounds max");

            // The converted transform is RoSpace of the same values (4.5).
            Near(RoSpace.Point(e.Position), RsmSpace.LocalPosition(root), "converted position");
            Near(e.Scale, RsmSpace.LocalScale(root), "converted scale");
        }
    }
}
