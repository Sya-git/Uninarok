using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Uninarok.RoImport.Core.Sprites;
using Uninarok.Sprites;

namespace Uninarok.RoImport.Tests.Sprites
{
    // TASKS-0007 T4.1 / T4.2 (FR-3, FR-4, FR-5, FR-6, plan 4.5): an ACT flattened into the arrays of a SpriteAnimation,
    // identical actions stored once.
    public class RoActCompilerTests
    {
        static RoCompiledAct Compile((RoSpr spr, RoAct act) sample)
        {
            bool ok = RoActCompiler.TryCompile(sample.act, sample.spr.Indexed.Count, sample.spr.TrueColor.Count, out var c, out var reason);
            Assert.IsTrue(ok, reason);
            return c;
        }

        static SpriteAnimation.FrameData FrameOf(RoCompiledAct c, int fileAction, int frame) =>
            c.Frames[c.Actions[c.ActionMap[fileAction]].FirstFrame + frame];

        static SpriteAnimation.LayerData LayerOf(RoCompiledAct c, int fileAction, int frame, int layer) =>
            c.Layers[FrameOf(c, fileAction, frame).FirstLayer + layer];

        [Test]
        public void IdenticalActions_AreStoredOnce_AndMapped()
        {
            var c = Compile(SpriteTestData.Monster());

            Assert.AreEqual(SpriteTestData.MonsterActions, c.FileActionCount);
            Assert.AreEqual(SpriteTestData.MonsterActions, c.ActionMap.Length);
            Assert.AreEqual(SpriteTestData.MonsterDistinctActions, c.Actions.Length, "directions repeat in pairs");
            for (int a = 0; a < 16; a += 2) Assert.AreEqual(c.ActionMap[a], c.ActionMap[a + 1], "direction pair " + a);
            Assert.AreNotEqual(c.ActionMap[0], c.ActionMap[4], "a mirrored pair is a different action");
            Assert.AreEqual(c.Actions.Sum(a => a.FrameCount), c.Frames.Length, "only unique actions' frames are stored");
        }

        [Test]
        public void FrameTime_IsSpeedTimes25()
        {
            var c = Compile(SpriteTestData.Monster());

            Assert.AreEqual(100f, c.Actions[c.ActionMap[0]].FrameMs, 1e-4f);
            Assert.AreEqual(62.5f, c.Actions[c.ActionMap[8]].FrameMs, 1e-4f);
        }

        [Test]
        public void Flattening_KeepsEveryLayerField_AndComputesTheSlot()
        {
            var c = Compile(SpriteTestData.Mixed());

            var under = LayerOf(c, 0, 0, 0);
            Assert.AreEqual((1, -3, -10), ((int)under.Slot, (int)under.OffsetX, (int)under.OffsetY));
            var top = LayerOf(c, 0, 0, 1);
            Assert.AreEqual(2, top.Slot, "true colour 0 comes after the 2 indexed frames");
            Assert.AreEqual((5, -12), ((int)top.OffsetX, (int)top.OffsetY));
            Assert.IsTrue(top.Mirror);
            Assert.AreEqual((0.5f, 2f, 90f), (top.ScaleX, top.ScaleY, top.Rotation));
            Assert.AreEqual(new Color32(255, 128, 64, 200), top.Tint);
            Assert.AreEqual(2, FrameOf(c, 0, 0).LayerCount, "draw order kept");
            Assert.AreEqual(2, c.MaxLayers);
        }

        [Test]
        public void EmptyActionsAndPartlyEmptyFrames_AreKept()
        {
            var c = Compile(SpriteTestData.Weapon());

            Assert.AreEqual(104, c.FileActionCount);
            Assert.IsTrue(c.Directional);
            Assert.AreEqual(3, c.Actions.Length, "the empty action, standby and attack");
            var idle = c.Actions[c.ActionMap[0]];
            Assert.AreEqual(1, idle.FrameCount, "the empty action is data, not a gap");
            Assert.AreEqual(-1, LayerOf(c, 0, 0, 0).Slot);
            Assert.AreEqual(c.ActionMap[0], c.ActionMap[8], "every empty action is one");

            int attack = 10 * 8;
            Assert.AreEqual(3, c.Actions[c.ActionMap[attack]].FrameCount);
            Assert.AreEqual(-1, LayerOf(c, attack, 1, 0).Slot, "the hidden attack frame stays, empty");
            Assert.AreEqual(2, LayerOf(c, attack, 0, 0).Slot);
        }

        [Test]
        public void NonDirectionalFiles_AreFlagged()
        {
            Assert.IsFalse(Compile(SpriteTestData.TrueColorEffect()).Directional);
            Assert.IsFalse(Compile(SpriteTestData.Static()).Directional);
            Assert.IsTrue(Compile(SpriteTestData.Anchored()).Directional);
        }

        [Test]
        public void EventsAndAnchors_AreKept()
        {
            var events = Compile(SpriteTestData.WithEvents());
            CollectionAssert.AreEqual(new[] { "atk", "x_move.wav" }, events.Events);
            Assert.AreEqual(1, FrameOf(events, 0, 0).EventId);
            Assert.AreEqual(-1, FrameOf(events, 0, 1).EventId);
            Assert.AreEqual(0, FrameOf(events, 8, 1).EventId);

            var anchored = Compile(SpriteTestData.Anchored());
            var f1 = FrameOf(anchored, 3, 1);
            Assert.AreEqual(1, f1.AnchorCount);
            Assert.AreEqual(new Vector2Int(1, -57), anchored.Anchors[f1.FirstAnchor]);
            Assert.AreEqual(1, anchored.Actions.Length, "8 identical directions");
        }

        [Test]
        public void ALayerOutsideItsSheet_IsRejected_WithWhere()
        {
            var (spr, act) = SpriteTestData.OutOfRange();

            Assert.IsFalse(RoActCompiler.TryCompile(act, spr.Indexed.Count, spr.TrueColor.Count, out var c, out var reason));
            Assert.IsNull(c);
            StringAssert.Contains("action 1", reason);
            StringAssert.Contains("5", reason);
        }

        [Test]
        public void ATrueColourLayer_IsCheckedAgainstTheTrueColourFrames()
        {
            var act = SpriteTestData.Act(new[] { SpriteTestData.Action(4f, SpriteTestData.Frame(RoActLayer.Simple(1, 0, 0, RoActLayer.TrueColor))) });

            Assert.IsFalse(RoActCompiler.TryCompile(act, 5, 1, out _, out _), "true colour 1 of 1 frame");
            Assert.IsTrue(RoActCompiler.TryCompile(act, 0, 2, out var c, out _));
            Assert.AreEqual(1, c.Layers[0].Slot);
        }
    }
}
