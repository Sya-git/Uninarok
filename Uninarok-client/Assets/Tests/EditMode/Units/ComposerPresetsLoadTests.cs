using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Uninarok.Networking.Data;
using Uninarok.Units.Editor;

namespace Uninarok.Units.Tests
{
    // TASKS-0009 T11.5 (FR-18, FR-17; PLAN-0009 4.12 "Missing ids"): loading presets through the window, with the imported catalog.
    [Category("NeedsData")]
    public class ComposerPresetsLoadTests
    {
        string _dir;
        RoUnitComposerWindow _window;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "uninarok-composer-load-" + System.Guid.NewGuid().ToString("N"));
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

        static ComposerLookFields Knight() => new ComposerLookFields
        {
            Kind = UnitKind.Player, Class = (int)PlayerJob.JOB_SWORDMAN, Sex = AccountSex.SEX_MALE,
            HairStyle = 2, HairColour = 3,
            Right = new PresetHand { ByItem = true, Id = 1201 },
            Animation = (int)PcAnimation.Idle, Mode = PlayMode.Loop, Facing = UninarokDirection.South,
        };

        // The window loads a preset from a user file in the temporary folder (never UserSettings).
        void LoadFromUserFile(UnitComposerPreset preset)
        {
            var store = Store();
            Assert.That(store.Save(preset, false), Is.EqualTo(SaveResult.Saved));
            _window = ScriptableObject.CreateInstance<RoUnitComposerWindow>();
            _window.Store = store;
            _window.PickPreset(preset.Name);
        }

        [Test]
        public void UnitComposerPresets_Starters_ResolveWithNoMissingPart()
        {
            UnitTestData.CatalogOrIgnore();
            _window = ScriptableObject.CreateInstance<RoUnitComposerWindow>();
            var store = Store();
            _window.Store = store;
            var starters = store.All.Where(e => e.Starter).Select(e => e.Preset.Name).ToList();
            Assert.That(starters.Count, Is.EqualTo(4), string.Join(" | ", starters));

            foreach (var name in starters)
            {
                _window.PickPreset(name); // an unexpected warning (a problem in the preset) is checked below
                var missing = _window.Stage.Rows().Where(r => !r.Found).Select(r => $"{r.Slot}: {r.Resolution}").ToList();
                Assert.That(missing, Is.Empty, $"starter '{name}': {string.Join("; ", missing)}");
            }
            LogAssert.NoUnexpectedReceived();

            _window.PickPreset("Assassin, two knives");
            Assert.That(_window.Stage.View.Report.Hands.Weapon, Is.EqualTo(WeaponSource.Both));
        }

        [Test]
        public void UnitComposerPresets_UnknownHeadgear_LoadsTheRest_OneWarning()
        {
            UnitTestData.CatalogOrIgnore();
            var f = Knight();
            f.Top = new PresetHand { ByItem = false, Id = 65000 };
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Unit Composer\] preset 'headgear-bad': 1 problem: HeadTop.*"));
            LoadFromUserFile(ComposerPresets.Build("headgear-bad", f));
            LogAssert.NoUnexpectedReceived();

            var rows = _window.Stage.Rows().ToList();
            var missing = rows.Where(r => !r.Found).ToList();
            Assert.That(missing.Select(r => r.Slot), Is.EqualTo(new[] { PartSlot.HeadTop }), string.Join("; ", rows.Select(r => $"{r.Slot}:{r.Found}")));
            Assert.That(rows.Where(r => r.Slot == PartSlot.Weapon && r.Found).Count(), Is.EqualTo(1), "the weapon of the preset still loads");
            Assert.That(rows.Any(r => r.Slot == PartSlot.Body && r.Found), Is.True, "the body still loads");
            Assert.That(rows.Any(r => r.Slot == PartSlot.Head && r.Found), Is.True, "the head (hair) still loads");
            Assert.That(_window.SelectedPreset, Is.EqualTo("headgear-bad"));
        }

        [Test]
        public void UnitComposerPresets_BadAnimationFacingMode_FallBack_OneWarning()
        {
            UnitTestData.CatalogOrIgnore();
            var p = ComposerPresets.Build("fallbacks", Knight());
            p.Animation = "NoSuchAnimation";
            p.Facing = (UninarokDirection)99;
            p.Mode = (PlayMode)77;
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Unit Composer\] preset 'fallbacks': 3 problems: .*play mode 77.*facing 99.*animation 'NoSuchAnimation'"));
            LoadFromUserFile(p);
            LogAssert.NoUnexpectedReceived();

            var so = new SerializedObject(_window);
            Assert.That(so.FindProperty("_animation").intValue, Is.EqualTo((int)PcAnimation.Idle));
            Assert.That(so.FindProperty("_facing").intValue, Is.EqualTo((int)UninarokDirection.South));
            Assert.That(so.FindProperty("_mode").intValue, Is.EqualTo((int)PlayMode.Loop));
            Assert.That(_window.Stage.Rows().Where(r => !r.Found), Is.Empty, "the rest of the preset loads");
        }
    }
}
