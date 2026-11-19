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
using Uninarok.RoImport.Editor.Pipeline;
using Uninarok.RoImport.Editor.Sprites;
using Uninarok.RoImport.Editor.Ui;
using Uninarok.Sprites;
using Object = UnityEngine.Object;

namespace Uninarok.RoImport.Tests.Sprites
{
    // TASKS-0007 T6.1 (FR-1 to FR-9, FR-11; spec §5): the reference set of tasks.md converts from the real data with no
    // failure, one sheet per distinct SPR content, repeated actions stored once. Writes under Assets/__RoImportTests.
    [Category(SpriteTestFiles.Category)]
    public class RoSpriteReferenceTests
    {
        // ACT and the SPR it is converted against (same name, except the garment class ACT on its folder's root sheet).
        readonly (string act, string spr)[] ReferenceSet =
        {
            ("sprite/몬스터/poring.act", null),
            ("sprite/몬스터/elemetal_master_s3.act", null),
            ("sprite/npc/4_f_agentkafra.act", null),
            ("sprite/인간족/몸통/남/초보자_남.act", null),
            ("sprite/인간족/머리통/남/1_남.act", null),
            ("sprite/악세사리/남/남_고글.act", null),
            ("sprite/로브/천사날개/천사날개.act", null),
            ("sprite/로브/천사날개/남/abyss_chaser_남.act", "sprite/로브/천사날개/천사날개.spr"),
            ("sprite/인간족/검사/검사_남_검.act", null),
            ("sprite/인간족/검사/검사_남_검_검광.act", null),
            ("sprite/방패/검사/검사_남_2189_방패.act", null),
            ("sprite/아이템/사과.act", null),
            ("sprite/아이템/sm_bash.act", null),
            ("sprite/이팩트/emotion.act", null),
        };

        RoOutputRoots _roots;
        string _ledger;
        RoImportContext _ctx;

        [OneTimeSetUp]
        public void Convert()
        {
            string root = RoSourceLocation.Resolve();
            if (!Directory.Exists(root)) Assert.Ignore($"source data folder not found: {root}");
            _roots = RoImportTestRoot.Create();
            _ledger = Path.Combine(Path.GetTempPath(), "RoImportTests", Guid.NewGuid().ToString("N"));
            _ctx = new RoImportContext(new RoFolderSource(root), _roots, _ledger) { SpritePalettes = true };
        }

        [OneTimeTearDown]
        public void Delete()
        {
            RoImportTestRoot.Delete();
            if (_ledger != null && Directory.Exists(_ledger)) Directory.Delete(_ledger, recursive: true);
        }

        RoResult<Object> Sprite(string act, string spr = null) =>
            _ctx.Sprite(RoPath.Parse(act), RoPath.Parse(spr ?? Path.ChangeExtension(act, ".spr").Replace('\\', '/')));

        [Test]
        public void TheReferenceSet_ConvertsWithoutFailure_OneSheetPerDistinctContent()
        {
            var hashes = new System.Collections.Generic.HashSet<string>();
            foreach (var (act, spr) in ReferenceSet)
            {
                var result = Sprite(act, spr);
                Assert.AreNotEqual(RoStatus.Failed, result.Status, $"{act}: {result.Failure}");
                Assert.IsNotNull(result.Value, act);
                Assert.IsTrue(_ctx.Source.TryGetFile(RoPath.Parse(spr ?? Path.ChangeExtension(act, ".spr").Replace('\\', '/')), out var file));
                hashes.Add(file.ContentHash());
            }
            Assert.AreEqual(0, _ctx.Report.Issues.Count(i => i.Code != RoIssueCode.OutputCollision), string.Join("\n", _ctx.Report.Issues));

            _ctx.SaveLedgers();
            Assert.AreEqual(hashes.Count, RoSpriteSheetIndex.Load(_ledger).Count, "one sheet per distinct SPR content (FR-2)");
            int sheets = Directory.GetFiles(RoImportTestRoot.FullPath(_roots.Art + "/" + RoSpriteOutputPaths.SheetsFolder), "*.png", SearchOption.AllDirectories)
                .Count(f => !f.EndsWith(RoSpriteOutputPaths.IndexSuffix + ".png", StringComparison.Ordinal));
            Assert.AreEqual(hashes.Count, sheets);
        }

        [Test]
        public void EveryAnimation_StoresAtMostItsFileActions_AndThePoringHalf()
        {
            foreach (var (act, spr) in ReferenceSet)
                if (Sprite(act, spr).Value is SpriteAnimation a)
                {
                    Assert.LessOrEqual(a.Actions.Length, a.FileActionCount, act);
                    Assert.IsTrue(a.Sprites.All(s => s != null), act + ": every frame has its sprite");
                }

            var poring = (SpriteAnimation)Sprite("sprite/몬스터/poring.act").Value;
            Assert.LessOrEqual(poring.Actions.Length, poring.FileActionCount / 2, "directions repeat in pairs (FR-4)");
            Assert.AreEqual(10, poring.MaxLayers);
        }

        [Test]
        public void TheSword_IsEmptyWhileWalking_AndDrawnWhileAttacking()
        {
            var sword = (SpriteAnimation)Sprite("sprite/인간족/검사/검사_남_검.act").Value;

            for (int d = 0; d < 8; d++)
            {
                Assert.IsTrue(sword.TryGetAction(1, d, SpriteMissingAction.DrawNothing, out int walk), "walk exists (FR-5)");
                Assert.IsTrue(AllEmpty(sword, walk), $"walk direction {d} draws nothing");
                Assert.IsTrue(sword.TryGetAction(10, d, SpriteMissingAction.DrawNothing, out int attack));
                Assert.IsFalse(AllEmpty(sword, attack), $"attack 2 direction {d} draws the sword");
            }
        }

        static bool AllEmpty(SpriteAnimation a, int unique)
        {
            var action = a.Actions[unique];
            for (int f = 0; f < action.FrameCount; f++)
            {
                var frame = a.Frames[action.FirstFrame + f];
                for (int l = 0; l < frame.LayerCount; l++)
                    if (a.Layers[frame.FirstLayer + l].Slot >= 0) return false;
            }
            return true;
        }

        [TestCase("sprite/아이템/사과.act")]
        [TestCase("sprite/아이템/sm_bash.act")]
        public void TheGroundItemAndTheSkillIcon_AreSprites(string act)
        {
            var result = Sprite(act);

            Assert.IsInstanceOf<Sprite>(result.Value, "static (FR-11)");
            Assert.IsFalse(File.Exists(RoImportTestRoot.FullPath(RoSpriteOutputPaths.Animation(_roots, RoPath.Parse(act)))));
        }

        [Test]
        public void TheBody_HasItsIndexTexture_AndOneAnchorPerFrame()
        {
            var body = (SpriteAnimation)Sprite("sprite/인간족/몸통/남/초보자_남.act").Value;

            Assert.AreEqual(1, body.Sprites[0].GetSecondaryTextureCount(), "palettes requested (FR-7)");
            Assert.AreEqual(body.Frames.Length, body.Anchors.Length);
        }
    }
}
