using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Uninarok.Sprites.Tests
{
    // TASKS-0007 T2.1 (FR-3, FR-5, plan 4.5): actions are addressed by animation and direction; an action the file has is
    // returned even when empty, a missing one follows the caller's policy.
    public class SpriteAnimationLookupTests
    {
        SpriteAnimation _anim;

        [TearDown]
        public void TearDown()
        {
            if (_anim != null) Object.DestroyImmediate(_anim);
        }

        /// <summary>An animation whose file action i maps to unique action i (so the result names the file action).</summary>
        SpriteAnimation Make(int fileActions, bool directional)
        {
            _anim = ScriptableObject.CreateInstance<SpriteAnimation>();
            _anim.FileActionCount = fileActions;
            _anim.Directional = directional;
            _anim.ActionMap = Enumerable.Range(0, fileActions).Select(i => (short)i).ToArray();
            _anim.Actions = new SpriteAnimation.ActionData[fileActions];
            return _anim;
        }

        [Test]
        public void InsideTheCount_ReturnsAnimationTimes8PlusDirection()
        {
            var a = Make(104, true);

            Assert.IsTrue(a.TryGetAction(1, 3, SpriteMissingAction.DrawNothing, out int u));
            Assert.AreEqual(11, u);
            Assert.IsTrue(a.TryGetAction(12, 7, SpriteMissingAction.DrawNothing, out u));
            Assert.AreEqual(103, u);
        }

        [Test]
        public void AnEmptyActionInsideTheCount_IsReturned_NotMissing()
        {
            var a = Make(16, true);
            a.ActionMap[9] = 0;   // walk direction 1 shares unique action 0, which may be all empty: still data (FR-5)

            Assert.IsTrue(a.TryGetAction(1, 1, SpriteMissingAction.DrawNothing, out int u));
            Assert.AreEqual(0, u);
        }

        [Test]
        public void DuplicateActions_MapToOneUniqueAction()
        {
            var a = Make(16, true);
            for (int i = 0; i < 16; i++) a.ActionMap[i] = (short)(i / 2);

            Assert.IsTrue(a.TryGetAction(0, 0, SpriteMissingAction.DrawNothing, out int d0));
            Assert.IsTrue(a.TryGetAction(0, 1, SpriteMissingAction.DrawNothing, out int d1));
            Assert.AreEqual(d0, d1);
        }

        [Test]
        public void OutsideTheCount_DrawNothing_ReturnsFalse()
        {
            var a = Make(8, true);

            Assert.IsFalse(a.TryGetAction(1, 3, SpriteMissingAction.DrawNothing, out int u));
            Assert.AreEqual(-1, u);
        }

        [Test]
        public void OutsideTheCount_Fallback_UsesIdleInTheRequestedDirection()
        {
            var a = Make(8, true);   // a one-animation NPC asked for walk, direction 3 (spec FR-5 acceptance)

            Assert.IsTrue(a.TryGetAction(1, 3, SpriteMissingAction.Fallback, out int u));
            Assert.AreEqual(3, u);
        }

        [Test]
        public void Fallback_PrefersTheSameAnimationInDirection0()
        {
            var a = Make(20, true);  // animation 2 has directions 0 to 3 only

            Assert.IsTrue(a.TryGetAction(2, 6, SpriteMissingAction.Fallback, out int u));
            Assert.AreEqual(16, u);
        }

        [Test]
        public void Fallback_EndsWithTheFirstAction()
        {
            var a = Make(3, false);

            Assert.IsTrue(a.TryGetAction(9, 5, SpriteMissingAction.Fallback, out int u));
            Assert.AreEqual(0, u);
        }

        [Test]
        public void NonDirectionalFiles_IgnoreTheDirection()
        {
            var a = Make(98, false);

            Assert.IsTrue(a.TryGetAction(5, 0, SpriteMissingAction.DrawNothing, out int u0));
            Assert.IsTrue(a.TryGetAction(5, 6, SpriteMissingAction.DrawNothing, out int u6));
            Assert.AreEqual((5, 5), (u0, u6));
            Assert.AreEqual(98, a.AnimationCount);
        }

        [Test]
        public void NegativeAndLargeDirections_WrapAround()
        {
            var a = Make(16, true);

            Assert.IsTrue(a.TryGetAction(1, -1, SpriteMissingAction.DrawNothing, out int u));
            Assert.AreEqual(15, u);
            Assert.IsTrue(a.TryGetAction(0, 9, SpriteMissingAction.DrawNothing, out u));
            Assert.AreEqual(1, u);
        }

        [Test]
        public void AnEmptyFile_HasNoAction_WhateverThePolicy()
        {
            var a = Make(0, true);

            Assert.IsFalse(a.TryGetAction(0, 0, SpriteMissingAction.Fallback, out _));
        }
    }
}
