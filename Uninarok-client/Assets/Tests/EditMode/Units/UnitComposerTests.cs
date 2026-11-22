using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Uninarok.Catalog;
using Uninarok.Networking.Data;

namespace Uninarok.Units.Tests
{
    // TASKS-0009 T3.4, T3.5 (FR-3, FR-4; PLAN-0009 4.5; composition-reference.md), on synthetic tables.
    public class UnitComposerTests
    {
        CompositionTables _tables;
        readonly PartPlacement[] _into = new PartPlacement[UnitComposer.MaxParts];

        [SetUp] public void SetUp() => _tables = UnitTestData.Tables();
        [TearDown] public void TearDown() => UnityEngine.Object.DestroyImmediate(_tables);

        static AttachedPart Part(int view, int x = 0, int y = 0) => new AttachedPart { Present = true, ViewId = view, HasAnchor = true, Anchor = new Vector2Int(x, y) };

        static ComposeInput Full(int direction, int animation = 0, int frame = 0) => new ComposeInput
        {
            Player = true, Job = 1, Sex = AccountSex.SEX_MALE, Animation = animation, Direction = direction, Frame = frame,
            Body = true, HasBodyAnchor = true, BodyAnchor = new Vector2Int(1, -57),
            Head = Part(0, 0, -50), HeadBottom = Part(8, 0, -50), HeadMid = Part(40, 0, -50), HeadTop = Part(16, 0, -50),
            Garment = true, GarmentId = 1, Weapon = true, WeaponTrail = true, Shield = true, Shadow = true,
        };

        PartSlot[] Order(in ComposeInput input, CompositionTables tables)
        {
            int n = UnitComposer.Compose(input, tables, _into);
            for (int i = 0; i < n; i++) Assert.That(_into[i].Order, Is.EqualTo(i));
            return _into.Take(n).Select(p => p.Slot).ToArray();
        }

        PartPlacement Of(PartSlot slot, int n) => _into.Take(n).Single(p => p.Slot == slot);

        static int At(PartSlot[] order, PartSlot slot) => Array.IndexOf(order, slot);

        static readonly PartSlot[] Front =
        {
            PartSlot.Shadow, PartSlot.Garment, PartSlot.Body, PartSlot.Head, PartSlot.HeadMid, PartSlot.HeadTop, PartSlot.HeadBottom,
            PartSlot.Weapon, PartSlot.WeaponTrail, PartSlot.Shield,
        };

        static readonly PartSlot[] Back =
        {
            PartSlot.Shadow, PartSlot.Shield, PartSlot.Body, PartSlot.Head, PartSlot.HeadMid, PartSlot.HeadTop, PartSlot.HeadBottom,
            PartSlot.Garment, PartSlot.Weapon, PartSlot.WeaponTrail,
        };

        [Test]
        public void UnitComposer_DefaultOrder_PerDirection()
        {
            foreach (int d in new[] { 0, 1, 6, 7 }) CollectionAssert.AreEqual(Front, Order(Full(d), _tables), $"direction {d}");
            foreach (int d in new[] { 3, 4, 5 }) CollectionAssert.AreEqual(Back, Order(Full(d), _tables), $"direction {d}");
            var left = Order(Full(2), _tables);
            Assert.That(At(left, PartSlot.Shield), Is.LessThan(At(left, PartSlot.Body)), "the shield is behind at 2");
            Assert.That(At(left, PartSlot.Garment), Is.LessThan(At(left, PartSlot.Body)));
            for (int d = 0; d < 8; d++)
            {
                var o = Order(Full(d), _tables);
                Assert.That(At(o, PartSlot.HeadTop), Is.GreaterThan(At(o, PartSlot.Head)), "a top headgear stays above the hair");
                Assert.That(At(o, PartSlot.Weapon), Is.GreaterThan(At(o, PartSlot.Head)), "the sword is in front of the head");
            }
        }

        [Test]
        public void UnitComposer_EmptyTables_StillSane()
        {
            CollectionAssert.AreEqual(Front, Order(Full(0), null));
            CollectionAssert.AreEqual(Back, Order(Full(4), null));
            CollectionAssert.AreEqual(Front, Order(Full(0), _tables));
            var naked = new ComposeInput { Player = true, Body = true, Head = Part(0) };
            CollectionAssert.AreEqual(new[] { PartSlot.Body, PartSlot.Head }, Order(naked, null));
        }

        [Test]
        public void UnitComposer_Units_HaveOnlyShadowAndBody()
        {
            var poring = new ComposeInput { Body = true, Shadow = true, Weapon = true, Head = Part(0) };
            CollectionAssert.AreEqual(new[] { PartSlot.Shadow, PartSlot.Body }, Order(poring, _tables));
            Assert.That(Order(new ComposeInput(), _tables), Is.Empty);
        }

        [Test]
        public void UnitComposer_HeadAttachesToBodyAnchor()
        {
            var input = Full(0);
            input.BodyAnchor = new Vector2Int(3, -60);
            input.Head = Part(0, 1, -48);
            input.HeadTop = Part(16, 2, -40);
            int n = UnitComposer.Compose(input, _tables, _into);
            Assert.That(Of(PartSlot.Head, n).OffsetPx, Is.EqualTo(new Vector2(2, -12)), "body anchor - head anchor");
            Assert.That(Of(PartSlot.HeadTop, n).OffsetPx, Is.EqualTo(new Vector2(1, -20)));
            Assert.That(Of(PartSlot.Body, n).OffsetPx, Is.EqualTo(Vector2.zero));
            Assert.That(Of(PartSlot.Weapon, n).OffsetPx, Is.EqualTo(Vector2.zero));
            input.Head.HasAnchor = false;
            n = UnitComposer.Compose(input, _tables, _into);
            Assert.That(Of(PartSlot.Head, n).OffsetPx, Is.EqualTo(Vector2.zero), "no anchor, no attachment");
        }

        [Test]
        public void UnitComposer_TheSameHeadgearInTwoSlots_IsDrawnOnce()
        {
            var input = Full(0);
            input.HeadMid = input.HeadTop;
            var o = Order(input, _tables);
            Assert.That(o.Contains(PartSlot.HeadMid) && !o.Contains(PartSlot.HeadTop), Is.True);
        }

        // ---- table rules (T3.5) ----

        [Test]
        public void UnitComposer_RobeTopLayerAboveBody()
        {
            _tables.RobeTopLayer = new[] { 2 };
            _tables.Priorities = new[] { new HeadgearPriority { ViewId = 16, Default = 404, FirstDirection = -1 } };
            var input = Full(4);
            input.GarmentId = 2;
            var o = Order(input, _tables);
            Assert.That(At(o, PartSlot.Garment), Is.GreaterThan(At(o, PartSlot.Body)));
            Assert.That(At(o, PartSlot.Garment), Is.GreaterThan(At(o, PartSlot.HeadTop)), "above every headgear");
            Assert.That(At(o, PartSlot.Garment), Is.LessThan(At(o, PartSlot.Weapon)));
            input.GarmentId = 1;
            o = Order(input, _tables);
            Assert.That(At(o, PartSlot.Garment), Is.LessThan(At(o, PartSlot.HeadTop)), "another garment takes priority 400");
            Assert.That(At(o, PartSlot.Garment), Is.GreaterThan(At(o, PartSlot.HeadBottom)));
            input.Direction = 0;
            input.GarmentId = 2;
            o = Order(input, _tables);
            Assert.That(At(o, PartSlot.Garment), Is.LessThan(At(o, PartSlot.Body)), "still behind facing front (D16)");
        }

        [Test]
        public void UnitComposer_LayerSwapOnlyListedFrames()
        {
            _tables.LayerSwapJobs = new[] { (1 << 1) | 1 };
            _tables.LayerSwaps = new[] { new LayerSwap { Key = LayerSwap.KeyOf(1, AccountSex.SEX_MALE, 5 * 8 + 4), Behind = 0b0110 } };
            bool Behind(int frame)
            {
                var o = Order(Full(4, 5, frame), _tables);
                return At(o, PartSlot.Garment) < At(o, PartSlot.Body);
            }
            Assert.That(Behind(0), Is.False);
            Assert.That(Behind(1), Is.True);
            Assert.That(Behind(2), Is.True);
            Assert.That(Behind(3), Is.False);
            var front = Order(Full(0, 5, 0), _tables);
            Assert.That(At(front, PartSlot.Garment), Is.GreaterThan(At(front, PartSlot.Body)), "a job with a table: an action not listed is in front");
        }

        [Test]
        public void UnitComposer_HeadToBody_GarmentUnderTheHead()
        {
            _tables.HeadToBody = new[] { new HeadToBodyRow { Key = HeadToBodyRow.KeyOf(1, AccountSex.SEX_MALE, 1, 4), Frames = 0b1 } };
            var o = Order(Full(4), _tables);
            Assert.That(At(o, PartSlot.Garment), Is.EqualTo(At(o, PartSlot.Body) + 1));
            Assert.That(At(o, PartSlot.Head), Is.EqualTo(At(o, PartSlot.Garment) + 1));
            o = Order(Full(4, 0, 1), _tables);
            Assert.That(At(o, PartSlot.Garment), Is.GreaterThan(At(o, PartSlot.Head)), "only the listed frame");
        }

        [Test]
        public void UnitComposer_HeadgearPriority_PerDirection_IgnoreRiding()
        {
            const int none = CompositionTables.NoPriority;
            _tables.DirectionPriorities = new[] { -100, -100, -100, none, none, none, -100, -100 };
            _tables.Priorities = new[]
            {
                new HeadgearPriority { ViewId = 8, Default = 304, IgnoreRiding = true, FirstDirection = -1 },
                new HeadgearPriority { ViewId = 16, Default = 143, FirstDirection = 0 },
                new HeadgearPriority { ViewId = 40, Default = 302, FirstDirection = -1 },
            };
            var o = Order(Full(0), _tables);
            Assert.That(At(o, PartSlot.HeadTop), Is.LessThan(At(o, PartSlot.Body)), "negative: behind the body");
            o = Order(Full(4), _tables);
            Assert.That(At(o, PartSlot.HeadTop), Is.GreaterThan(At(o, PartSlot.Head)));
            Assert.That(At(o, PartSlot.HeadTop), Is.LessThan(At(o, PartSlot.HeadMid)), "143 below 302");
            Assert.That(At(o, PartSlot.HeadBottom), Is.GreaterThan(At(o, PartSlot.HeadMid)), "304 above 302");

            var riding = Full(4);
            riding.Riding = true;
            o = Order(riding, _tables);
            Assert.That(At(o, PartSlot.HeadBottom), Is.LessThan(At(o, PartSlot.HeadMid)), "riding: the slot default 300 below 302");
        }

        [Test]
        public void UnitComposer_ItemOffset_DoramVariant()
        {
            _tables.ItemOffsets = new[] { new ItemOffset { ViewId = 16, Direction = 0, Race = SpriteRace.Doram, Dx = -3, Dy = -15 } };
            var input = Full(0);
            int n = UnitComposer.Compose(input, _tables, _into);
            var human = Of(PartSlot.HeadTop, n).OffsetPx;
            input.Race = SpriteRace.Doram;
            n = UnitComposer.Compose(input, _tables, _into);
            Assert.That(Of(PartSlot.HeadTop, n).OffsetPx - human, Is.EqualTo(new Vector2(-3, -15)));
            Assert.That(Of(PartSlot.HeadMid, n).OffsetPx, Is.EqualTo(human), "another headgear is not moved");
        }

        [Test]
        public void UnitComposer_RidingEdits_OnlyWhenAskedAndRiding()
        {
            _tables.HeadEdits = new[] { new SprEdit { Job = 1, FirstAction = 4, LastAction = 4, FirstFrame = 0, LastFrame = 2, Dy = 35 } };
            _tables.BodyEdits = new[] { new SprEdit { Job = 1, FirstAction = 0, LastAction = 7, FirstFrame = 0, LastFrame = 2, Dy = 5 } };
            Vector2 Head(bool riding, bool edits, out Vector2 body)
            {
                var input = Full(4);
                input.Riding = riding;
                input.RidingEdits = edits;
                int n = UnitComposer.Compose(input, _tables, _into);
                body = Of(PartSlot.Body, n).OffsetPx;
                return Of(PartSlot.Head, n).OffsetPx;
            }
            var plain = Head(false, true, out var body0);
            Assert.That(body0, Is.EqualTo(Vector2.zero));
            Assert.That(Head(true, false, out _), Is.EqualTo(plain), "off by default (D17)");
            Assert.That(Head(true, true, out var body) - plain, Is.EqualTo(new Vector2(0, 40)), "the head follows the body, plus its own edit");
            Assert.That(body, Is.EqualTo(new Vector2(0, 5)));
        }

        [Test]
        public void UnitComposer_Transparency_TintsTheGarment()
        {
            _tables.Transparency = new[] { new ItemTint { ViewId = 1, Tint = new Color32(255, 255, 255, 150) } };
            int n = UnitComposer.Compose(Full(0), _tables, _into);
            Assert.That(Of(PartSlot.Garment, n).Tint.a, Is.EqualTo(150));
            Assert.That(Of(PartSlot.Body, n).Tint.a, Is.EqualTo(255));
        }

        [Test]
        public void UnitComposer_Compose_AllocatesNothing()
        {
            Run();
            Assert.That(Run(), Is.EqualTo(0));
        }

        long Run()
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++) UnitComposer.Compose(Full(i & 7), _tables, _into);
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }
    }
}
