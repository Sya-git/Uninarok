using NUnit.Framework;
using UnityEngine;
using Uninarok.Map;
using Uninarok.Networking.Data;
using Uninarok.Sprites;
using Uninarok.Units.Editor;

namespace Uninarok.Units.Tests
{
    // TASKS-0009 T5.2 (FR-9; PLAN-0009 4.2, 4.6) on the imported catalog, stepped by hand.
    [Category("NeedsData")]
    public class UnitViewTests
    {
        AssetDatabaseUnitAssetSource _source;
        UnitView _view;

        [SetUp]
        public void SetUp()
        {
            UnitSystem.ResetForTests();
            _source = UnitTestData.CatalogOrIgnore();
            _view = UnitView.Create(new UnitLoader(_source), null);
        }

        [TearDown]
        public void TearDown()
        {
            if (_view != null) Object.DestroyImmediate(_view.gameObject);
            UnitSystem.ResetForTests();
        }

        void Step(float seconds) => UnitSystem.StepOne(_view, seconds, Quaternion.Euler(50f, 0f, 0f));

        [Test]
        public void UnitView_SetLook_KeepsFrameAndPosition()
        {
            var look = UnitTestData.Swordsman();
            _view.transform.position = new Vector3(3f, 1f, 2f);
            _view.SetLook(look);
            _view.Play(PcAnimation.Walk);
            Step(0f);
            float ms = _view.ActorOf(PartSlot.Body).Data.Actions[_view.ActorOf(PartSlot.Body).UniqueAction].FrameMs;
            Step(3.5f * ms / 1000f);
            Assert.That(_view.Frame, Is.EqualTo(3));
            var hair = _view.ActorOf(PartSlot.Head).LayerRenderers[0];

            look.Weapon = 1;         // a dagger for the sword
            look.WeaponItem = 1201;
            look.HairColour = 5;
            var sword = _view.ActorOf(PartSlot.Weapon).Data;
            var palette = _view.Parts.Palettes[(int)PartSlot.Head];
            _view.SetLook(look);
            Step(0f);
            Assert.That(_view.Frame, Is.EqualTo(3), "mid-walk, the frame stays");
            Assert.That(_view.ActorOf(PartSlot.Weapon).Data, Is.Not.SameAs(sword));
            Assert.That(_view.ActorOf(PartSlot.Weapon).CurrentFrame % 8, Is.EqualTo(_view.Frame % 8));
            Assert.That(_view.Parts.Palettes[(int)PartSlot.Head], Is.Not.SameAs(palette));
            Assert.That(_view.transform.position, Is.EqualTo(new Vector3(3f, 1f, 2f)));
            Step(0.6f * ms / 1000f);
            Assert.That(_view.Frame, Is.EqualTo(4), "and the time goes on");

            // The job id replaces the look in place; walking stays walking.
            _view.SetLook(UnitLook.ForJob(1063));
            Step(0f);
            Assert.That(_view.AnimationNumber, Is.EqualTo((int)MonsterAnimation.Walk));
            Assert.That(_view.transform.position, Is.EqualTo(new Vector3(3f, 1f, 2f)));
            Assert.That(_view.ActorOf(PartSlot.Head).LayerRenderers[0].enabled, Is.False, "the head is gone");
            Assert.That(hair != null, Is.True);
        }

        // TASKS-0009 T9.1 (FR-16; PLAN-0009 4.10): what the shader reads is where the layer really is above the ground point.
        [Test]
        public void UnitView_LayerHeights_AreFromThePivot_AtTheDisplayScale()
        {
            foreach (var look in new[] { UnitTestData.Swordsman(), UnitLook.ForJob((int)PlayerJob.JOB_BABY_SWORDMAN), UnitLook.ForJob(1002) })
            {
                _view.SetLook(look);
                _view.Play(PcAnimation.Walk);
                int checkedLayers = 0;
                for (int frame = 0; frame < 12; frame++)
                {
                    Step(0.06f);
                    foreach (PartSlot slot in System.Enum.GetValues(typeof(PartSlot)))
                    {
                        if ((int)slot >= UnitParts.SlotCount) continue;
                        var actor = _view.ActorOf(slot);
                        if (actor == null) continue;
                        foreach (var r in actor.LayerRenderers)
                        {
                            if (!r.enabled) continue;
                            float height = Vector3.Dot(r.transform.position - _view.transform.position, _view.Pivot.up);
                            Vector3 axis = r.transform.localToWorldMatrix.GetColumn(2);
                            Assert.That(axis.magnitude - SpriteActor.HeightOffset, Is.EqualTo(height).Within(1e-3f), $"{look.JobId} {slot} frame {frame}");
                            checkedLayers++;
                        }
                    }
                }
                Assert.That(checkedLayers, Is.GreaterThan(5));
            }
        }

        [Test]
        public void UnitView_SetLook_ReleasesTheOldAssets()
        {
            _view.SetLook(UnitTestData.Swordsman());
            int full = _source.HeldCount;
            _view.SetLook(UnitLook.ForJob(1002));
            Assert.That(_source.HeldCount, Is.LessThan(full));
            Object.DestroyImmediate(_view.gameObject);
            Assert.That(_source.HeldCount, Is.EqualTo(0));
        }

        [Test]
        public void UnitView_UnequippedPart_IsHidden_OthersStay()
        {
            var look = UnitTestData.Swordsman();
            _view.SetLook(look);
            Step(0f);
            Assert.That(_view.ActorOf(PartSlot.Garment).LayerRenderers[0].enabled, Is.True);
            look.Garment = 0;
            _view.SetLook(look);
            Step(0f);
            Assert.That(_view.ActorOf(PartSlot.Garment).LayerRenderers[0].enabled, Is.False);
            Assert.That(_view.ActorOf(PartSlot.Body).LayerRenderers[0].enabled, Is.True);
            Assert.That(_view.Parts.Has(PartSlot.Shield), Is.True);
        }

        [Test]
        public void UnitView_HiddenJob_ShowsNothing()
        {
            _view.SetLook(UnitTestData.Novice());
            Step(0f);
            _view.SetLook(UnitLook.ForJob(111));
            Step(0f);
            Assert.That(_view.ActorOf(PartSlot.Body).LayerRenderers[0].enabled, Is.False);
            Assert.That(_view.Frame, Is.EqualTo(-1));
            Assert.That(_view.Loaded, Is.True);
        }

        [Test]
        public void UnitView_ComposesOnlyOnChange()
        {
            _view.SetLook(UnitTestData.Swordsman());
            _view.Play(PcAnimation.Idle);
            Step(0f);
            int before = _view.ComposeCount;
            for (int i = 0; i < 20; i++) Step(0.05f);
            Assert.That(_view.ComposeCount, Is.EqualTo(before), "idle with the head still composes nothing");
            _view.HeadFacing = HeadFacing.Left;
            Step(0f);
            Assert.That(_view.ComposeCount, Is.EqualTo(before + 1));
        }

        [Test]
        public void UnitView_PartOrder_IsTheSortingOrder()
        {
            _view.SetLook(UnitTestData.Swordsman());
            _view.Play(PcAnimation.Idle);
            Step(0f);
            var sorted = _view.Placements.ToArray();
            for (int i = 1; i < sorted.Length; i++)
                Assert.That(_view.ActorOf(sorted[i].Slot).SortingBase, Is.GreaterThan(_view.ActorOf(sorted[i - 1].Slot).SortingBase));
            Assert.That(_view.ActorOf(PartSlot.Weapon).SortingBase, Is.GreaterThan(_view.ActorOf(PartSlot.Head).SortingBase), "facing front the sword is in front of the head");
        }

        // T5.3 (FR-10)
        [Test]
        public void UnitView_PlaceOn_TakesTheGroundHeight()
        {
            var map = UnityEditor.AssetDatabase.LoadAssetAtPath<MapData>("Assets/Data/map/prt_fild08.asset");
            if (map == null) Assert.Ignore("prt_fild08 is not imported");
            var p = map.CellCenter(150, 150);
            _view.PlaceOn(map, new Vector3(p.x, 100f, p.z));
            Assert.That(_view.transform.position.y, Is.EqualTo(p.y).Within(1e-4f));
            _view.PlaceOn(map, new Vector3(-5f, 7f, -5f));
            Assert.That(_view.transform.position.y, Is.EqualTo(7f), "outside the map the height is kept");
        }
    }
}
