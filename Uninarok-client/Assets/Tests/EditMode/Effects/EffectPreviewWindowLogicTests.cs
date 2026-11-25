using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Uninarok.Effects.Editor;

namespace Uninarok.Effects.Tests
{
    /// <summary>FR-11: the arithmetic and list logic of the effect preview window (no window, no scene).</summary>
    public sealed class EffectPreviewWindowLogicTests
    {
        static readonly string[] Keys =
        {
            "model/trap.rsm",
            "sprite/fx/a.act",
            "sprite/fx/b.act",
            "texture/effect/_authored/warp_portal/ring_inner.cylinder",
            "texture/effect/_authored/warp_portal/ring_outer.cylinder",
            "texture/effect/_authored/warp_portal/sparkle.quad",
            "texture/effect/firewall.str",
            "texture/effect/Heal.str",
        };

        // ---- filter ----

        [Test]
        public void Filter_EmptyKeepsAll_FR11()
        {
            Assert.That(EffectBrowse.Filter(Keys, "").Count, Is.EqualTo(Keys.Length));
            Assert.That(EffectBrowse.Filter(Keys, "   ").Count, Is.EqualTo(Keys.Length));
            Assert.That(EffectBrowse.Filter(Keys, null).Count, Is.EqualTo(Keys.Length));
        }

        [Test]
        public void Filter_SubstringIgnoresCaseAndSlashForm_FR11()
        {
            Assert.That(EffectBrowse.Filter(Keys, "warp_portal").Select(i => Keys[i]),
                Is.EqualTo(new[] { Keys[3], Keys[4], Keys[5] }));
            Assert.That(EffectBrowse.Filter(Keys, "HEAL").Select(i => Keys[i]), Is.EqualTo(new[] { Keys[7] }));
            Assert.That(EffectBrowse.Filter(Keys, "effect\\fire").Select(i => Keys[i]), Is.EqualTo(new[] { Keys[6] }));
        }

        [Test]
        public void Filter_AllTokensMustMatch_FR11()
        {
            Assert.That(EffectBrowse.Filter(Keys, "warp ring").Select(i => Keys[i]), Is.EqualTo(new[] { Keys[3], Keys[4] }));
            Assert.That(EffectBrowse.Filter(Keys, "warp nothing"), Is.Empty);
        }

        // ---- flat and tree ----

        [Test]
        public void Flat_OneRowPerMatchWithWholeKey_FR11()
        {
            var rows = EffectBrowse.FlatRows(Keys, EffectBrowse.Filter(Keys, "sprite"));
            Assert.That(rows.Select(r => r.Label), Is.EqualTo(new[] { Keys[1], Keys[2] }));
            Assert.That(rows.All(r => !r.IsFolder && r.Depth == 0), Is.True);
        }

        [Test]
        public void Tree_FoldersBeforeTheirKeys_FR11()
        {
            var rows = EffectBrowse.TreeRows(Keys, EffectBrowse.Filter(Keys, ""), null);
            string[] expected =
            {
                "model", "trap.rsm",
                "sprite", "fx", "a.act", "b.act",
                "texture", "effect", "_authored", "warp_portal", "ring_inner.cylinder", "ring_outer.cylinder", "sparkle.quad",
                "firewall.str", "Heal.str",
            };
            // ordinal order: "texture/effect/Heal.str" sorts before "texture/effect/_authored/..." and "firewall.str"
            Assert.That(rows.Select(r => r.Label).OrderBy(x => x), Is.EqualTo(expected.OrderBy(x => x)));
            Assert.That(rows.Count(r => r.IsFolder), Is.EqualTo(7), "model, sprite, fx, texture, effect, _authored, warp_portal once each");
            var warp = rows.First(r => r.IsFolder && r.Label == "warp_portal");
            Assert.That(warp.Depth, Is.EqualTo(3));
            Assert.That(warp.Folder, Is.EqualTo("texture/effect/_authored/warp_portal"));
            var sparkle = rows.First(r => r.Label == "sparkle.quad");
            Assert.That(sparkle.KeyIndex, Is.EqualTo(5));
            Assert.That(sparkle.Depth, Is.EqualTo(4));
            Assert.That(rows.IndexOf(warp), Is.LessThan(rows.IndexOf(sparkle)));
        }

        [Test]
        public void Tree_CollapsedFolderHidesWhatIsBelow_FR11()
        {
            var collapsed = new HashSet<string> { "texture/effect" };
            var rows = EffectBrowse.TreeRows(Keys, EffectBrowse.Filter(Keys, ""), collapsed);
            Assert.That(rows.Any(r => r.IsFolder && r.Label == "effect"), Is.True, "the collapsed folder keeps its own row");
            Assert.That(rows.Any(r => r.Label == "sparkle.quad" || r.Label == "firewall.str" || r.Label == "warp_portal"), Is.False);
            Assert.That(rows.Any(r => r.Label == "a.act"), Is.True, "other folders stay open");

            var all = EffectBrowse.TreeRows(Keys, EffectBrowse.Filter(Keys, ""), null);
            Assert.That(all.Any(r => r.Label == "sparkle.quad"), Is.True, "null collapsed set expands everything");
        }

        [Test]
        public void Tree_FilterKeepsOnlyTheMatchingBranches_FR11()
        {
            var rows = EffectBrowse.TreeRows(Keys, EffectBrowse.Filter(Keys, "warp_portal"), null);
            Assert.That(rows.Count(r => !r.IsFolder), Is.EqualTo(3));
            Assert.That(rows.Where(r => r.IsFolder).Select(r => r.Label), Is.EqualTo(new[] { "texture", "effect", "_authored", "warp_portal" }));
        }

        // ---- virtual range ----

        [Test]
        public void VisibleRange_CoversTheViewPlusOverscan_FR11()
        {
            EffectPreviewMath.VisibleRange(3200, 18f, 0f, 180f, 2, out int first, out int end);
            Assert.That(first, Is.EqualTo(0));
            Assert.That(end, Is.EqualTo(12), "10 rows in view + 2 overscan");

            EffectPreviewMath.VisibleRange(3200, 18f, 1800f, 180f, 2, out first, out end);
            Assert.That(first, Is.EqualTo(98));
            Assert.That(end, Is.EqualTo(112));
            Assert.That(end - first, Is.LessThan(20), "only a screenful of 3200 rows is drawn");
        }

        [Test]
        public void VisibleRange_ClampsAtBothEndsAndHandlesEmpty_FR11()
        {
            EffectPreviewMath.VisibleRange(50, 18f, 100000f, 180f, 2, out int first, out int end);
            Assert.That(first, Is.EqualTo(50));
            Assert.That(end, Is.EqualTo(50));

            EffectPreviewMath.VisibleRange(5, 18f, 0f, 1000f, 2, out first, out end);
            Assert.That((first, end), Is.EqualTo((0, 5)));

            EffectPreviewMath.VisibleRange(0, 18f, 0f, 180f, 2, out first, out end);
            Assert.That((first, end), Is.EqualTo((0, 0)));
            EffectPreviewMath.VisibleRange(10, 0f, 0f, 180f, 2, out first, out end);
            Assert.That((first, end), Is.EqualTo((0, 0)));
            EffectPreviewMath.VisibleRange(10, 18f, -50f, 36f, 0, out first, out end);
            Assert.That((first, end), Is.EqualTo((0, 2)), "negative scroll counts as 0");
        }

        // ---- frame step ----

        [Test]
        public void StepFrame_ClampsAtFirstAndLastFrame_FR11()
        {
            Assert.That(EffectPreviewMath.StepFrame(0f, -1, 59), Is.EqualTo(0));
            Assert.That(EffectPreviewMath.StepFrame(59f, 1, 59), Is.EqualTo(59));
            Assert.That(EffectPreviewMath.StepFrame(10f, 1, 59), Is.EqualTo(11));
            Assert.That(EffectPreviewMath.StepFrame(10f, -1, 59), Is.EqualTo(9));
            Assert.That(EffectPreviewMath.StepFrame(3f, -10, 59), Is.EqualTo(0));
            Assert.That(EffectPreviewMath.StepFrame(58f, 10, 59), Is.EqualTo(59));
            Assert.That(EffectPreviewMath.StepFrame(5f, 1, 0), Is.EqualTo(0), "a one-frame effect stays at 0");
        }

        [Test]
        public void StepFrame_FromAFractionMovesTheShownFrameByOne_FR11()
        {
            foreach (float f in new[] { 3f, 3.4f, 3.999f, 2.9999f })
            {
                int shown = EffectPreviewMath.ShownFrame(f);
                Assert.That(EffectPreviewMath.StepFrame(f, 1, 59) - shown, Is.EqualTo(1), "+1 from " + f);
                Assert.That(shown - EffectPreviewMath.StepFrame(f, -1, 59), Is.LessThanOrEqualTo(1), "-1 from " + f);
            }
            Assert.That(EffectPreviewMath.StepFrame(3.4f, -1, 59), Is.EqualTo(3));
            Assert.That(EffectPreviewMath.StepFrame(float.NaN, 1, 59), Is.EqualTo(1));
        }

        [Test]
        public void LastFrame_OneShotReachesTheEndLoopWraps_FR11()
        {
            Assert.That(EffectPreviewMath.LastFrame(1f, 60f, false), Is.EqualTo(60));
            Assert.That(EffectPreviewMath.LastFrame(1f, 60f, true), Is.EqualTo(59));
            Assert.That(EffectPreviewMath.LastFrame(0.5083f, 60f, true), Is.EqualTo(30), "30.5 frames: 0..30");
            Assert.That(EffectPreviewMath.LastFrame(0f, 60f, true), Is.EqualTo(0));
            Assert.That(EffectPreviewMath.LastFrame(1f, 0f, false), Is.EqualTo(0));
        }

        // ---- speed ----

        [Test]
        public void ClampSpeed_StaysBetweenPointOneAndFour_FR11()
        {
            Assert.That(EffectPreviewMath.ClampSpeed(0f), Is.EqualTo(0.1f));
            Assert.That(EffectPreviewMath.ClampSpeed(-3f), Is.EqualTo(0.1f));
            Assert.That(EffectPreviewMath.ClampSpeed(0.1f), Is.EqualTo(0.1f));
            Assert.That(EffectPreviewMath.ClampSpeed(2.5f), Is.EqualTo(2.5f));
            Assert.That(EffectPreviewMath.ClampSpeed(4f), Is.EqualTo(4f));
            Assert.That(EffectPreviewMath.ClampSpeed(9f), Is.EqualTo(4f));
            Assert.That(EffectPreviewMath.ClampSpeed(float.NaN), Is.EqualTo(1f));
            Assert.That(EffectPreviewMath.ClampSpeed(float.PositiveInfinity), Is.EqualTo(1f));
        }
    }
}
