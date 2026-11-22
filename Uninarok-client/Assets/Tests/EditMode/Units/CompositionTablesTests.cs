using System;
using NUnit.Framework;
using UnityEngine;
using Uninarok.Catalog;
using Uninarok.Networking.Data;

namespace Uninarok.Units.Tests
{
    // TASKS-0009 T1.2 (FR-3, FR-4, FR-11; PLAN-0009 4.1, D5)
    public class CompositionTablesTests
    {
        const int None = CompositionTables.NoPriority;
        CompositionTables _t;

        [SetUp] public void SetUp() => _t = UnitTestData.Tables();

        [TearDown] public void TearDown() => UnityEngine.Object.DestroyImmediate(_t);

        [Test]
        public void CompositionTables_ShadowScaleOf_Default1_Zero_Baby075()
        {
            Assert.That(_t.ShadowScaleOf(45), Is.EqualTo(1f), "an empty table is all default");
            _t.ShadowJobIds = new[] { 45, 1007, 4023 };
            _t.ShadowScales = new[] { 0f, 0.5f, 0.75f };
            Assert.That(_t.ShadowScaleOf(0), Is.EqualTo(1f), "novice is not listed");
            Assert.That(_t.ShadowScaleOf(1002), Is.EqualTo(1f));
            Assert.That(_t.ShadowScaleOf(45), Is.EqualTo(0f));
            Assert.That(_t.ShadowScaleOf(1007), Is.EqualTo(0.5f));
            Assert.That(_t.ShadowScaleOf(4023), Is.EqualTo(0.75f));
        }

        [Test]
        public void PriorityOf_DirectionThenDefaultThenSlot_AndRiding()
        {
            _t.DirectionPriorities = new[] { -100, -100, None, None, None, None, None, -100 };
            _t.Priorities = new[]
            {
                new HeadgearPriority { ViewId = 5, Default = 143, FirstDirection = 0 },
                new HeadgearPriority { ViewId = 9, Default = 403, IgnoreRiding = true, FirstDirection = -1 },
                new HeadgearPriority { ViewId = 12, NoDefault = true, FirstDirection = -1 },
            };
            Assert.That(_t.PriorityOf(5, 0, false, 200), Is.EqualTo(-100));
            Assert.That(_t.PriorityOf(5, 4, false, 200), Is.EqualTo(143));
            Assert.That(_t.PriorityOf(9, 4, false, 200), Is.EqualTo(403));
            Assert.That(_t.PriorityOf(9, 4, true, 200), Is.EqualTo(200), "ignored while riding");
            Assert.That(_t.PriorityOf(12, 0, false, 300), Is.EqualTo(300));
            Assert.That(_t.PriorityOf(7, 0, false, 100), Is.EqualTo(100), "unknown item");
        }

        [Test]
        public void TryGetLayerSwap_ListedFramesOnly_JobInheritance_NoTable()
        {
            _t.LayerJobs = new[] { 4001 };
            _t.LayerJobBases = new[] { 0 };
            _t.LayerSwapJobs = new[] { (0 << 1) | 1 };
            _t.LayerSwaps = new[] { new LayerSwap { Key = LayerSwap.KeyOf(0, AccountSex.SEX_MALE, 8), Behind = 0b101 } };
            Assert.That(_t.TryGetLayerSwap(0, AccountSex.SEX_MALE, 8, 0, out bool behind) && behind, Is.True);
            Assert.That(_t.TryGetLayerSwap(0, AccountSex.SEX_MALE, 8, 1, out behind) && !behind, Is.True, "frame 1 is not listed");
            Assert.That(_t.TryGetLayerSwap(4001, AccountSex.SEX_MALE, 8, 2, out behind) && behind, Is.True, "high novice uses the novice's");
            Assert.That(_t.TryGetLayerSwap(0, AccountSex.SEX_MALE, 9, 0, out behind) && !behind, Is.True, "an action not listed is in front");
            Assert.That(_t.TryGetLayerSwap(0, AccountSex.SEX_FEMALE, 8, 0, out _), Is.False, "no table for the sex");
            Assert.That(_t.TryGetLayerSwap(7, AccountSex.SEX_MALE, 8, 0, out _), Is.False, "no table for the job");
        }

        [Test]
        public void IsGarmentUnderHead_ListedOnly()
        {
            _t.HeadToBody = new[] { new HeadToBodyRow { Key = HeadToBodyRow.KeyOf(4218, AccountSex.SEX_FEMALE, 23, 3), Frames = 1 } };
            Assert.That(_t.IsGarmentUnderHead(4218, AccountSex.SEX_FEMALE, 23, 3, 0), Is.True);
            Assert.That(_t.IsGarmentUnderHead(4218, AccountSex.SEX_FEMALE, 23, 3, 1), Is.False);
            Assert.That(_t.IsGarmentUnderHead(4218, AccountSex.SEX_MALE, 23, 3, 0), Is.False);
            Assert.That(_t.IsGarmentUnderHead(4218, AccountSex.SEX_FEMALE, 22, 3, 0), Is.False);
        }

        [Test]
        public void Edits_RangesAndTheLastCallWins()
        {
            _t.HeadEdits = new[]
            {
                new SprEdit { Job = 0, FirstAction = 4, LastAction = 4, FirstFrame = 0, LastFrame = 2, Dy = 35 },
                new SprEdit { Job = 4047, FirstAction = 99, LastAction = 101, FirstFrame = 1, LastFrame = 1, Dx = -6, Dy = 15 },
                new SprEdit { Job = 4047, FirstAction = 100, LastAction = 101, FirstFrame = 1, LastFrame = 1, Dx = 22, Dy = -1 },
            };
            Assert.That(_t.TryGetHeadEdit(0, 4, 2, out var o) && o == new Vector2Int(0, 35), Is.True);
            Assert.That(_t.TryGetHeadEdit(0, 4, 3, out _), Is.False);
            Assert.That(_t.TryGetHeadEdit(4047, 99, 1, out o) && o == new Vector2Int(-6, 15), Is.True);
            Assert.That(_t.TryGetHeadEdit(4047, 101, 1, out o) && o == new Vector2Int(22, -1), Is.True);
            Assert.That(_t.TryGetBodyEdit(0, 4, 0, out _), Is.False);
        }

        [Test]
        public void ItemOffset_RaceDirectionSex_FirstMatch()
        {
            _t.ItemOffsets = new[]
            {
                new ItemOffset { ViewId = 54, Direction = 1, Race = SpriteRace.Doram, Sex = 2, Dx = -3, Dy = 1 },
                new ItemOffset { ViewId = 1717, Direction = 0, Race = SpriteRace.Doram, Dy = -15 },
            };
            Assert.That(_t.TryGetItemOffset(1717, 0, SpriteRace.Doram, AccountSex.SEX_MALE, out var o) && o == new Vector2Int(0, -15), Is.True);
            Assert.That(_t.TryGetItemOffset(1717, 0, SpriteRace.Human, AccountSex.SEX_MALE, out _), Is.False);
            Assert.That(_t.TryGetItemOffset(1717, 1, SpriteRace.Doram, AccountSex.SEX_MALE, out _), Is.False);
            Assert.That(_t.TryGetItemOffset(54, 1, SpriteRace.Doram, AccountSex.SEX_FEMALE, out o) && o == new Vector2Int(-3, 1), Is.True);
            Assert.That(_t.TryGetItemOffset(54, 1, SpriteRace.Doram, AccountSex.SEX_MALE, out _), Is.False);
        }

        [Test]
        public void Tint_TopLayer_Hidden()
        {
            _t.Transparency = new[] { new ItemTint { ViewId = 36, Tint = new Color32(255, 255, 255, 150) } };
            _t.RobeTopLayer = new[] { 2, 60 };
            _t.HiddenJobIds = new[] { 45, 111, 139 };
            Assert.That(_t.TryGetTint(36, out var c) && c.a == 150, Is.True);
            Assert.That(_t.TryGetTint(1, out c) || c.a != 255, Is.False);
            Assert.That(_t.IsRobeTopLayer(60) && !_t.IsRobeTopLayer(1), Is.True);
            Assert.That(_t.IsHidden(111) && !_t.IsHidden(1002), Is.True);
        }

        [Test]
        public void Lookups_AllocateNothing()
        {
            _t.ShadowJobIds = new[] { 45 };
            _t.ShadowScales = new[] { 0f };
            _t.Priorities = new[] { new HeadgearPriority { ViewId = 5, Default = 143, FirstDirection = -1 } };
            Run();
            Assert.That(Run(), Is.EqualTo(0));
        }

        long Run()
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++)
            {
                _t.ShadowScaleOf(i);
                _t.PriorityOf(i, 0, false, 100);
                _t.TryGetLayerSwap(i, AccountSex.SEX_MALE, 8, 0, out _);
                _t.IsGarmentUnderHead(i, AccountSex.SEX_MALE, 1, 3, 0);
                _t.TryGetHeadEdit(i, 0, 0, out _);
                _t.TryGetItemOffset(i, 0, SpriteRace.Human, AccountSex.SEX_MALE, out _);
                _t.TryGetTint(i, out _);
                _t.IsRobeTopLayer(i);
            }
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }
    }
}
