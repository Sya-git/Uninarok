using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Uninarok.Networking.Data;
using Uninarok.Units.Editor;

namespace Uninarok.Units.Tests
{
    // TASKS-0009 T11.4 (FR-18, FR-15; PLAN-0009 4.12 "Window"): preset <-> composer fields, and picking a starter in the window.
    public class ComposerPresetsTests
    {
        static ComposerLookFields Sample() => new ComposerLookFields
        {
            Kind = UnitKind.Player, Class = (int)PlayerJob.JOB_KNIGHT, Sex = AccountSex.SEX_FEMALE,
            BodyStyle = 1, HairStyle = 5, HairColour = 7, ClothColour = 2,
            Top = new PresetHand { ByItem = true, Id = 2220 }, Mid = new PresetHand { ByItem = false, Id = 12 }, Low = new PresetHand { ByItem = true, Id = 2218 },
            Garment = new PresetHand { ByItem = false, Id = 1 }, Right = new PresetHand { ByItem = true, Id = 1201 }, Left = new PresetHand { ByItem = false, Id = 2 },
            Animation = (int)PcAnimation.Walk, Mode = PlayMode.OnceHold, Facing = UninarokDirection.NorthEast,
        };

        [Test]
        public void Preset_RoundTrip_BuildThenApplyGivesTheSameFields()
        {
            var f = Sample();
            var problems = new List<string>();
            var back = ComposerPresets.ToFields(ComposerPresets.Build("x", f), problems);
            Assert.That(problems, Is.Empty);
            Assert.That(back, Is.EqualTo(f));
        }

        [Test]
        public void Preset_RoundTrip_SurvivesJson()
        {
            var f = Sample();
            var json = JsonUtility.ToJson(ComposerPresets.Build("x", f));
            var back = ComposerPresets.ToFields(JsonUtility.FromJson<UnitComposerPreset>(json), null);
            Assert.That(back, Is.EqualTo(f));
        }

        [Test]
        public void Preset_UnitMode_StoresTheMonsterAnimationByName()
        {
            var f = new ComposerLookFields { Kind = UnitKind.Unit, UnitJob = 1002, Animation = (int)MonsterAnimation.Attack, Mode = PlayMode.Loop, Facing = UninarokDirection.South };
            var p = ComposerPresets.Build("poring", f);
            Assert.That(p.Animation, Is.EqualTo("Attack"));
            Assert.That(ComposerPresets.ToFields(p, null), Is.EqualTo(f));
        }

        [Test]
        public void Preset_BadValues_FallBackAndAreListed()
        {
            var p = ComposerPresets.Build("bad", Sample());
            p.Animation = "NoSuchAnimation";
            p.Mode = (PlayMode)9;
            p.Facing = (UninarokDirection)9;
            p.Class = 65000;
            var problems = new List<string>();
            var f = ComposerPresets.ToFields(p, problems);
            Assert.That(f.Animation, Is.EqualTo((int)PcAnimation.Idle));
            Assert.That(f.Mode, Is.EqualTo(PlayMode.Loop));
            Assert.That(f.Facing, Is.EqualTo(UninarokDirection.South));
            Assert.That(problems.Count, Is.EqualTo(4), string.Join("; ", problems));
        }

        // ---- the window, with the real starter file and a temporary user file ----

        string _dir;
        RoUnitComposerWindow _window;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "uninarok-composer-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (_window != null) Object.DestroyImmediate(_window);
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        UnitComposerPresetStore Store() => new UnitComposerPresetStore(
            Path.Combine(_dir, "user.json"),
            Path.Combine(Application.dataPath, "Scripts", "Units", "Editor", "Presets", "UnitComposerStarterPresets.json"));

        [Test]
        public void Window_ListsTheFourStarters_MarkedStarter_AndOffersNoEditsOnThem()
        {
            _window = ScriptableObject.CreateInstance<RoUnitComposerWindow>();
            _window.Store = Store();
            var labels = _window.PresetLabels;
            Assert.That(labels.Count, Is.EqualTo(5), string.Join(" | ", labels));
            Assert.That(labels[0], Is.EqualTo("(none)"));
            for (int i = 1; i < 5; i++) Assert.That(labels[i], Does.EndWith(" (starter)"));
            Assert.That(labels, Does.Contain("Assassin, two knives (starter)"));

            Assert.That(_window.CanModifySelected, Is.False);
            _window.PickPreset("Novice, rod");
            Assert.That(_window.SelectedPreset, Is.EqualTo("Novice, rod"));
            Assert.That(_window.CanModifySelected, Is.False, "Save, Rename and Delete are disabled for a starter");
        }

        [Test, Category("NeedsData")]
        public void Window_PickingAssassinStarter_ShowsBothBlades_AtFrame0_AndLeavesCameraAndPlaybackAlone()
        {
            UnitTestData.CatalogOrIgnore();
            _window = ScriptableObject.CreateInstance<RoUnitComposerWindow>();
            var so = new SerializedObject(_window);
            so.FindProperty("_yaw").floatValue = 123f;
            so.FindProperty("_pitch").floatValue = 33f;
            so.FindProperty("_distance").floatValue = 20f;
            so.FindProperty("_speed").floatValue = 2.5f;
            so.FindProperty("_head").intValue = 2;
            so.FindProperty("_grid").boolValue = true;
            so.FindProperty("_shadow").boolValue = false;
            so.FindProperty("_playing").boolValue = false;
            so.ApplyModifiedProperties();

            _window.Store = Store();
            _window.PickPreset("Assassin, two knives");

            var view = _window.Stage.View;
            Assert.That(view.Report.Hands.Weapon, Is.EqualTo(WeaponSource.Both));
            Assert.That(view.Frame, Is.EqualTo(0));
            Assert.That(_window.CanModifySelected, Is.False);

            so.Update();
            Assert.That(so.FindProperty("_yaw").floatValue, Is.EqualTo(123f));
            Assert.That(so.FindProperty("_pitch").floatValue, Is.EqualTo(33f));
            Assert.That(so.FindProperty("_distance").floatValue, Is.EqualTo(20f));
            Assert.That(so.FindProperty("_speed").floatValue, Is.EqualTo(2.5f));
            Assert.That(so.FindProperty("_head").intValue, Is.EqualTo(2));
            Assert.That(so.FindProperty("_grid").boolValue, Is.True);
            Assert.That(so.FindProperty("_shadow").boolValue, Is.False);
            Assert.That(so.FindProperty("_playing").boolValue, Is.False);
            Assert.That(so.FindProperty("_right.Id").intValue, Is.EqualTo(1201));
            Assert.That(so.FindProperty("_left.Id").intValue, Is.EqualTo(1201));
        }
    }
}
