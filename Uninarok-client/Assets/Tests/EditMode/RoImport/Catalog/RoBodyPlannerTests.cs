using System.Linq;
using NUnit.Framework;
using Uninarok.Catalog;
using Uninarok.Networking.Data;
using Uninarok.RoImport.Core.Catalog.Planning;
using Uninarok.RoImport.Core.Catalog.Tables;
using Uninarok.RoImport.Core.Results;
using static Uninarok.RoImport.Tests.Catalog.PlannerFixtures;

namespace Uninarok.RoImport.Tests.Catalog
{
    // TASKS-0008 T4.6 (FR-2, FR-10; PLAN-0008 4.3.2, 4.3.11): class bodies from the class table and synthetic files.
    [Ignore("pending T4.7")]
    public class RoBodyPlannerTests
    {
        const AccountSex M = AccountSex.SEX_MALE, F = AccountSex.SEX_FEMALE;
        const SpriteRace H = SpriteRace.Human, D = SpriteRace.Doram;

        static readonly string[] Files = Pairs(
                "sprite/인간족/몸통/남/기사_남", "sprite/인간족/몸통/여/기사_여", "sprite/인간족/몸통/남/페코페코_기사_남",
                "sprite/인간족/몸통/남/기사_h_남", "sprite/인간족/몸통/남/costume_1/기사_남_1",
                "sprite/도람족/몸통/남/summoner_남", "sprite/도람족/몸통/남/결혼_남", "sprite/인간족/몸통/남/결혼_남",
                "sprite/인간족/몸통/남/rebellion_남_권총", "sprite/인간족/몸통/_여/어쌔신크로스_여", "sprite/인간족/몸통/남/abyss_chaser_남")
            .Concat(new[]
            {
                "palette/몸/기사_남_0.pal", "palette/몸/기사_남_1.pal", "palette/몸/costume_1/기사_남_0_1.pal",
                "palette/도람족/body/summoner_남_0.pal",
            }).ToArray();

        static RoCatalogPlan Bodies() => Plan(new RoBodyPlanner(), RoCatalogTables.Create(own: Own()), Files);

        static RoCatalogRecord Record(RoCatalogPlan plan, PlayerJob baseJob) => plan.Records.Single(r => r.Key == RoCatalogKeys.Body(baseJob));

        [Test]
        public void TheClassTableRows_BecomeTheVariantsOfTheirBaseClass()
        {
            var knight = Record(Bodies(), PlayerJob.JOB_KNIGHT);

            Assert.That(knight.Key, Is.EqualTo("body-0007"));
            Assert.That(knight.Pairs.Select(p => p.Role), Is.EquivalentTo(new[]
            {
                RoCatalogRoles.Body(PlayerJob.JOB_KNIGHT, M, H, BodyVariantKind.Base),
                RoCatalogRoles.Body(PlayerJob.JOB_KNIGHT, F, H, BodyVariantKind.Base),
                RoCatalogRoles.Body(PlayerJob.JOB_KNIGHT2, M, H, BodyVariantKind.Mount),
                RoCatalogRoles.Body(PlayerJob.JOB_KNIGHT, M, H, BodyVariantKind.Outfit),
                RoCatalogRoles.Body(PlayerJob.JOB_KNIGHT, M, H, BodyVariantKind.Costume1),
            }));
            string mount = RoCatalogRoles.Body(PlayerJob.JOB_KNIGHT2, M, H, BodyVariantKind.Mount);
            Assert.That(knight.Value(RoCatalogRoles.Name(mount)), Is.EqualTo("페코페코_기사"));
            Assert.That(knight.Pairs.Single(p => p.Role == mount).Spr.Canonical, Is.EqualTo("sprite/인간족/몸통/남/페코페코_기사_남.spr"));
            Assert.That(knight.Value("baseJob"), Is.EqualTo(nameof(PlayerJob.JOB_KNIGHT)));
        }

        [Test]
        public void ACostume_IsInItsSubfolder()
        {
            var knight = Record(Bodies(), PlayerJob.JOB_KNIGHT);
            string costume = RoCatalogRoles.Body(PlayerJob.JOB_KNIGHT, M, H, BodyVariantKind.Costume1);

            Assert.That(knight.Pairs.Single(p => p.Role == costume).Act.Canonical, Is.EqualTo("sprite/인간족/몸통/남/costume_1/기사_남_1.act"));
            Assert.That(knight.Value(RoCatalogRoles.Palette(costume, 0)), Is.EqualTo("palette/몸/costume_1/기사_남_0_1.pal"));
        }

        [Test]
        public void PalettesAreListedByColourNumber_PerVariantAndSex()
        {
            var plan = Bodies();
            string male = RoCatalogRoles.Body(PlayerJob.JOB_KNIGHT, M, H, BodyVariantKind.Base);
            string female = RoCatalogRoles.Body(PlayerJob.JOB_KNIGHT, F, H, BodyVariantKind.Base);
            var knight = Record(plan, PlayerJob.JOB_KNIGHT);

            Assert.That(knight.Value(RoCatalogRoles.Palette(male, 0)), Is.EqualTo("palette/몸/기사_남_0.pal"));
            Assert.That(knight.Value(RoCatalogRoles.Palette(male, 1)), Is.EqualTo("palette/몸/기사_남_1.pal"));
            Assert.That(knight.Values.Any(v => v.Name.StartsWith(RoCatalogRoles.PalettePrefix + female)), Is.False, "no female palette file");
            string summoner = RoCatalogRoles.Body(PlayerJob.JOB_SUMMONER, M, D, BodyVariantKind.Base);
            Assert.That(Record(plan, PlayerJob.JOB_SUMMONER).Value(RoCatalogRoles.Palette(summoner, 0)), Is.EqualTo("palette/도람족/body/summoner_남_0.pal"));
        }

        [Test]
        public void ADoramBody_IsInTheDoramFolder_AndBothRacesShareAClass()
        {
            var plan = Bodies();

            Assert.That(Record(plan, PlayerJob.JOB_SUMMONER).Pairs.Single().Spr.Canonical, Is.EqualTo("sprite/도람족/몸통/남/summoner_남.spr"));
            Assert.That(Record(plan, PlayerJob.JOB_WEDDING).Pairs.Select(p => p.Role), Is.EquivalentTo(new[]
            {
                RoCatalogRoles.Body(PlayerJob.JOB_WEDDING, M, H, BodyVariantKind.Base),
                RoCatalogRoles.Body(PlayerJob.JOB_WEDDING, M, D, BodyVariantKind.Base),
            }));
        }

        [Test]
        public void StrayAndUnnamedFiles_AreOneWarningEach()
        {
            var plan = Bodies();

            Assert.That(plan.Gaps.Where(g => g.Code == RoIssueCode.CatalogStrayFile).Select(g => g.Item), Is.EquivalentTo(new[]
            {
                "sprite/인간족/몸통/남/rebellion_남_권총.spr", "sprite/인간족/몸통/_여/어쌔신크로스_여.spr",
            }), "a weapon in the body folder, the _여 folder");
            Assert.That(plan.Gaps.Single(g => g.Code == RoIssueCode.CatalogFileWithoutName).Item, Is.EqualTo("sprite/인간족/몸통/남/abyss_chaser_남.spr"));
            Assert.That(plan.Gaps.Single(g => g.Code == RoIssueCode.CatalogNameWithoutFile).Item, Does.Contain("검사"), "a class row with no body file");
            Assert.That(plan.Records.Any(r => r.Key == RoCatalogKeys.Body(PlayerJob.JOB_SWORDMAN)), Is.False);
        }
    }

    // TASKS-0008 T4.6 (FR-3, FR-10; PLAN-0008 4.3.3): hair styles from synthetic files.
    [Ignore("pending T4.7")]
    public class RoHeadPlannerTests
    {
        static readonly string[] Files = Pairs("sprite/인간족/머리통/남/1_남", "sprite/인간족/머리통/여/1_여", "sprite/도람족/머리통/남/1_남",
                "sprite/인간족/머리통/남/2_남", "sprite/인간족/머리통/남/old_남")
            .Concat(new[] { "palette/머리/머리1_남_0.pal", "palette/머리/머리1_남_3.pal", "palette/도람족/머리/머리1_남_0.pal" }).ToArray();

        static RoCatalogPlan Heads() => Plan(new RoHeadPlanner(), RoCatalogTables.Create(), Files);

        [Test]
        public void OneRecordPerStyle_WithAPairPerSexAndRace()
        {
            var plan = Heads();

            Assert.That(plan.Records.Select(r => r.Key), Is.EqualTo(new[] { "head-01", "head-02" }));
            Assert.That(plan.Records[0].Pairs.Select(p => p.Role), Is.EquivalentTo(new[]
            {
                RoCatalogRoles.Head(AccountSex.SEX_MALE, SpriteRace.Human), RoCatalogRoles.Head(AccountSex.SEX_FEMALE, SpriteRace.Human),
                RoCatalogRoles.Head(AccountSex.SEX_MALE, SpriteRace.Doram),
            }));
            Assert.That(plan.Records[0].Value("style"), Is.EqualTo("1"));
        }

        [Test]
        public void HairPalettes_AreByColourNumber()
        {
            var head = Heads().Records[0];
            string human = RoCatalogRoles.Head(AccountSex.SEX_MALE, SpriteRace.Human);

            Assert.That(head.Value(RoCatalogRoles.Palette(human, 3)), Is.EqualTo("palette/머리/머리1_남_3.pal"));
            Assert.That(head.Value(RoCatalogRoles.Palette(RoCatalogRoles.Head(AccountSex.SEX_MALE, SpriteRace.Doram), 0)), Is.EqualTo("palette/도람족/머리/머리1_남_0.pal"));
        }

        [Test]
        public void ANonNumericName_IsStray()
        {
            Assert.That(Heads().Gaps.Single(g => g.Code == RoIssueCode.CatalogStrayFile).Item, Is.EqualTo("sprite/인간족/머리통/남/old_남.spr"));
        }
    }
}
