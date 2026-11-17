using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Uninarok.RoImport.Editor.Ui;

namespace Uninarok.RoImport.Tests
{
    // SPEC-0002 FR-1 (configured source folder), FR-15 (source files are not imported). Plan 4.2. TASKS T2.3.
    // The tests save and restore the owner's own preference.
    public class RoSourceLocationTests
    {
        const string KnownSourceFile = "Assets/Editor/GRF Resources~/data/texture/BACKSIDE.BMP";

        bool _hadPref;
        string _pref;

        [SetUp]
        public void SetUp()
        {
            _hadPref = EditorPrefs.HasKey(RoSourceLocation.PrefKey);
            _pref = EditorPrefs.GetString(RoSourceLocation.PrefKey, string.Empty);
            RoSourceLocation.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            if (_hadPref) EditorPrefs.SetString(RoSourceLocation.PrefKey, _pref);
            else EditorPrefs.DeleteKey(RoSourceLocation.PrefKey);
        }

        static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);

        [Test]
        public void Default_IsTheTildeFolderInThisProject()
        {
            Assert.IsFalse(RoSourceLocation.IsOverridden);
            Assert.AreEqual(Path.GetFullPath(Path.Combine(ProjectRoot, "Assets", "Editor", "GRF Resources~", "data")), RoSourceLocation.Resolve());
            StringAssert.EndsWith("~", Path.GetFileName(Path.GetDirectoryName(RoSourceLocation.Resolve())),
                "the source folder name ends with ~ so Unity ignores it");
        }

        [Test]
        public void FileInTheDefaultSourceFolder_IsNotAUnityAsset()
        {
            Assert.IsTrue(File.Exists(Path.Combine(ProjectRoot, KnownSourceFile)), "the reference data is in the default folder");

            Assert.AreEqual(string.Empty, AssetDatabase.AssetPathToGUID(KnownSourceFile, AssetPathToGUIDOptions.OnlyExistingAssets));
            Assert.IsFalse(File.Exists(Path.Combine(ProjectRoot, KnownSourceFile + ".meta")));
        }

        [Test]
        public void Preference_OverridesTheDefault()
        {
            string elsewhere = Path.Combine(Path.GetTempPath(), "RoImportTests", "some data");

            RoSourceLocation.Set(elsewhere);

            Assert.IsTrue(RoSourceLocation.IsOverridden);
            Assert.AreEqual(Path.GetFullPath(elsewhere), RoSourceLocation.Resolve());
        }

        [Test]
        public void RelativePreference_IsResolvedAgainstTheProjectRoot()
        {
            RoSourceLocation.Set("../GRF/data");

            Assert.AreEqual(Path.GetFullPath(Path.Combine(ProjectRoot, "..", "GRF", "data")), RoSourceLocation.Resolve());
        }

        [Test]
        public void EmptyPreference_ReturnsToTheDefault()
        {
            RoSourceLocation.Set("C:/somewhere");
            RoSourceLocation.Set("  ");

            Assert.IsFalse(RoSourceLocation.IsOverridden);
            StringAssert.EndsWith(Path.Combine("GRF Resources~", "data"), RoSourceLocation.Resolve());
        }
    }
}
