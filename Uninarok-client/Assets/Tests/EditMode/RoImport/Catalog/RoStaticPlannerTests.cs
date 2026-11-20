using System.Linq;
using NUnit.Framework;
using Uninarok.RoImport.Core.Catalog.Planning;
using Uninarok.RoImport.Core.Catalog.Tables;
using Uninarok.RoImport.Core.Results;
using static Uninarok.RoImport.Tests.Catalog.PlannerFixtures;

namespace Uninarok.RoImport.Tests.Catalog
{
    // TASKS-0008 T4.2 (FR-5, FR-7, FR-10; PLAN-0008 4.3.8): ground items and skill icons from synthetic tables.
    [Ignore("pending T4.4")]
    public class RoStaticPlannerTests
    {
        static readonly string[] ItemFiles = Pairs("sprite/아이템/빨간포션", "sprite/아이템/단검", "sprite/아이템/sm_bash", "sprite/아이템/al_heal")
            .Concat(new[] { "sprite/아이템/외톨이.act", "sprite/아이템/그림만.spr" }).ToArray();

        static RoCatalogTables ItemTables() => RoCatalogTables.Create(items: Items(
            "[501] = { identifiedResourceName = \"빨간포션\", unidentifiedResourceName = \"빨간포션\", ClassNum = 0 }, " +
            "[502] = { identifiedResourceName = \"빨간포션\", unidentifiedResourceName = \"빨간포션\", ClassNum = 0 }, " +
            "[1201] = { identifiedResourceName = \"단검\", unidentifiedResourceName = \"검\", ClassNum = 1 }"));

        static RoCatalogPlan GroundItems() => Plan(new RoGroundItemPlanner(), ItemTables(), ItemFiles);

        [Test]
        public void ItemsSharingAResource_ShareOneRecord()
        {
            var plan = GroundItems();

            Assert.That(plan.Records.Count(r => r.Key == "빨간포션"), Is.EqualTo(1), "501 and 502");
            Assert.That(plan.Records.Single(r => r.Key == "빨간포션").Pairs.Single().Spr.Canonical, Is.EqualTo("sprite/아이템/빨간포션.spr"));
        }

        [Test]
        public void IdentifiedAndUnidentified_AreBothPlanned()
        {
            var plan = GroundItems();

            Assert.That(plan.Records.Any(r => r.Key == "단검"), Is.True, "identified");
            Assert.That(plan.Gaps.Single(g => g.Code == RoIssueCode.CatalogNameWithoutFile).Item, Does.Contain("검.spr"), "unidentified 검 has no file");
            Assert.That(plan.Records.Count, Is.EqualTo(2));
        }

        [Test]
        public void OrphansInTheItemFolder_AreReported()
        {
            var plan = GroundItems();

            Assert.That(plan.Gaps.Single(g => g.Code == RoIssueCode.CatalogOrphanAct).Item, Is.EqualTo("sprite/아이템/외톨이.act"));
            Assert.That(plan.Gaps.Single(g => g.Code == RoIssueCode.CatalogOrphanSpr).Item, Is.EqualTo("sprite/아이템/그림만.spr"));
        }

        [Test]
        public void ASkillIcon_IsTheLowerCaseSkid()
        {
            var tables = RoCatalogTables.Create(skills: Skills("SM_BASH = 5, AL_HEAL = 28, MG_NOICON = 10, NPC_PIERCINGATT = 158, NPC_COMBOATTACK = 171"));
            var plan = Plan(new RoSkillIconPlanner(), tables, ItemFiles);

            Assert.That(plan.Records.Select(r => r.Key), Is.EquivalentTo(new[] { "5", "28" }));
            Assert.That(plan.Records.Single(r => r.Key == "5").Pairs.Single().Spr.Canonical, Is.EqualTo("sprite/아이템/sm_bash.spr"));
            Assert.That(plan.Records.Single(r => r.Key == "5").Value("constant"), Is.EqualTo("SM_BASH"));
            Assert.That(plan.Gaps.Count(g => g.Item.Contains("mg_noicon")), Is.EqualTo(1), "a player skill without an icon is warned");
            Assert.That(plan.Gaps.Count(g => g.Item.Contains("npc_")), Is.EqualTo(0), "monster skills are not warned one by one");
            Assert.That(plan.Gaps.Single(g => g.Item == RoSkillIconPlanner.MonsterSkillPrefix + "*").Message, Does.Contain("2"));
        }

        [Test]
        [Ignore("pending T4.5")]
        public void EffectsAreByName_WithStillSprites_AndOrphans()
        {
            var files = Pairs("sprite/이팩트/cart", "sprite/이팩트/sub/glow", "sprite/이팩트/emotion")
                .Concat(new[] { "sprite/이팩트/bluemsg.spr", "sprite/이팩트/emotion_index.act" });
            var plan = Plan(new RoEffectPlanner(), RoCatalogTables.Create(emotes: Emotes("ET_SURPRISE = 0\nET_QUESTION = 1")), files);

            Assert.That(plan.Records.Select(r => r.Key), Is.EquivalentTo(new[] { "bluemsg", "cart", "emotion", "sub/glow" }));
            Assert.That(plan.Records.Single(r => r.Key == "bluemsg").Pairs.Single().IsStatic, Is.True);
            Assert.That(plan.Gaps.Single(g => g.Code == RoIssueCode.CatalogOrphanAct).Item, Is.EqualTo("sprite/이팩트/emotion_index.act"));
        }

        [Test]
        [Ignore("pending T4.5")]
        public void TheEmoteSet_CarriesTheEmoteIds()
        {
            var emotes = RoCatalogTables.Create(emotes: Emotes("ET_SURPRISE = 0\nET_QUESTION = 1"));
            var plan = Plan(new RoEffectPlanner(), emotes, Pairs("sprite/이팩트/emotion"));
            var set = plan.Records.Single(r => r.Key == RoEffectPlanner.EmoteSet);

            Assert.That(set.Value(RoEffectPlanner.EmoteValuePrefix + "ET_SURPRISE"), Is.EqualTo("0"));
            Assert.That(set.Value(RoEffectPlanner.EmoteValuePrefix + "ET_QUESTION"), Is.EqualTo("1"));

            var missing = Plan(new RoEffectPlanner(), emotes, Pairs("sprite/이팩트/cart"));
            Assert.That(missing.Gaps.Single(g => g.Code == RoIssueCode.CatalogNameWithoutFile).Item, Does.Contain("emotion"));
        }
    }
}
