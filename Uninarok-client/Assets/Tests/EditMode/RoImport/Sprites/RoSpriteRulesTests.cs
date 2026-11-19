using NUnit.Framework;
using UnityEngine;
using Uninarok.RoImport.Core.Space;
using Uninarok.RoImport.Core.Sprites;

namespace Uninarok.RoImport.Tests.Sprites
{
    // TASKS-0007 T4.1 / T4.2 (FR-3, FR-5, FR-6, plan 4.3): the rules table pinned with the values of the rules check (T0.2).
    // A rules change edits this table and these tests only, and raises RoSpriteConverter.RulesVersion.
    [Ignore("pending T4.2")]
    public class RoSpriteRulesTests
    {
        [TestCase(4f, 100f)]
        [TestCase(2.5f, 62.5f)]
        [TestCase(1f, 25f)]
        [TestCase(8f, 200f)]
        public void FrameTime_IsAnimationSpeedTimes25Ms(float speed, float ms)
        {
            Assert.AreEqual(ms, RoSpriteRules.FrameMs(speed), 1e-4f);
            Assert.AreEqual(25f, RoSpriteRules.FrameUnitMs);
        }

        [TestCase(104, true)]
        [TestCase(40, true)]
        [TestCase(8, true)]
        [TestCase(98, false)]
        [TestCase(41, false)]
        [TestCase(1, false)]
        [TestCase(0, false)]
        public void AFileIsDirectional_WhenItsActionCountIsAMultipleOf8(int actions, bool directional)
        {
            Assert.AreEqual(directional, RoSpriteRules.IsDirectional(actions));
        }

        [Test]
        public void PixelsPerUnit_Is35PixelsPerGatCell()
        {
            Assert.AreEqual(35f, RoSpriteRules.PixelsPerGatCell);
            Assert.AreEqual(35f / RoUnits.GatCell, RoSpriteRules.PixelsPerUnit, 1e-4f);
            Assert.AreEqual(70f, RoSpriteRules.PixelsPerUnit, 1e-4f, "one GAT cell is half a Unity unit");
        }

        [Test]
        public void TheLayerRules_AreTheVerifiedOnes()
        {
            Assert.IsTrue(RoSpriteRules.OffsetYDown, "offset: frame centre, X right, Y down");
            Assert.IsTrue(RoSpriteRules.MirrorAboutLayerCentre);
            Assert.IsTrue(RoSpriteRules.RotationClockwise, "degrees, clockwise on screen");
            Assert.IsTrue(RoSpriteRules.ScaleBeforeRotation);
            Assert.IsTrue(RoSpriteRules.TintMultipliesAlphaIncluded);
            Assert.IsTrue(RoSpriteRules.PaletteIndex0Transparent);
            Assert.IsTrue(RoSpriteRules.LaterLayersOnTop);
            Assert.IsTrue(RoSpriteRules.EveryActionLoops);
        }

        [TestCase(0, 5, 10, 5)]
        [TestCase(1, 2, 10, 12)]
        [TestCase(1, 0, 0, 0)]
        [TestCase(0, -1, 10, -1)]
        [TestCase(1, -1, 10, -1)]
        public void TheSlot_CountsIndexedFramesFirst(int type, int index, int indexedCount, int slot)
        {
            Assert.AreEqual(slot, RoSpriteRules.Slot(type, index, indexedCount));
        }

        [Test]
        public void StaticSprites_AreOneActionOfOneFrame_WithAtMostOneDrawnLayer()
        {
            Assert.IsTrue(RoSpriteRules.IsStatic(SpriteTestData.Static().act));
            Assert.IsFalse(RoSpriteRules.IsStatic(SpriteTestData.TrueColorEffect().act), "one action of 3 frames");
            Assert.IsFalse(RoSpriteRules.IsStatic(SpriteTestData.Monster().act));
            var twoLayers = SpriteTestData.Act(new[] { SpriteTestData.Action(4f, SpriteTestData.Frame(RoActLayer.Simple(0), RoActLayer.Simple(1))) });
            Assert.IsFalse(RoSpriteRules.IsStatic(twoLayers));
            var withEmpty = SpriteTestData.Act(new[] { SpriteTestData.Action(4f, SpriteTestData.Frame(RoActLayer.Empty, RoActLayer.Simple(0))) });
            Assert.IsTrue(RoSpriteRules.IsStatic(withEmpty), "an empty layer does not count");
        }

        [Test]
        public void AStaticSpritesPivot_PutsTheOriginWhereTheOffsetSays()
        {
            Assert.AreEqual(new Vector2(0.5f, 0.5f), RoSpriteRules.StaticPivot(14, 16, 0, 0));
            var p = RoSpriteRules.StaticPivot(14, 16, 4, -7);   // centre 4 right of and 7 above the origin
            Assert.AreEqual((7f - 4f) / 14f, p.x, 1e-6f);
            Assert.AreEqual((8f - 7f) / 16f, p.y, 1e-6f);
        }
    }
}
