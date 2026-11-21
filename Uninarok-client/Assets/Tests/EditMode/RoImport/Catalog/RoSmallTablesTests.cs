using System.Linq;
using NUnit.Framework;
using Uninarok.RoImport.Core.Catalog.Tables;
using Uninarok.RoImport.Core.Formats;

namespace Uninarok.RoImport.Tests.Catalog
{
    // TASKS-0008 T2.7 (FR-1, FR-6, FR-7; PLAN-0008 4.2): emote ids, skill ids and monster scales.
    public class RoSmallTablesTests
    {
        [Test]
        public void EmotesAreTheEtGlobals()
        {
            const string lua = "ET_SURPRISE = 0\nET_QUESTION = 1\nMSI_EMOTION_SURPRISE = 544\nEMOTION_ORDERLIST = { ET_QUESTION, ET_SURPRISE }\n" +
                               "EmotionList = {}\nInsertEmotionListTable(ET_SURPRISE, -1, 0)";
            Assert.That(RoEmoteTable.TryRead(lua, out var t, out string reason), Is.True, reason);
            Assert.That(t.Ids.Keys, Is.EquivalentTo(new[] { "ET_SURPRISE", "ET_QUESTION" }));
            CollectionAssert.AreEqual(new[] { 1, 0 }, t.Order);
        }

        [Test]
        public void SkillIconsAreTheLowerCaseConstants()
        {
            Assert.That(RoSkillTable.TryRead("SKID = { NV_BASIC = 1, SM_BASH = 5, TEAK = 11000, SCRIPT_000 = 11000 }", out var t, out string reason),
                Is.True, reason);
            Assert.That(t.Ids["SM_BASH"], Is.EqualTo(5));
            Assert.That(RoSkillTable.IconName(t.ConstantsOf(5)[0]), Is.EqualTo("sm_bash"));
            CollectionAssert.AreEqual(new[] { "SCRIPT_000", "TEAK" }, t.ConstantsOf(11000));
            Assert.That(t.ConstantsOf(999), Is.Empty);
        }

        [Test]
        public void ScaleDefaultsToOne()
        {
            const string lua = "EFFECT = {}\nEFFECT.EF_X = 1\ntbl = {\n\t[1002] = { MonsterSize = 1.2, MonsterEff = { EFFECT.EF_X } },\n" +
                               "\t[1003] = { MonsterEff = { EFFECT.EF_X } }\n}\nmain = function() end";
            Assert.That(RoMonsterScaleTable.TryRead(lua, out var t, out string reason), Is.True, reason);
            Assert.That(t.ScaleOf(1002), Is.EqualTo(1.2f));
            Assert.That(t.ScaleOf(1003), Is.EqualTo(1f), "no size");
            Assert.That(t.ScaleOf(4242), Is.EqualTo(1f), "not listed");
        }

        [Test, Category(CatalogTestFiles.Category)]
        public void ReadsTheClientTables()
        {
            Assert.That(RoEmoteTable.TryRead(CatalogTestFiles.TableLua("emotion/emotionlist.lub"), out var emotes, out string reason), Is.True, reason);
            Assert.That(emotes.Ids["ET_SURPRISE"], Is.EqualTo(0));

            Assert.That(RoSkillTable.TryRead(CatalogTestFiles.TableLua("skillinfoz/skillid.lub"), out var skills, out reason), Is.True, reason);
            Assert.That(skills.Ids.Count, Is.EqualTo(1806), "T0.4 count");
            Assert.That(skills.ConstantsOf(5).Select(RoSkillTable.IconName), Does.Contain("sm_bash"));

            Assert.That(RoMonsterScaleTable.TryRead(CatalogTestFiles.SystemLua(RoMonsterScaleTable.File, RoLubEncoding.Cp949), out var scales, out reason),
                Is.True, reason);
            Assert.That(scales.Scales.Values, Does.Contain(1.2f));
            Assert.That(scales.ScaleOf(1002), Is.EqualTo(1f), "Poring is not in the table");
            Assert.That(scales.ScaleOf(21537), Is.EqualTo(1.5f), "the entry with the bare EF_LEVEL99_RED (D17)");
        }
    }
}
