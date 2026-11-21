using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Uninarok.Catalog;
using Uninarok.Networking.Data;
using Uninarok.RoImport.Core.Catalog.Planning;
using Uninarok.RoImport.Core.Catalog.Tables;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Source;
using Uninarok.RoImport.Editor.Catalog;
using Uninarok.Sprites;

namespace Uninarok.RoImport.Tests.Catalog
{
    // TASKS-0008 T7.1 (FR-1 to FR-7, FR-10; spec §4 acceptance, plan §6): the reference lookups on the catalog imported into the
    // project from the real data (T7.2 to T7.4). Each is ignored until its category is imported (T7.2 to T7.4: all enabled).
    public class RoCatalogReferenceTests
    {
        const string Art = "Assets/Art/Imported/";
        const AccountSex M = AccountSex.SEX_MALE, F = AccountSex.SEX_FEMALE;

        static SpriteCatalog Catalog()
        {
            var catalog = RoCatalogLookupWindow.LoadInEditor(new RoOutputRoots());
            if (catalog == null) Assert.Ignore("the catalog is not imported into the project");
            return catalog;
        }

        static T Load<T>(UnityEngine.AddressableAssets.AssetReference r) where T : Object =>
            r == null || string.IsNullOrEmpty(r.AssetGUID) ? null : AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(r.AssetGUID));

        static string PathOf(UnityEngine.AddressableAssets.AssetReference r) => r == null ? null : AssetDatabase.GUIDToAssetPath(r.AssetGUID);

        static RoCatalogLookupResult Lookup(RoCatalogLookupRequest q)
        {
            var result = RoCatalogLookupWindow.Resolve(q, new RoOutputRoots(), out string message);
            Assert.That(result, Is.Not.Null, message);
            return result;
        }

        // ---- T7.2: units, ground items, skill icons, effects, headgears, shields ----

        [Test, Category(CatalogTestFiles.Category)]
        public void Poring1002_IsItsSpriteAtScale1()
        {
            Assert.That(Catalog().TryGetUnit(1002, out var poring), Is.True);
            Assert.That(poring.Scale, Is.EqualTo(1f));
            Assert.That(PathOf(poring.Sprite.Animation), Is.EqualTo(Art + "sprite/몬스터/poring.asset"));
        }

        [Test, Category(CatalogTestFiles.Category)]
        public void ANameInBothFolders_IsTheOneItsIdRangeSelects()
        {
            var catalog = Catalog();
            if (!catalog.TryGetUnit(105, out var npc)) Assert.Ignore("the NPCs are not imported");
            Assert.That(PathOf(npc.Sprite.Animation), Is.EqualTo(Art + "sprite/npc/8w_soldier.asset"));
            Assert.That(catalog.TryGetUnit(2245, out var monster), Is.True);
            Assert.That(PathOf(monster.Sprite.Animation), Is.EqualTo(Art + "sprite/몬스터/8w_soldier.asset"));
        }

        [Test, Category(CatalogTestFiles.Category)]
        public void Item501_ShowsTheRedPotionOnTheGround()
        {
            Assert.That(Catalog().TryGetGroundItem(501, true, out var potion), Is.True);
            string sheet = potion.HasStill ? PathOf(potion.Still) : AssetDatabase.GetAssetPath(Load<SpriteAnimation>(potion.Animation).Sprites[0].texture);
            Assert.That(sheet, Does.Contain("빨간포션"), "the red potion's sheet");
        }

        [Test, Category(CatalogTestFiles.Category)]
        public void Skill5_Bash_ShowsItsIcon()
        {
            Assert.That(Catalog().TryGetSkillIcon(5, out var bash), Is.True);
            Assert.That(bash.IsEmpty, Is.False);
            string path = bash.HasStill ? PathOf(bash.Still) : PathOf(bash.Animation);
            Assert.That(path, Does.Contain("sm_bash"));
        }

        [Test, Category(CatalogTestFiles.Category)]
        public void Emote0_IsTheSurpriseActionOfTheEmoteSet_AndCart1IsFoundByName()
        {
            var catalog = Catalog();
            Assert.That(catalog.TryGetEmote(0, out var set, out int action), Is.True);
            Assert.That(action, Is.EqualTo(0));
            Assert.That(PathOf(set.Animation), Is.EqualTo(Art + "sprite/이팩트/emotion.asset"));
            Assert.That(catalog.TryGetEffect("손수레1", out var cart) && !cart.IsEmpty, Is.True, "cart 1");
        }

        [Test, Category(CatalogTestFiles.Category)]
        public void Item2220_ForAMaleKnight_IsTheHatOnItsOwn()
        {
            var hat = Lookup(new RoCatalogLookupRequest { Mode = RoCatalogLookupMode.ItemId, Id = 2220, Job = PlayerJob.JOB_KNIGHT, Sex = M });
            Assert.That(AssetDatabase.GetAssetPath(hat.Animation), Does.StartWith(Art + "sprite/악세사리/남/"));
            Assert.That(hat.Trail, Is.Null);
        }

        [Test, Category(CatalogTestFiles.Category)]
        public void TheShieldOfItem2189_ForAMaleSwordman_IsItsItemIdFile()
        {
            var shield = Lookup(new RoCatalogLookupRequest { Mode = RoCatalogLookupMode.ItemId, Id = 2189, Job = PlayerJob.JOB_SWORDMAN, Sex = M });
            Assert.That(AssetDatabase.GetAssetPath(shield.Animation), Does.StartWith(Art + "sprite/방패/"));
        }

        // ---- T7.3: bodies, heads, palettes, weapons ----

        // D23 (owner, T7.3): the summoner's cloth palettes are named 묘족 in the client, not after its body file, so for now it
        // has none; the novice and the knight on peco keep theirs.
        [Test, Category(CatalogTestFiles.Category)]
        public void NoviceMale_KnightOnPecoFemale_SummonerMale_ReturnTheirBodies_WithPalettes()
        {
            foreach (var (job, sex, file, palettes) in new[]
                     {
                         (PlayerJob.JOB_NOVICE, M, "sprite/인간족/몸통/남/초보자_남.asset", true),
                         (PlayerJob.JOB_KNIGHT2, F, "sprite/인간족/몸통/여/페코페코_기사_여.asset", true),
                         (PlayerJob.JOB_SUMMONER, M, "sprite/도람족/몸통/남/summoner_남.asset", false),
                     })
            {
                var body = Lookup(new RoCatalogLookupRequest { Mode = RoCatalogLookupMode.BodyClass, Job = job, Sex = sex });
                Assert.That(AssetDatabase.GetAssetPath(body.Animation), Is.EqualTo(Art + file), job.ToString());
                if (palettes) Assert.That(body.Description, Does.Not.Contain(" 0 palettes"), $"{job}: a cloth palette applies");
                else Assert.That(body.Description, Does.Contain(" 0 palettes"), $"{job}: no palette for now (D23)");
            }
        }

        [Test, Category(CatalogTestFiles.Category)]
        public void HairStyle1Male_ReturnsItsHead_WithAHairPalette()
        {
            var head = Lookup(new RoCatalogLookupRequest { Mode = RoCatalogLookupMode.HeadStyle, Id = 1, Sex = M, Race = SpriteRace.Human });
            Assert.That(AssetDatabase.GetAssetPath(head.Animation), Is.EqualTo(Art + "sprite/인간족/머리통/남/1_남.asset"));
            Assert.That(head.Description, Does.Not.Contain(" 0 palettes"));
        }

        [Test, Category(CatalogTestFiles.Category)]
        public void Item1201_ForAMaleSwordman_IsTheShortSword()
        {
            var knife = Lookup(new RoCatalogLookupRequest { Mode = RoCatalogLookupMode.ItemId, Id = 1201, Job = PlayerJob.JOB_SWORDMAN, Sex = M });
            Assert.That(AssetDatabase.GetAssetPath(knife.Animation), Is.EqualTo(Art + "sprite/인간족/검사/검사_남_단검.asset"));
        }

        // ---- T7.4: garments and the full run ----

        [Test, Category(CatalogTestFiles.Category)]
        public void TheAngelWings_ForAKnightAndASummoner_AreTheirOwnAnimations_OnDistinctSheetsPerSpr()
        {
            var knight = Lookup(new RoCatalogLookupRequest { Mode = RoCatalogLookupMode.ViewId, Kind = EquipmentKind.Garment, Id = 1, Job = PlayerJob.JOB_KNIGHT, Sex = M });
            Assert.That(AssetDatabase.GetAssetPath(knight.Animation), Is.EqualTo(Art + "sprite/로브/천사날개/남/기사_남.asset"));
            var summoner = Lookup(new RoCatalogLookupRequest { Mode = RoCatalogLookupMode.ViewId, Kind = EquipmentKind.Garment, Id = 1, Job = PlayerJob.JOB_SUMMONER, Sex = M });
            Assert.That(AssetDatabase.GetAssetPath(summoner.Animation), Is.EqualTo(Art + "sprite/로브/천사날개/남/summoner_남.asset"));

            // FR-8: no sheet is duplicated: the garment's distinct sheet textures equal the distinct hashes of the SPRs it uses.
            Assert.That(Catalog().TryFindEquipment(EquipmentKind.Garment, 1, 0, out var reference), Is.True);
            var entry = Load<EquipmentEntry>(reference);
            var sheets = entry.Slots.Select(s => Load<SpriteAnimation>(s.Animation)).Where(a => a != null)
                .Select(a => AssetDatabase.GetAssetPath(a.Sprites[0].texture)).Distinct().Count();
            var data = new RoFolderSource(CatalogTestFiles.DataRoot);
            var tables = RoCatalogTables.Load(data, null, RoCatalogNeeds.ViewIds | RoCatalogNeeds.Own, RoItemTableLanguage.Portuguese, new RoReport());
            var b = new RoCatalogPlan.Builder();
            new RoGarmentPlanner().Plan(new RoCatalogContext(data, tables), b);
            var hashes = b.Build().Records.Single(r => r.Key == RoCatalogKeys.Garment(1)).Pairs
                .Select(p => data.TryGetFile(p.Spr, out var f) ? f.ContentHash() : null).Distinct().Count();
            Assert.That(sheets, Is.EqualTo(hashes));
        }

        [Test, Category(CatalogTestFiles.Category)]
        public void AFullPlan_ListsEachGapOnce_GroupedByCategory()
        {
            var data = new RoFolderSource(CatalogTestFiles.DataRoot);
            var tables = RoCatalogTables.Load(data, new RoFolderSource(CatalogTestFiles.SystemRoot), RoCatalogNeeds.All, RoItemTableLanguage.Portuguese,
                new RoReport(), null, CatalogTestFiles.ItemDbPath);
            var ctx = new RoCatalogContext(data, tables);
            var b = new RoCatalogPlan.Builder();
            foreach (SpriteCatalogCategory c in System.Enum.GetValues(typeof(SpriteCatalogCategory))) RoCatalogImporter.PlannerOf(c).Plan(ctx, b);
            var plan = b.Build();

            Assert.That(plan.Gaps.Select(g => (g.Code, g.Item)).Distinct().Count(), Is.EqualTo(plan.Gaps.Count), "each gap once");
            var categories = plan.Gaps.Select(g => g.Category).ToList();
            Assert.That(categories, Is.Ordered, "grouped by category");
        }
    }
}
