using System.Linq;
using NUnit.Framework;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Rsm;

namespace Uninarok.RoImport.Tests.Rsm
{
    // TASKS-0003 T2.1 (FR-1): parents are resolved by name, independent of file order (PLAN-0003 4.2).
    public class RsmHierarchyTests
    {
        static RoRsmNode N(string name, string parent = "") => new RoRsmNode { Name = name, ParentName = parent };

        static RsmTree Build(params RoRsmNode[] nodes)
        {
            Assert.IsTrue(RsmHierarchy.TryBuild(RsmTestModels.Model(nodes), out var tree, out var issue), issue?.ToString());
            Assert.IsNotNull(tree);
            return tree;
        }

        [Test]
        public void ChildBeforeParent_GivesTheCorrectTreeAndOrder()
        {
            // file order: grandchild, child, root
            var tree = Build(N("leaf", "mid"), N("mid", "root"), N("root"));
            CollectionAssert.AreEqual(new[] { 2 }, tree.Roots);
            CollectionAssert.AreEqual(new[] { 1, 2, -1 }, tree.Parent);
            CollectionAssert.AreEqual(new[] { 2, 1, 0 }, tree.Order);
            CollectionAssert.AreEqual(new[] { 1 }, tree.Children[2]);
            CollectionAssert.AreEqual(new[] { 0 }, tree.Children[1]);
            CollectionAssert.IsEmpty(tree.Children[0]);
            CollectionAssert.IsEmpty(tree.Warnings);
        }

        [Test]
        public void Order_PutsEveryParentBeforeItsChildren()
        {
            var tree = Build(N("d", "b"), N("c", "a"), N("b", "a"), N("a"), N("e", "d"));
            for (int i = 0; i < tree.Parent.Length; i++)
                if (tree.Parent[i] >= 0)
                    Assert.Less(tree.Order.ToList().IndexOf(tree.Parent[i]), tree.Order.ToList().IndexOf(i), $"node {i}");
        }

        [Test]
        public void Order_IsStable_FileOrderAmongNodesReadyTogether()
        {
            // a is the root; c, b and d are its children in file order 1, 2, 3.
            var tree = Build(N("a"), N("c", "a"), N("b", "a"), N("d", "a"));
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, tree.Order);
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, tree.Children[0]);
        }

        [Test]
        public void Names_AreKept()
        {
            var tree = Build(N("Mesh02"), N("Box05", "Mesh02"), N("나무", "Mesh02"));
            CollectionAssert.AreEqual(new[] { "Mesh02", "Box05", "나무" }, tree.UniqueNames);
        }

        [Test]
        public void DuplicateNames_GetSuffixes_AndOneWarning()
        {
            var tree = Build(N("root"), N("leaf", "root"), N("leaf", "root"), N("leaf", "root"));
            CollectionAssert.AreEqual(new[] { "root", "leaf", "leaf_2", "leaf_3" }, tree.UniqueNames);
            Assert.AreEqual(1, tree.Warnings.Count);
            Assert.AreEqual(RoIssueCode.RsmDuplicateName, tree.Warnings[0].Code);
            StringAssert.Contains("leaf", tree.Warnings[0].Item);
        }

        [Test]
        public void DuplicateNames_DoNotCollideWithAnExistingSuffixedName()
        {
            var tree = Build(N("root"), N("leaf_2", "root"), N("leaf", "root"), N("leaf", "root"));
            Assert.AreEqual(tree.UniqueNames.Length, tree.UniqueNames.Distinct().Count(), string.Join(",", tree.UniqueNames));
            Assert.AreEqual("leaf_2", tree.UniqueNames[1]);
            Assert.AreEqual("leaf", tree.UniqueNames[2]);
        }

        [Test]
        public void DuplicateNames_ChildrenAttachToTheFirstNodeOfThatName()
        {
            var tree = Build(N("root"), N("x", "root"), N("x", "root"), N("child", "x"));
            Assert.AreEqual(1, tree.Parent[3]);
        }

        [Test]
        public void MissingParent_MakesARoot_WithOneWarning()
        {
            var tree = Build(N("root"), N("orphan", "nobody"), N("child", "root"));
            CollectionAssert.AreEqual(new[] { 0, 1 }, tree.Roots);
            Assert.AreEqual(-1, tree.Parent[1]);
            Assert.AreEqual(1, tree.Warnings.Count);
            Assert.AreEqual(RoIssueCode.RsmParentMissing, tree.Warnings[0].Code);
            Assert.AreEqual("orphan", tree.Warnings[0].Item);
        }

        [Test]
        public void SelfParent_MakesARoot_WithOneWarning()
        {
            var tree = Build(N("root", "root"), N("child", "root"));
            CollectionAssert.AreEqual(new[] { 0 }, tree.Roots);
            Assert.AreEqual(0, tree.Parent[1]);
            Assert.AreEqual(1, tree.Warnings.Count);
            Assert.AreEqual(RoIssueCode.RsmParentMissing, tree.Warnings[0].Code);
        }

        [Test]
        public void Cycle_FailsWithOneIssue()
        {
            var model = RsmTestModels.Model(N("root"), N("a", "b"), N("b", "a"));
            Assert.IsFalse(RsmHierarchy.TryBuild(model, out var tree, out var issue));
            Assert.IsNull(tree);
            Assert.IsNotNull(issue);
            Assert.AreEqual(RoIssueCode.RsmCycle, issue.Code);
            StringAssert.Contains("a", issue.Message);
            StringAssert.Contains("b", issue.Message);
        }

        [Test]
        public void SeveralRoots_AreAllListed()
        {
            var tree = Build(N("r1"), N("c1", "r1"), N("r2"), N("c2", "r2"), N("r3"));
            CollectionAssert.AreEqual(new[] { 0, 2, 4 }, tree.Roots);
            CollectionAssert.IsEmpty(tree.Warnings);
            Assert.AreEqual(5, tree.Order.Count);
        }

        [Test]
        public void NullParentName_IsARoot()
        {
            var tree = Build(new RoRsmNode { Name = "a", ParentName = null });
            CollectionAssert.AreEqual(new[] { 0 }, tree.Roots);
        }

        [Test]
        public void EmptyModel_GivesAnEmptyTree()
        {
            Assert.IsTrue(RsmHierarchy.TryBuild(RsmTestModels.Model(), out var tree, out _));
            CollectionAssert.IsEmpty(tree.Order);
            CollectionAssert.IsEmpty(tree.Roots);
        }

        [Test]
        public void TestModels_ParentAndChild_HasOneRootAndOneChild()
        {
            Assert.IsTrue(RsmHierarchy.TryBuild(RsmTestModels.ParentAndChild(), out var tree, out _));
            CollectionAssert.AreEqual(new[] { 0 }, tree.Roots);
            CollectionAssert.AreEqual(new[] { -1, 0 }, tree.Parent);
        }
    }
}
