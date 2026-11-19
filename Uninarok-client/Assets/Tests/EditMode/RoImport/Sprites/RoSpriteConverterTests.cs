using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Source;
using Uninarok.RoImport.Core.Sprites;
using Uninarok.RoImport.Editor.Pipeline;
using Uninarok.RoImport.Editor.Sprites;
using Uninarok.Sprites;
using Object = UnityEngine.Object;

namespace Uninarok.RoImport.Tests.Sprites
{
    // TASKS-0007 T5.1 / T5.2 (FR-2, FR-3, FR-8, FR-9, FR-11, plan 4.4): synthetic files written to a temporary data folder
    // and converted into Assets/__RoImportTests; the ledger and the sheet index go to a temporary folder.
    [Ignore("pending T5.2")]
    public class RoSpriteConverterTests
    {
        RoOutputRoots _roots;
        string _temp;
        string _data;

        const string Monster = "sprite/monster/mon";
        const string Copy = "sprite/copy/mon2";
        const string GarmentRoot = "sprite/garment/wing";
        const string GarmentClass = "sprite/garment/male/novice_male";
        const string Item = "sprite/item/apple";
        const string Shoes = "sprite/item/shoes";
        const string Weapon = "sprite/weapon/sword";

        [SetUp]
        public void SetUp()
        {
            _roots = RoImportTestRoot.Create();
            _temp = Path.Combine(Path.GetTempPath(), "RoImportTests", Guid.NewGuid().ToString("N"));
            _data = Path.Combine(_temp, "data");
            Directory.CreateDirectory(_data);

            WritePair(Monster, SpriteTestData.Monster());
            WritePair(Copy, SpriteTestData.Monster());
            WritePair(GarmentRoot, SpriteTestData.Anchored());
            WriteFile(GarmentClass + ".act", SpriteFileWriter.Act(SpriteTestData.Anchored().act));
            WritePair(Item, SpriteTestData.Static());
            WritePair(Shoes, SpriteTestData.Static(0, -7));
            WritePair(Weapon, SpriteTestData.Weapon());
            WritePair("sprite/bad/oob", SpriteTestData.OutOfRange());
            WriteFile("sprite/bad/corrupt.spr", new byte[] { (byte)'S', (byte)'P', 1, 2, 9, 0, 0, 0, 1, 2, 3 });
            WriteFile("sprite/bad/corrupt.act", SpriteFileWriter.Act(SpriteTestData.Static().act));
            WriteFile("sprite/bad/nospr.act", SpriteFileWriter.Act(SpriteTestData.Static().act));
            var static_ = SpriteTestData.Static();
            WriteFile("sprite/bad/oldspr.spr", SpriteFileWriter.Spr(static_.spr, major: 1, minor: 1));
            WriteFile("sprite/bad/oldspr.act", SpriteFileWriter.Act(static_.act));
        }

        [TearDown]
        public void TearDown()
        {
            RoImportTestRoot.Delete();
            if (Directory.Exists(_temp)) Directory.Delete(_temp, recursive: true);
        }

        void WriteFile(string relative, byte[] bytes)
        {
            string path = Path.Combine(_data, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, bytes);
        }

        void WritePair(string name, (RoSpr spr, RoAct act) sample)
        {
            WriteFile(name + ".spr", SpriteFileWriter.Spr(sample.spr));
            WriteFile(name + ".act", SpriteFileWriter.Act(sample.act));
        }

        string LedgerFolder => Path.Combine(_temp, "ledger");

        RoImportContext Context(bool force = false) =>
            new RoImportContext(new RoFolderSource(_data), _roots, LedgerFolder) { Force = force };

        static RoPath P(string path) => RoPath.Parse(path);

        string Hash(string relative) => RoHashCache.HashOf(Path.Combine(_data, relative));

        string AnimationPath(string name) => RoSpriteOutputPaths.Animation(_roots, P(name + ".act"));

        string SheetPath(string sprName, string hashOf = null) => RoSpriteOutputPaths.Sheet(_roots, P(sprName + ".spr"), Hash((hashOf ?? sprName) + ".spr"));

        static RoResult<Object> Convert(RoImportContext ctx, string name) => new RoSpriteConverter().Convert(ctx, P(name + ".act"));

        static bool Exists(string projectPath) => File.Exists(RoImportTestRoot.FullPath(projectPath));

        static long FileId(Object o)
        {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o, out string _, out long id);
            return id;
        }

        static int Count(RoReport report, RoIssueCode code) => report.Issues.Count(i => i.Code == code);

        // ---- FR-3, plan §3: a pair becomes a sheet and an animation ----

        [Test]
        public void APair_BecomesASheetAndAnAnimation_AtThePlanPaths()
        {
            var ctx = Context();
            var result = Convert(ctx, Monster);

            Assert.AreEqual(RoStatus.Created, result.Status, result.Failure?.ToString());
            var anim = result.Value as SpriteAnimation;
            Assert.IsNotNull(anim, "the result is the animation");
            Assert.AreEqual(AnimationPath(Monster), AssetDatabase.GetAssetPath(anim));
            Assert.IsTrue(Exists(SheetPath(Monster)), "sheet named by content");

            Assert.AreEqual(SpriteTestData.MonsterActions, anim.FileActionCount);
            Assert.AreEqual(SpriteTestData.MonsterDistinctActions, anim.Actions.Length);
            Assert.IsTrue(anim.Directional);
            Assert.AreEqual(RoSpriteRules.PixelsPerUnit, anim.PixelsPerUnit);
            Assert.AreEqual(8, anim.Sprites.Length);
            Assert.IsTrue(anim.Sprites.All(s => s != null && AssetDatabase.GetAssetPath(s) == SheetPath(Monster)));
            Assert.AreEqual("f3", anim.Sprites[3].name);
            Assert.AreEqual(1, RoSpriteSheetIndex.Load(LedgerFolder).Count);
        }

        // ---- FR-2: shared images ----

        [Test]
        public void AByteIdenticalSpr_ReusesTheSheet()
        {
            var ctx = Context();
            var a = (SpriteAnimation)Convert(ctx, Monster).Value;
            var b = (SpriteAnimation)Convert(ctx, Copy).Value;

            Assert.AreEqual(Hash(Monster + ".spr"), Hash(Copy + ".spr"));
            Assert.AreSame(a.Sprites[0], b.Sprites[0], "one image set");
            Assert.IsFalse(Exists(RoSpriteOutputPaths.Sheet(_roots, P(Copy + ".spr"), Hash(Copy + ".spr"))), "no second sheet");
            Assert.AreEqual(1, RoSpriteSheetIndex.Load(LedgerFolder).Count);
        }

        [Test]
        public void AnActWithoutItsOwnSpr_ConvertsAgainstAnotherFilesSheet()
        {
            var ctx = Context();
            var result = RoSpriteConverter.Convert(ctx, P(GarmentClass + ".act"), P(GarmentRoot + ".spr"), null);

            Assert.AreEqual(RoStatus.Created, result.Status, result.Failure?.ToString());
            var anim = (SpriteAnimation)result.Value;
            Assert.AreEqual(AnimationPath(GarmentClass), AssetDatabase.GetAssetPath(anim));
            Assert.AreEqual(SheetPath(GarmentRoot), AssetDatabase.GetAssetPath(anim.Sprites[0]));
        }

        [Test]
        public void WithPalettes_TheSheetGetsItsIndexTexture()
        {
            var ctx = Context();
            var anim = (SpriteAnimation)RoSpriteConverter.Convert(ctx, P(Monster + ".act"), P(Monster + ".spr"), new RoSpriteOptions { Palettes = true }).Value;

            Assert.IsTrue(Exists(RoSpriteOutputPaths.IndexOf(SheetPath(Monster))));
            Assert.AreEqual(1, anim.Sprites[0].GetSecondaryTextureCount());
            Assert.IsTrue(RoSpriteSheetIndex.Load(LedgerFolder).Find(Hash(Monster + ".spr")).HasIndex);
        }

        // ---- FR-5: empty is data ----

        [Test]
        public void AWeapon_KeepsItsEmptyActions()
        {
            var anim = (SpriteAnimation)Convert(Context(), Weapon).Value;

            Assert.AreEqual(104, anim.FileActionCount);
            Assert.IsTrue(anim.TryGetAction(0, 0, SpriteMissingAction.DrawNothing, out int idle));
            Assert.AreEqual(-1, anim.Layers[anim.Frames[anim.Actions[idle].FirstFrame].FirstLayer].Slot, "idle is empty, not missing");
        }

        // ---- FR-11: static sprites ----

        [Test]
        public void AStaticAct_GivesASprite_AndNoAnimationAsset()
        {
            var result = Convert(Context(), Item);

            Assert.AreEqual(RoStatus.Created, result.Status, result.Failure?.ToString());
            var sprite = result.Value as Sprite;
            Assert.IsNotNull(sprite);
            Assert.AreEqual("f0", sprite.name);
            Assert.IsFalse(Exists(AnimationPath(Item)));
        }

        [Test]
        public void AStaticActWithAnOffset_GivesItsOffsetSprite()
        {
            var sprite = Convert(Context(), Shoes).Value as Sprite;

            Assert.IsNotNull(sprite);
            Assert.AreEqual("f0@0_-7", sprite.name);
            Assert.AreEqual(16f / 2f - 7f, sprite.pivot.y, 1e-4f, "the origin 7 pixels below the centre");
            Assert.AreEqual(14f / 2f, sprite.pivot.x, 1e-4f);
        }

        // ---- FR-8: bad input ----

        [TestCase("sprite/bad/oob", RoIssueCode.SpriteIndexOutOfRange)]
        [TestCase("sprite/bad/corrupt", RoIssueCode.CorruptFile)]
        [TestCase("sprite/bad/nospr", RoIssueCode.SourceMissing)]
        [TestCase("sprite/bad/oldspr", RoIssueCode.UnsupportedSprVersion)]
        public void BadInput_IsOneIssue_AndNoOutput(string name, RoIssueCode code)
        {
            var ctx = Context();
            var result = Convert(ctx, name);

            Assert.AreEqual(RoStatus.Failed, result.Status);
            Assert.AreEqual(code, result.Failure.Code);
            Assert.AreEqual(1, Count(ctx.Report, code), "one warning");
            Assert.IsFalse(Exists(AnimationPath(name)));
            Assert.IsFalse(Directory.Exists(RoImportTestRoot.FullPath(_roots.Art + "/" + RoSpriteOutputPaths.SheetsFolder)), "no sheet either");
        }

        // ---- FR-9: re-import is stable ----

        [Test]
        public void ASecondRun_IsSkipped()
        {
            Assert.AreEqual(RoStatus.Created, Convert(Context(), Monster).Status);
            Context().SaveLedgers();

            var ctx = Context();
            Convert(ctx, Item);
            var again = Convert(ctx, Monster);

            Assert.AreEqual(RoStatus.Skipped, again.Status);
            Assert.IsInstanceOf<SpriteAnimation>(again.Value);
        }

        [Test]
        public void AForcedRun_KeepsGuidsSpriteIdsAndBytes()
        {
            var first = Context();
            var anim = (SpriteAnimation)Convert(first, Monster).Value;
            first.SaveLedgers();
            string animGuid = AssetDatabase.AssetPathToGUID(AnimationPath(Monster));
            string sheetGuid = AssetDatabase.AssetPathToGUID(SheetPath(Monster));
            var ids = anim.Sprites.Select(FileId).ToArray();
            var sheetBytes = File.ReadAllBytes(RoImportTestRoot.FullPath(SheetPath(Monster)));
            var assetBytes = File.ReadAllBytes(RoImportTestRoot.FullPath(AnimationPath(Monster)));

            var forced = Context(force: true);
            var result = Convert(forced, Monster);
            forced.SaveLedgers();

            Assert.AreNotEqual(RoStatus.Failed, result.Status);
            Assert.AreEqual(animGuid, AssetDatabase.AssetPathToGUID(AnimationPath(Monster)));
            Assert.AreEqual(sheetGuid, AssetDatabase.AssetPathToGUID(SheetPath(Monster)));
            CollectionAssert.AreEqual(ids, ((SpriteAnimation)result.Value).Sprites.Select(FileId).ToArray());
            CollectionAssert.AreEqual(sheetBytes, File.ReadAllBytes(RoImportTestRoot.FullPath(SheetPath(Monster))));
            CollectionAssert.AreEqual(assetBytes, File.ReadAllBytes(RoImportTestRoot.FullPath(AnimationPath(Monster))));
        }

        [Test]
        public void AChangedSpr_ConvertsTheActsThatUseItAgain()
        {
            var first = Context();
            Convert(first, Monster);
            first.SaveLedgers();

            var changed = SpriteTestData.Monster();
            changed.spr.Indexed[0].Indices[15] = 77;
            WriteFile(Monster + ".spr", SpriteFileWriter.Spr(changed.spr));

            var ctx = Context();
            var result = Convert(ctx, Monster);

            Assert.AreEqual(RoStatus.Updated, result.Status);
            Assert.AreEqual(SheetPath(Monster), AssetDatabase.GetAssetPath(((SpriteAnimation)result.Value).Sprites[0]), "the new content's sheet");
        }
    }
}
