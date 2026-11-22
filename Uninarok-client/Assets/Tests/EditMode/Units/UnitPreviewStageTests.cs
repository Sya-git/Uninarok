using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Uninarok.Networking.Data;
using Uninarok.Units.Editor;

namespace Uninarok.Units.Tests
{
    // TASKS-0009 T7.3, T7.4 (FR-15; PLAN-0009 4.9): what the composer window shows, through its stage.
    [Category("NeedsData")]
    public class UnitPreviewStageTests
    {
        UnitPreviewStage _stage;
        AssetDatabaseUnitAssetSource _source;

        [SetUp]
        public void SetUp()
        {
            _source = UnitTestData.CatalogOrIgnore();
            _stage = new UnitPreviewStage(_source);
        }

        [TearDown]
        public void TearDown()
        {
            _stage?.Dispose();
            if (_source != null) Assert.That(_source.HeldCount, Is.EqualTo(0), "closing the stage releases everything");
        }

        UnitPreviewStage.PartRow Row(PartSlot slot) => _stage.Rows().Single(r => r.Slot == slot);

        [Test]
        public void Swordsman_PlaysAttack3_SteppingAdvancesOneBodyFrame()
        {
            _stage.SetLook(UnitTestData.Swordsman());
            _stage.View.Play(PcAnimation.Attack3);
            _stage.Step(0f);
            int count = _stage.BodyAction.FrameCount;
            Assert.That(count, Is.GreaterThan(1));
            for (int i = 1; i <= count + 2; i++)
            {
                _stage.StepFrame();
                Assert.That(_stage.View.Frame, Is.EqualTo(i % count));
            }
            _stage.SetFrame(2);
            Assert.That(_stage.View.Frame, Is.EqualTo(2));
        }

        [Test]
        public void TurningThePreview180_ShowsTheBackOfAUnitFacingSouth()
        {
            _stage.SetLook(UnitTestData.Swordsman());
            _stage.View.Facing = UninarokDirection.South;
            _stage.Step(0f);
            Assert.That(_stage.View.ShownDirection, Is.EqualTo(0));
            _stage.Yaw = 22f;
            _stage.Step(0f);
            Assert.That(_stage.View.ShownDirection, Is.EqualTo(0));
            _stage.Yaw = 23f;
            _stage.Step(0f);
            Assert.That(_stage.View.ShownDirection, Is.EqualTo(7));
            _stage.Yaw = 180f;
            _stage.Step(0f);
            Assert.That(_stage.View.ShownDirection, Is.EqualTo(4));
        }

        [Test]
        public void Parts_BodyAndWeaponFramesInLockStep()
        {
            _stage.SetLook(UnitTestData.Swordsman());
            foreach (var animation in new[] { PcAnimation.Attack2, PcAnimation.Standby, PcAnimation.Walk })
            {
                _stage.View.Play(animation);
                _stage.Step(0f);
                for (int i = 0; i < 30; i++)
                {
                    _stage.StepFrame();
                    var body = Row(PartSlot.Body);
                    foreach (var slot in new[] { PartSlot.Weapon, PartSlot.WeaponTrail, PartSlot.Head, PartSlot.Garment })
                    {
                        var row = Row(slot);
                        Assert.That(row.Frame, Is.EqualTo(body.Frame % row.FrameCount), $"{animation} {slot}");
                    }
                }
                Assert.That(Row(PartSlot.WeaponTrail).OwnFrameMs, Is.Not.EqualTo(Row(PartSlot.Body).OwnFrameMs), "the trail's own time differs and is ignored");
            }
        }

        [Test]
        public void HidingTheGarment_RemovesOnlyIt()
        {
            _stage.SetLook(UnitTestData.Swordsman());
            _stage.View.SetPartHidden(PartSlot.Garment, true);
            _stage.Step(0f);
            Assert.That(Row(PartSlot.Garment).Hidden, Is.True);
            Assert.That(_stage.View.ActorOf(PartSlot.Garment).LayerRenderers.Any(r => r.enabled), Is.False);
            foreach (var slot in new[] { PartSlot.Body, PartSlot.Head, PartSlot.HeadTop, PartSlot.HeadBottom })
                Assert.That(_stage.View.ActorOf(slot).LayerRenderers.Any(r => r.enabled), Is.True, slot.ToString());
            _stage.View.SetPartHidden(PartSlot.Garment, false);
            _stage.Step(0f);
            Assert.That(_stage.View.ActorOf(PartSlot.Garment).LayerRenderers.Any(r => r.enabled), Is.True);
        }

        [Test]
        public void UnknownHeadgear_IsListedAsMissing_WithTheReason()
        {
            var look = UnitTestData.Swordsman();
            look.HeadTop = 64999;
            _stage.SetLook(look);
            var row = Row(PartSlot.HeadTop);
            Assert.That(row.Found, Is.False);
            Assert.That(row.Resolution, Does.Contain("64999").And.Contain("not in the catalog"));
            Assert.That(Row(PartSlot.Body).Found, Is.True);
            Assert.That(Row(PartSlot.Body).Resolution, Does.Contain("JOB_SWORDMAN"));
        }

        [Test]
        public void Rows_ListTheOrderOfTheCurrentFrame()
        {
            _stage.SetLook(UnitTestData.Swordsman());
            Assert.That(Row(PartSlot.Garment).Order, Is.LessThan(Row(PartSlot.Body).Order), "behind, facing front");
            Assert.That(Row(PartSlot.Weapon).Order, Is.GreaterThan(Row(PartSlot.Head).Order));
            _stage.Yaw = 180f;
            _stage.Step(0f);
            Assert.That(Row(PartSlot.Garment).Order, Is.GreaterThan(Row(PartSlot.Body).Order), "in front, facing back");
        }

        [Test]
        public void Save_WritesTheViewAndTheGrid()
        {
            _stage.SetLook(UnitTestData.Swordsman());
            string folder = Path.Combine(Path.GetTempPath(), "Uninarok-0009");
            string one = Path.Combine(folder, "composer-one.png"), grid = Path.Combine(folder, "composer-grid.png");
            _stage.Save(one, false, 256, 256);
            _stage.Save(grid, true, 256, 256);
            var image = new Texture2D(2, 2);
            Assert.That(image.LoadImage(File.ReadAllBytes(one)), Is.True);
            Assert.That((image.width, image.height), Is.EqualTo((256, 256)));
            Assert.That(image.GetPixels32().Select(c => (c.r << 16) | (c.g << 8) | c.b).Distinct().Count(), Is.GreaterThan(20), "a picture, not a plain colour");
            Assert.That(image.LoadImage(File.ReadAllBytes(grid)), Is.True);
            Assert.That((image.width, image.height), Is.EqualTo((512, 256)), "4 × 2 cells");
            Object.DestroyImmediate(image);
            Assert.That(_stage.View.Facing, Is.EqualTo(UninarokDirection.South), "the facing is put back");
        }

        [Test]
        public void TheWindow_OpensAndCloses()
        {
            RoUnitComposerWindow.Open();
            var window = EditorWindow.GetWindow<RoUnitComposerWindow>();
            Assert.That(window, Is.Not.Null);
            window.Close();
        }
    }
}
