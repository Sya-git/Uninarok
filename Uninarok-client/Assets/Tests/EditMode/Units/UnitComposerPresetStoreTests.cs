using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Uninarok.Networking.Data;
using Uninarok.Units.Editor;

namespace Uninarok.Units.Tests
{
    // TASKS-0009 T11.1 (FR-18; PLAN-0009 4.12, D37 to D39): the preset store, on temporary folders and synthetic presets.
    public class UnitComposerPresetStoreTests
    {
        const string Pending = "pending T11.2";

        [Serializable] class FileModel { public int Version = 1; public List<UnitComposerPreset> Presets = new List<UnitComposerPreset>(); }

        string _dir, _user, _starters;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "uninarok-presets-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _user = Path.Combine(_dir, "UserSettings", "Uninarok", "UnitComposerPresets.json");
            _starters = Path.Combine(_dir, "Starters.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        void WriteStarters(params string[] names)
        {
            var model = new FileModel();
            foreach (var n in names) model.Presets.Add(Make(n));
            File.WriteAllText(_starters, JsonUtility.ToJson(model));
        }

        static UnitComposerPreset Make(string name) => new UnitComposerPreset { Name = name, Animation = "Idle", Facing = UninarokDirection.South };

        static UnitComposerPreset Full(string name) => new UnitComposerPreset
        {
            Name = name, Kind = UnitKind.Player, UnitJob = 1002, Class = 7, Sex = AccountSex.SEX_MALE,
            BodyStyle = 1, HairStyle = 5, HairColour = 4, ClothColour = 2,
            Top = new PresetHand { ByItem = true, Id = 2201 }, Mid = new PresetHand { ByItem = false, Id = 33 },
            Low = new PresetHand { ByItem = false, Id = 44 }, Garment = new PresetHand { ByItem = true, Id = 2501 },
            Right = new PresetHand { ByItem = true, Id = 1201 }, Left = new PresetHand { ByItem = true, Id = 1201 },
            Animation = "Attack3", Mode = PlayMode.OnceHold, Facing = UninarokDirection.NorthEast,
        };

        UnitComposerPresetStore NewStore() => new UnitComposerPresetStore(_user, _starters);

        [Test, Ignore(Pending)]
        public void UnitComposerPresetStore_SaveReload_AllFields_NoCameraFrameOrPlayState()
        {
            WriteStarters();
            var a = NewStore();
            Assert.That(a.Save(Full("Mine"), false), Is.EqualTo(SaveResult.Saved));
            var b = NewStore();
            var p = b.All.Single(x => x.Preset.Name == "Mine").Preset;
            Assert.That(JsonUtility.ToJson(p), Is.EqualTo(JsonUtility.ToJson(Full("Mine"))));
            // The preset type holds no camera, frame or play state (FR-18).
            var fields = typeof(UnitComposerPreset).GetFields().Select(f => f.Name.ToLowerInvariant()).ToList();
            Assert.That(fields.Any(f => f.Contains("camera") || f.Contains("frame") || f.Contains("playing") || f.Contains("paused")
                || f.Contains("speed") || f.Contains("tilt") || f.Contains("distance")), Is.False);
        }

        [Test, Ignore(Pending)]
        public void UnitComposerPresetStore_StartersListed_AndReadOnly()
        {
            WriteStarters("Starter A", "Starter B");
            var s = NewStore();
            Assert.That(s.All.Select(x => (x.Preset.Name, x.Starter)), Is.EqualTo(new[] { ("Starter A", true), ("Starter B", true) }));
            Assert.That(s.IsStarter("starter a"), Is.True);
            Assert.That(s.Rename("Starter A", "Other"), Is.False);
            Assert.That(s.Delete("Starter A"), Is.False);
            Assert.That(s.Save(Make("Starter A"), true), Is.EqualTo(SaveResult.Refused));
            s.Save(Make("Mine"), false);
            Assert.That(s.All.Take(2).All(x => x.Starter), Is.True, "starters come first");
            Assert.That(NewStore().All.Count(x => x.Starter), Is.EqualTo(2));
            Assert.That(File.ReadAllText(_starters), Does.Not.Contain("Mine"), "the starter file is never written");
        }

        [Test, Ignore(Pending)]
        public void UnitComposerPresetStore_NamesUniqueAcrossKinds_CaseInsensitive()
        {
            WriteStarters("Starter A");
            var s = NewStore();
            s.Save(Make("Mine"), false);
            Assert.That(s.IsTaken("  mine "), Is.True);
            Assert.That(s.IsTaken("STARTER a"), Is.True);
            Assert.That(s.IsTaken("Free"), Is.False);
            s.Save(Make("Other"), false);
            Assert.That(s.Rename("Other", "MINE"), Is.False);
            Assert.That(s.Rename("Other", "starter A"), Is.False);
            Assert.That(s.All.Select(x => x.Preset.Name).Count(n => n == "Other"), Is.EqualTo(1));
        }

        [Test, Ignore(Pending)]
        public void UnitComposerPresetStore_SaveOverUserName_NeedsConfirm_StarterNameRefused()
        {
            WriteStarters("Starter A");
            var s = NewStore();
            Assert.That(s.Save(Make("Mine"), false), Is.EqualTo(SaveResult.Saved));
            Assert.That(s.Save(Full("mine"), false), Is.EqualTo(SaveResult.NeedsConfirm));
            Assert.That(s.All.Single(x => !x.Starter).Preset.HairStyle, Is.EqualTo(0), "not overwritten without confirmation");
            Assert.That(s.Save(Full("mine"), true), Is.EqualTo(SaveResult.Saved));
            Assert.That(s.All.Count(x => !x.Starter), Is.EqualTo(1));
            Assert.That(s.All.Single(x => !x.Starter).Preset.HairStyle, Is.EqualTo(5));
            Assert.That(s.Save(Make("starter a"), false), Is.EqualTo(SaveResult.Refused));
            Assert.That(s.Save(Make("starter a"), true), Is.EqualTo(SaveResult.Refused));
            Assert.That(s.Save(Make("  "), false), Is.EqualTo(SaveResult.Refused));
        }

        [Test, Ignore(Pending)]
        public void UnitComposerPresetStore_Rename_KeepsLook()
        {
            WriteStarters();
            var s = NewStore();
            s.Save(Full("Old"), false);
            Assert.That(s.Rename("Old", "New"), Is.True);
            var p = NewStore().All.Single().Preset;
            Assert.That(p.Name, Is.EqualTo("New"));
            Assert.That(JsonUtility.ToJson(p), Is.EqualTo(JsonUtility.ToJson(Full("New"))));
        }

        [Test, Ignore(Pending)]
        public void UnitComposerPresetStore_Delete_GoneAfterReload()
        {
            WriteStarters();
            var s = NewStore();
            s.Save(Make("A"), false);
            s.Save(Make("B"), false);
            Assert.That(s.Delete("A"), Is.True);
            Assert.That(s.Delete("A"), Is.False);
            s.Reload();
            Assert.That(s.All.Select(x => x.Preset.Name), Is.EqualTo(new[] { "B" }));
            Assert.That(NewStore().All.Select(x => x.Preset.Name), Is.EqualTo(new[] { "B" }));
        }

        [Test, Ignore(Pending)]
        public void UnitComposerPresetStore_UnreadableFile_OneWarning_KeptAside()
        {
            WriteStarters("Starter A");
            Directory.CreateDirectory(Path.GetDirectoryName(_user));
            const string junk = "{ this is not json";
            File.WriteAllText(_user, junk);

            LogAssert.Expect(LogType.Warning, new Regex(".+"));
            var s = NewStore();
            Assert.That(s.Problem, Is.Not.Null);
            Assert.That(s.All.Select(x => x.Starter), Is.EqualTo(new[] { true }), "user list empty, starters kept");
            Assert.That(File.ReadAllText(_user), Is.EqualTo(junk), "left untouched until the next save");

            Assert.That(s.Save(Make("Mine"), false), Is.EqualTo(SaveResult.Saved));
            var aside = Path.Combine(Path.GetDirectoryName(_user), "UnitComposerPresets.unreadable.json");
            Assert.That(File.Exists(aside), Is.True);
            Assert.That(File.ReadAllText(aside), Is.EqualTo(junk));
            Assert.That(NewStore().All.Any(x => x.Preset.Name == "Mine"), Is.True);
            // LogAssert fails the test on any further unexpected warning.
        }

        [Test, Ignore(Pending)]
        public void UnitComposerPresetStore_UserFileUnderUserSettings()
        {
            // ForProject points at <project>/UserSettings/Uninarok/UnitComposerPresets.json (D37). The real file is
            // backed up and restored around the check.
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string file = Path.Combine(projectRoot, "UserSettings", "Uninarok", "UnitComposerPresets.json");
            byte[] backup = File.Exists(file) ? File.ReadAllBytes(file) : null;
            try
            {
                if (backup != null) File.Delete(file);
                var s = UnitComposerPresetStore.ForProject();
                Assert.That(s.Save(Make("zz-test-user-settings"), false), Is.EqualTo(SaveResult.Saved));
                Assert.That(File.Exists(file), Is.True);
                Assert.That(File.ReadAllText(file), Does.Contain("zz-test-user-settings"));
            }
            finally
            {
                if (backup != null) File.WriteAllBytes(file, backup);
                else if (File.Exists(file)) File.Delete(file);
            }
        }
    }
}
