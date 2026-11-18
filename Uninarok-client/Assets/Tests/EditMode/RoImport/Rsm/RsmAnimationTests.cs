using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Uninarok.RoImport.Core.Rsm;
using Uninarok.RoImport.Core.Space;

namespace Uninarok.RoImport.Tests.Rsm
{
    // TASKS-0003 T5.1 (FR-9): keys converted once in Core (PLAN-0003 4.8, D10).
    public class RsmAnimationTests
    {
        const float Eps = 1e-5f;

        static RsmRules R14
        {
            get
            {
                Assert.IsTrue(RsmRules.TryGet(1, 4, out var r));
                return r;
            }
        }

        static (RsmAnimationData anim, RsmRestPose rest) Build(RoRsm model, RsmRules rules = null)
        {
            Assert.IsTrue(RsmHierarchy.TryBuild(model, out var tree, out var issue), issue?.ToString());
            rules = rules ?? R14;
            var rest = RsmPose.ComputeRest(model, tree, rules);
            return (RsmAnimationData.Build(model, tree, rest, rules), rest);
        }

        static void AreEqual(Vector3 expected, Vector3 actual) =>
            Assert.That((expected - actual).magnitude, Is.LessThan(Eps), $"expected {expected:F5}, got {actual:F5}");

        [Test]
        public void Timing_IsMilliseconds()
        {
            Assert.AreEqual(0.001f, RsmTiming.SecondsPerUnit);
            Assert.AreEqual(16f, RsmTiming.Seconds(16000), Eps);
            Assert.AreEqual(0.16f, RsmTiming.Seconds(160), Eps);
        }

        [Test]
        public void StaticModel_HasNoLoops()
        {
            var (anim, _) = Build(RsmTestModels.ParentAndChild());
            Assert.IsFalse(anim.IsAnimated);
            CollectionAssert.IsEmpty(anim.Loops);
        }

        [Test]
        public void OnlyNodesWithKeys_GetCurves()
        {
            var (anim, _) = Build(RsmTestModels.TwoNodeAnimated());
            Assert.AreEqual(1, anim.Loops.Count);
            CollectionAssert.AreEqual(new[] { 1 }, anim.Loops[0].Nodes.Select(n => n.Node));
            Assert.AreEqual(5, anim.Loops[0].Nodes[0].Rotation.Length);
            Assert.IsEmpty(anim.Loops[0].Nodes[0].Position);
            Assert.IsEmpty(anim.Loops[0].Nodes[0].Scale);
        }

        [Test]
        public void KeyTimes_AreSeconds_AndTheLoopIsTheLastKeyTime()
        {
            var (anim, _) = Build(RsmTestModels.TwoNodeAnimated());
            var loop = anim.Loops[0];
            Assert.AreEqual(1000, loop.LengthUnits);
            Assert.AreEqual(1f, loop.LengthSeconds, Eps);
            CollectionAssert.AreEqual(new[] { 0f, 0.25f, 0.5f, 0.75f, 1f }, loop.Nodes[0].Rotation.Select(k => k.Time).ToArray());
        }

        [Test]
        public void RotationKeys_AreConjugated_XAndZNegated()
        {
            var model = RsmTestModels.TwoNodeAnimated();
            var q = new Quaternion(0.1f, 0.2f, 0.3f, 0.927f).normalized;
            model.Nodes[1].RotKeys = new[] { new RoRsmRotKey(0, q), new RoRsmRotKey(500, Quaternion.identity) };
            var key = Build(model).anim.Loops[0].Nodes[0].Rotation[0].Value;
            Assert.AreEqual(-q.x, key.x, Eps);
            Assert.AreEqual(q.y, key.y, Eps);
            Assert.AreEqual(-q.z, key.z, Eps);
            Assert.AreEqual(q.w, key.w, Eps);
            // and that is RoSpace's conjugation of the rotation
            Assert.AreEqual(1f, Mathf.Abs(Quaternion.Dot(RoSpace.Rotation(Matrix4x4.Rotate(q)), key)), Eps);
        }

        [Test]
        public void RotationKeys_KeepTheirFileSign()
        {
            // Two keys 174° apart (as in 라헬/분수); the second has a negative w and must not be flipped (D10).
            var model = RsmTestModels.TwoNodeAnimated();
            var second = new Quaternion(0f, 0f, 0.99863f, -0.05234f);
            model.Nodes[1].RotKeys = new[] { new RoRsmRotKey(0, Quaternion.identity), new RoRsmRotKey(800, second) };
            var keys = Build(model).anim.Loops[0].Nodes[0].Rotation;
            Assert.Less(keys[1].Value.w, 0f);
            Assert.AreEqual(-0.99863f, keys[1].Value.z, 1e-4f);
        }

        [Test]
        public void PositionKeys_ConvertThroughRoSpacePoint_AndRootsIncludeTheRecenter()
        {
            var root = RsmTestModels.Cube("root");
            root.PosKeys = new[] { new RoRsmPosKey(0, new Vector3(10, 20, 30)), new RoRsmPosKey(400, new Vector3(0, -5, 0)) };
            var child = RsmTestModels.Cube("child");
            child.ParentName = "root";
            child.PosKeys = new[] { new RoRsmPosKey(0, new Vector3(1, 2, 3)), new RoRsmPosKey(400, new Vector3(4, 5, 6)) };
            var (anim, rest) = Build(RsmTestModels.Model(root, child));
            var nodes = anim.Loops.Single().Nodes;
            var r = nodes.Single(n => n.Node == 0).Position;
            var c = nodes.Single(n => n.Node == 1).Position;
            AreEqual(RoSpace.Point(new Vector3(10, 20, 30) + rest.Recenter), r[0].Value);
            AreEqual(RoSpace.Point(new Vector3(0, -5, 0) + rest.Recenter), r[1].Value);
            AreEqual(RoSpace.Point(new Vector3(1, 2, 3)), c[0].Value);
            AreEqual(RoSpace.Point(new Vector3(4, 5, 6)), c[1].Value);
            // the first key is the rest pose (4.4 step 1)
            AreEqual(RsmSpace.LocalPosition(rest.Local[0]), r[0].Value);
        }

        [Test]
        public void ScaleKeys_AreUnchanged()
        {
            var rules = R14;
            rules.HasScaleKeys = true;   // a version with scale keys (2.x)
            var model = RsmTestModels.TwoNodeAnimated();
            model.Nodes[1].ScaleKeys = new[] { new RoRsmScaleKey(0, new Vector3(1, 2, 3)), new RoRsmScaleKey(1000, new Vector3(-1, 1, 1)) };
            var node = Build(model, rules).anim.Loops[0].Nodes[0];
            AreEqual(new Vector3(1, 2, 3), node.Scale[0].Value);
            AreEqual(new Vector3(-1, 1, 1), node.Scale[1].Value);
        }

        [Test]
        public void ScaleKeys_AreIgnored_WhenTheVersionHasNone()
        {
            var model = RsmTestModels.TwoNodeAnimated();
            model.Nodes[1].ScaleKeys = new[] { new RoRsmScaleKey(0, Vector3.one), new RoRsmScaleKey(1000, Vector3.one * 2) };
            Assert.IsEmpty(Build(model).anim.Loops[0].Nodes[0].Scale);
        }

        [Test]
        public void Nodes_AreGroupedByTheirLastKeyTime_Ascending()
        {
            // Like 라헬/분수: two loop lengths, 12800 and 19360, given here in reverse file order.
            var root = RsmTestModels.Cube("root");
            var slow = RsmTestModels.Cube("slow");
            slow.ParentName = "root";
            slow.RotKeys = new[] { new RoRsmRotKey(0, Quaternion.identity), new RoRsmRotKey(19360, Quaternion.identity) };
            var fast1 = RsmTestModels.Cube("fast1");
            fast1.ParentName = "root";
            fast1.RotKeys = new[] { new RoRsmRotKey(0, Quaternion.identity), new RoRsmRotKey(800, Quaternion.identity), new RoRsmRotKey(12800, Quaternion.identity) };
            var fast2 = RsmTestModels.Cube("fast2");
            fast2.ParentName = "root";
            fast2.RotKeys = new[] { new RoRsmRotKey(0, Quaternion.identity), new RoRsmRotKey(12800, Quaternion.identity) };
            var (anim, _) = Build(RsmTestModels.Model(root, slow, fast1, fast2));
            CollectionAssert.AreEqual(new[] { 12800, 19360 }, anim.Loops.Select(l => l.LengthUnits));
            CollectionAssert.AreEqual(new[] { 2, 3 }, anim.Loops[0].Nodes.Select(n => n.Node));
            CollectionAssert.AreEqual(new[] { 1 }, anim.Loops[1].Nodes.Select(n => n.Node));
            Assert.AreEqual(12.8f, anim.Loops[0].LengthSeconds, Eps);
        }

        [Test]
        public void ANodeWhoseLastKeyIsAtZero_HoldsItsFirstKey()
        {
            // It has no curve; its rest pose (the first key, 4.4 step 1) is its static transform.
            var model = RsmTestModels.TwoNodeAnimated();
            model.Nodes[1].RotKeys = new[] { new RoRsmRotKey(0, Quaternion.AngleAxis(90f, Vector3.forward)) };
            var (anim, rest) = Build(model);
            Assert.IsFalse(anim.IsAnimated);
            Assert.AreEqual(1f, Mathf.Abs(Quaternion.Dot(Quaternion.AngleAxis(90f, Vector3.forward), rest.Local[1].Rotation)), Eps);
        }

        [Test]
        public void KeysAreInTimeOrder()
        {
            var model = RsmTestModels.TwoNodeAnimated();
            model.Nodes[1].RotKeys = model.Nodes[1].RotKeys.Reverse().ToArray();
            var times = Build(model).anim.Loops[0].Nodes[0].Rotation.Select(k => k.Time).ToArray();
            CollectionAssert.IsOrdered(times);
        }
    }
}
