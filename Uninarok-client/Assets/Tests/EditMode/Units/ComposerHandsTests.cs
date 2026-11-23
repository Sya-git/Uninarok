using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Uninarok.Networking.Data;
using Uninarok.Units.Editor;

namespace Uninarok.Units.Tests
{
    // TASKS-0009 T10.8 (FR-15, FR-17; PLAN-0009 4.11 "Composer", D35): the hands as typed.
    public class ComposerHandsTests
    {
        [TestCase(1201, 0, 1201)]
        [TestCase(1, 1, 0)]
        [TestCase(499, 499, 0)]
        [TestCase(500, 0, 500)]
        [TestCase(0, 0, 0)]
        public void Split_ItemIdSetsOnlyItem_ViewIdOnlyView(int typed, int view, int item)
        {
            ComposerHands.Split(typed, out int v, out int i);
            Assert.That((v, i), Is.EqualTo((view, item)));
        }

        [Test]
        public void Apply_PutsRightInWeaponFields_LeftInShieldFields()
        {
            var look = new UnitLook { Weapon = 9, WeaponItem = 9, Shield = 9, ShieldItem = 9 };
            ComposerHands.Apply(ref look, 1101, 2);
            Assert.That((look.Weapon, look.WeaponItem, look.Shield, look.ShieldItem), Is.EqualTo((0, 1101, 2, 0)));
        }

        [Test, Category("NeedsData")]
        public void Item1201InBothHands_HandsLineNamesType25AndAssassinFamily()
        {
            var source = UnitTestData.CatalogOrIgnore();
            var stage = new UnitPreviewStage(source);
            try
            {
                var look = new UnitLook { Kind = UnitKind.Player, JobId = (int)PlayerJob.JOB_ASSASSIN, Sex = AccountSex.SEX_MALE, HairStyle = 1 };
                ComposerHands.Apply(ref look, 1201, 1201);
                stage.SetLook(look);
                string line = stage.View.Report.Hands.Describe();
                Assert.That(line, Does.Contain("type 25").And.Contain("assassin family"), line);
            }
            finally
            {
                stage.Dispose();
            }
        }

        [Test]
        public void TheHands_AreSerializedFields_RightAndLeft_WithTheDefaults()
        {
            var window = ScriptableObject.CreateInstance<RoUnitComposerWindow>();
            try
            {
                var so = new SerializedObject(window);
                Assert.That(so.FindProperty("_right.Id").intValue, Is.EqualTo(1101));
                Assert.That(so.FindProperty("_left.Id").intValue, Is.EqualTo(2101));
                so.FindProperty("_right.Id").intValue = 1201;
                so.FindProperty("_left.Id").intValue = 1202;
                so.ApplyModifiedProperties();
                // What a domain reload does: the window's JSON is written and read back into a new instance.
                var copy = ScriptableObject.CreateInstance<RoUnitComposerWindow>();
                try
                {
                    JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(window), copy);
                    var so2 = new SerializedObject(copy);
                    Assert.That(so2.FindProperty("_right.Id").intValue, Is.EqualTo(1201));
                    Assert.That(so2.FindProperty("_left.Id").intValue, Is.EqualTo(1202));
                }
                finally
                {
                    Object.DestroyImmediate(copy);
                }
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }
    }
}
