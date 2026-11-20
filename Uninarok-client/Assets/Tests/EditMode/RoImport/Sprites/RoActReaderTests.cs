using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Uninarok.RoImport.Core.Formats;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Sprites;

namespace Uninarok.RoImport.Tests.Sprites
{
    // TASKS-0007 T1.2 / T1.3 (FR-3, FR-8). Values are GRF's, recorded by T0.1 (plan 4.1).
    [Category(SpriteTestFiles.Category)]
    public class RoActReaderTests
    {
        static RoAct Read(string file)
        {
            bool ok = RoActReader.TryRead(RoPath.Parse(file), SpriteTestFiles.Bytes(file), out var act, out var reason);
            Assert.IsTrue(ok, $"{file}: {reason}");
            Assert.IsNotNull(act);
            return act;
        }

        // file, version minor, actions, action 0 speed, action 0 frames
        [TestCase("sprite/몬스터/poring.act", 5, 72, 4f, 4)]
        [TestCase("sprite/몬스터/elemetal_master_s3.act", 5, 48, -1f, -1)]
        [TestCase("sprite/npc/4_f_agentkafra.act", 5, 8, 4f, 1)]
        [TestCase("sprite/인간족/몸통/남/초보자_남.act", 5, 104, 4f, 3)]
        [TestCase("sprite/인간족/머리통/남/1_남.act", 4, 104, 4f, 3)]
        [TestCase("sprite/인간족/검사/검사_남_검_검광.act", 1, 104, 6f, 3)]
        [TestCase("sprite/로브/천사날개/남/abyss_chaser_남.act", 5, 104, 4f, 3)]
        [TestCase("sprite/아이템/사과.act", 1, 1, 6f, 1)]
        [TestCase("sprite/아이템/sm_bash.act", 3, 1, 4f, 1)]
        [TestCase("sprite/이팩트/emotion.act", 5, 98, 2f, 29)]
        public void ReferenceAct_HasItsVersionAndActions(string file, int minor, int actions, float speed, int frames)
        {
            var act = Read(file);

            Assert.AreEqual((2, minor), (act.VersionMajor, act.VersionMinor));
            Assert.AreEqual(actions, act.Actions.Count);
            if (frames > 0)
            {
                Assert.AreEqual(speed, act.Actions[0].AnimationSpeed);
                Assert.AreEqual(frames, act.Actions[0].Frames.Count);
            }
        }

        [Test]
        public void Poring_KeepsEventsAndEveryLayerField()
        {
            var act = Read("sprite/몬스터/poring.act");

            CollectionAssert.AreEqual(new[] { "atk", "poring_move.wav", "poring_attack.wav", "poring_damage.wav", "poring_die.wav" }, act.Events);
            Assert.AreEqual(-1, act.Actions[0].Frames[0].EventId);
            Assert.AreEqual(1, act.Actions[8].Frames[5].EventId, "walk frame 5 plays poring_move.wav");

            var first = act.Actions[0].Frames[0].Layers[0];
            Assert.AreEqual(new RoActLayer(RoActLayer.Indexed, 0, 0, -12, false, 1f, 1f, 0, new Color32(255, 255, 255, 255)), first);

            var die = act.Actions[32].Frames[5].Layers;
            Assert.AreEqual(10, die.Count);
            Assert.AreEqual((36, 27, -10, 105), (die[1].SpriteIndex, die[1].OffsetX, die[1].OffsetY, die[1].Rotation));
            Assert.AreEqual(0.9914286f, die[2].ScaleX, 1e-6f);
            Assert.AreEqual(222, die[6].Tint.a);
            Assert.AreEqual((4, 97), (die[9].SpriteIndex, (int)die[9].Tint.a));
        }

        [Test]
        public void TrueColourMonster_LayersUseType1()
        {
            var act = Read("sprite/몬스터/elemetal_master_s3.act");

            var l = act.Actions[0].Frames[0].Layers[0];
            Assert.AreEqual((RoActLayer.TrueColor, 0), (l.SpriteType, l.SpriteIndex));
        }

        [Test]
        public void BodyAndHead_HaveOneAnchorPerFrame_AndTheHeadStartsWithAnEmptyLayer()
        {
            var body = Read("sprite/인간족/몸통/남/초보자_남.act");
            var head = Read("sprite/인간족/머리통/남/1_남.act");

            Assert.AreEqual(new Vector2Int(1, -56), body.Actions[0].Frames[0].Anchors.Single());
            Assert.AreEqual(new Vector2Int(1, -56), head.Actions[0].Frames[0].Anchors.Single());
            Assert.IsTrue(head.Actions[0].Frames[0].Layers[0].IsEmpty);
            Assert.AreEqual(0, head.Actions[0].Frames[0].Layers[1].SpriteIndex);
        }

        [Test]
        public void Sword_IdleIsEmpty_StandbyIsDrawn()
        {
            var sword = Read("sprite/인간족/검사/검사_남_검.act");

            Assert.IsTrue(sword.Actions[0].Frames.All(f => f.Layers.All(l => l.IsEmpty)), "idle: hidden");
            Assert.IsTrue(sword.Actions[32].Frames.All(f => f.Layers.Any(l => !l.IsEmpty)), "standby: drawn");
        }

        [Test]
        public void TruncatedEmptyAndForeignFiles_ReturnFalseWithAReason_AndNeverThrow()
        {
            byte[] full = SpriteTestFiles.Bytes("sprite/몬스터/poring.act");
            var cases = new[]
            {
                ("empty", new byte[0]),
                ("null", (byte[])null),
                ("header only", SpriteTestFiles.Slice(full, 4)),
                ("half", SpriteTestFiles.Slice(full, full.Length / 2)),
                ("not an ACT", new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }),
            };

            foreach (var (name, bytes) in cases)
            {
                bool ok = true;
                RoAct act = null;
                string reason = null;
                Assert.DoesNotThrow(() => ok = RoActReader.TryRead(RoPath.Parse("x.act"), bytes, out act, out reason), name);
                Assert.IsFalse(ok, name);
                Assert.IsNull(act, name);
                Assert.IsFalse(string.IsNullOrEmpty(reason), name + ": a reason");
            }
        }
    }
}
