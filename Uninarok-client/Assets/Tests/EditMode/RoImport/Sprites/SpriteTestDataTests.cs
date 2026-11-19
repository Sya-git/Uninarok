using System.Linq;
using NUnit.Framework;
using Uninarok.RoImport.Core.Sprites;

namespace Uninarok.RoImport.Tests.Sprites
{
    // TASKS-0007 T1.1: every synthetic sample builds with the counts the later tests rely on (FR-1, FR-3, FR-8).
    public class SpriteTestDataTests
    {
        [Test]
        public void Monster_Has16Actions_InTwoAnimations_OfEightDirections()
        {
            var (spr, act) = SpriteTestData.Monster();

            Assert.AreEqual((8, 0), (spr.Indexed.Count, spr.TrueColor.Count));
            Assert.AreEqual(SpriteTestData.MonsterActions, act.Actions.Count);
            Assert.IsTrue(act.Actions.Take(8).All(a => a.Frames.Count == 2));
            Assert.IsTrue(act.Actions.Skip(8).All(a => a.Frames.Count == 3));
        }

        [Test]
        public void TrueColorEffect_HasOnlyTrueColourFrames_AndNoPalette()
        {
            var (spr, act) = SpriteTestData.TrueColorEffect();

            Assert.AreEqual((0, 3), (spr.Indexed.Count, spr.TrueColor.Count));
            Assert.IsNull(spr.Palette);
            Assert.IsTrue(spr.TrueColor.All(f => f.Rgba.Length == f.Width * f.Height * 4 && f.Indices == null));
            Assert.AreEqual(1, act.Actions.Count);
            Assert.AreEqual(3, act.Actions[0].Frames.Count);
        }

        [Test]
        public void Mixed_HasBothKinds_AndAFrameWithEveryLayerFieldSet()
        {
            var (spr, act) = SpriteTestData.Mixed();

            Assert.AreEqual((2, 2, 4), (spr.Indexed.Count, spr.TrueColor.Count, spr.SlotCount));
            Assert.AreSame(spr.TrueColor[1], spr.Slot(3));
            var top = act.Actions[0].Frames[0].Layers[1];
            Assert.AreEqual(RoActLayer.TrueColor, top.SpriteType);
            Assert.IsTrue(top.Mirror);
            Assert.AreEqual((0.5f, 2f, 90), (top.ScaleX, top.ScaleY, top.Rotation));
            Assert.AreEqual(200, top.Tint.a);
        }

        [Test]
        public void Weapon_Has104Actions_MostlyEmpty_WithAPartlyEmptyAttack()
        {
            var (_, act) = SpriteTestData.Weapon();

            Assert.AreEqual(SpriteTestData.WeaponActions, act.Actions.Count);
            int empty = act.Actions.Count(a => a.Frames.All(f => f.Layers.All(l => l.IsEmpty)));
            Assert.AreEqual(104 - 16, empty, "all but standby and attack are empty");
            var attack = act.Actions[10 * 8];
            Assert.AreEqual(3, attack.Frames.Count);
            Assert.IsTrue(attack.Frames[1].Layers.All(l => l.IsEmpty));
            Assert.IsFalse(attack.Frames[0].Layers.All(l => l.IsEmpty));
        }

        [Test]
        public void Static_IsOneActionOfOneFrameAndOneLayer()
        {
            var (spr, act) = SpriteTestData.Static(0, -7);

            Assert.AreEqual(1, spr.SlotCount);
            Assert.AreEqual(1, act.Actions.Count);
            Assert.AreEqual(1, act.Actions[0].Frames.Count);
            Assert.AreEqual((0, -7), (act.Actions[0].Frames[0].Layers[0].OffsetX, act.Actions[0].Frames[0].Layers[0].OffsetY));
        }

        [Test]
        public void Anchored_HasOneAnchorPerFrame()
        {
            var (_, act) = SpriteTestData.Anchored();

            Assert.AreEqual(8, act.Actions.Count);
            Assert.IsTrue(act.Actions.SelectMany(a => a.Frames).All(f => f.Anchors.Count == 1));
        }

        [Test]
        public void WithEvents_NamesItsEvents_FromFrames()
        {
            var (_, act) = SpriteTestData.WithEvents();

            CollectionAssert.AreEqual(new[] { "atk", "x_move.wav" }, act.Events);
            Assert.AreEqual(1, act.Actions[0].Frames[0].EventId);
            Assert.AreEqual(0, act.Actions[8].Frames[1].EventId);
            Assert.AreEqual(-1, act.Actions[8].Frames[0].EventId);
        }

        [Test]
        public void OutOfRange_PointsPastItsSheet()
        {
            var (spr, act) = SpriteTestData.OutOfRange();

            Assert.Greater(act.Actions[1].Frames[0].Layers[0].SpriteIndex, spr.Indexed.Count - 1);
        }

        [Test]
        public void Hundred_Has100Frames_AndOneActionPlayingThem()
        {
            var (spr, act) = SpriteTestData.Hundred();

            Assert.AreEqual(100, spr.SlotCount);
            Assert.AreEqual(100, act.Actions[0].Frames.Count);
        }
    }
}
