using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Uninarok.Networking.Data;

namespace Uninarok.World.Tests
{
    // SPEC-0010 FR-3 to FR-8, FR-16, FR-17, FR-19 (PLAN-0010 4.5).
    public class WorldModelTests
    {
        sealed class Grid : IWalkGrid
        {
            public Func<int, int, bool> Wall = (x, y) => false;
            public int Width => 300;
            public int Height => 300;
            public bool IsPassable(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height && !Wall(x, y);
        }

        const uint Poring = 0x068EB5EC, Player = 2000002;
        static readonly EntityLook Look = new EntityLook { Job = 1002 };

        Grid _grid;
        WorldSettings _settings;
        WorldModel _world;
        List<VanishInfo> _vanished;

        [SetUp]
        public void SetUp()
        {
            _grid = new Grid();
            _settings = ScriptableObject.CreateInstance<WorldSettings>();
            _world = new WorldModel(_grid, _settings, 64);
            _vanished = new List<VanishInfo>();
            _world.Vanished += v => _vanished.Add(v);
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(_settings);

        int Monster(int x = 50, int y = 50, short speed = 200, uint id = Poring) =>
            _world.Upsert(id, EntityKind.Monster, Look, speed, x, y, UninarokDirection.South, HeadDirection.Straight, EntityState.Standing);

        int Human(int x = 50, int y = 50, uint id = Player, EntityState state = EntityState.Standing) =>
            _world.Upsert(id, EntityKind.Player, new EntityLook { Job = 1, HairStyle = 2 }, 150, x, y, UninarokDirection.South, HeadDirection.Left, state);

        Entity Get(uint id)
        {
            Assert.IsTrue(_world.TryGetSlot(id, out int slot), $"no entity {id}");
            return _world[slot];
        }

        bool Has(uint id) => _world.TryGetSlot(id, out _);

        // ---- appearing (FR-4) ----

        [Test]
        public void WorldModel_UpsertTwice_OneEntity()
        {
            int first = Monster();
            int generation = _world[first].Generation;
            var other = Look;
            other.Job = 1063;
            int second = _world.Upsert(Poring, EntityKind.Monster, other, 400, 60, 61, UninarokDirection.East, HeadDirection.Straight, EntityState.Standing);
            Assert.AreEqual(first, second);
            Assert.AreEqual(1, _world.Count);
            var e = Get(Poring);
            Assert.AreEqual((1063, 400, 60, 61), ((int)e.Look.Job, (int)e.Speed, e.CellX, e.CellY));
            Assert.AreEqual((60f, 61f, UninarokDirection.East), (e.PosX, e.PosY, e.Facing));
            Assert.AreEqual(generation, e.Generation, "the same entity");
        }

        [Test]
        public void WorldModel_Upsert_SetsEverythingOfTheEntry()
        {
            int slot = Human(12, 34, state: EntityState.Sitting);
            var e = _world[slot];
            Assert.AreEqual((Player, EntityKind.Player, EntityState.Sitting), (e.Id, e.Kind, e.State));
            Assert.AreEqual((12, 34, 12f, 34f), (e.CellX, e.CellY, e.PosX, e.PosY));
            Assert.AreEqual((UninarokDirection.South, HeadDirection.Left), (e.Facing, e.Head));
            Assert.IsFalse(e.Walking);
            Assert.IsTrue(_world.IsUsed(slot));
            Assert.Less(slot, _world.SlotLimit);
        }

        [Test]
        public void WorldModel_AReusedSlot_HasAnotherGeneration()
        {
            int slot = Monster();
            int generation = _world[slot].Generation;
            _world.Clear();
            Assert.AreEqual(0, _world.Count);
            Assert.AreEqual(slot, Monster(id: 99));
            Assert.AreNotEqual(generation, _world[slot].Generation);
        }

        [Test]
        public void WorldModel_Full_WarnsOnce_AndIgnoresTheNewEntity()
        {
            var small = new WorldModel(_grid, _settings, 2);
            small.Upsert(1, EntityKind.Npc, Look, 200, 1, 1, 0, 0, 0);
            small.Upsert(2, EntityKind.Npc, Look, 200, 1, 1, 0, 0, 0);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("full"));
            Assert.AreEqual(-1, small.Upsert(3, EntityKind.Npc, Look, 200, 1, 1, 0, 0, 0));
            Assert.AreEqual(-1, small.Upsert(4, EntityKind.Npc, Look, 200, 1, 1, 0, 0, 0));
            Assert.AreEqual(2, small.Count);
            Assert.AreEqual(0, small.Upsert(1, EntityKind.Npc, Look, 200, 2, 2, 0, 0, 0), "a known one is still updated");
        }

        // ---- walking (FR-6) ----

        [Test]
        public void WorldModel_Walk_ArrivesAtServerTime()
        {
            Monster();
            int animation = Get(Poring).AnimationVersion;
            _world.Walk(Poring, 50, 50, 55, 50, 10_000, 10_000);
            Assert.IsTrue(Get(Poring).Walking);
            Assert.AreNotEqual(animation, animation = Get(Poring).AnimationVersion, "walk starts playing");
            _world.Advance(10_500);
            var e = Get(Poring);
            Assert.AreEqual((52.5f, 50f), (e.PosX, e.PosY));
            Assert.AreEqual(UninarokDirection.East, e.Facing);
            _world.Advance(10_999);
            Assert.IsTrue(Get(Poring).Walking);
            _world.Advance(11_000);
            e = Get(Poring);
            Assert.IsFalse(e.Walking, "5 cells of 200 ms are over 1 s after the start");
            Assert.AreEqual((55, 50, 55f, 50f), (e.CellX, e.CellY, e.PosX, e.PosY));
            Assert.AreNotEqual(animation, e.AnimationVersion, "idle on arrival");
        }

        [Test]
        public void WorldModel_Walk_OneDiagonalTakes1080ms()
        {
            Monster();
            _world.Walk(Poring, 50, 50, 55, 51, 10_000, 10_000);
            Assert.AreEqual(5, _world.PathOf(0).Length);
            _world.Advance(11_079);
            Assert.IsTrue(Get(Poring).Walking);
            _world.Advance(11_080);
            Assert.IsFalse(Get(Poring).Walking);
            Assert.AreEqual((55, 51), (Get(Poring).CellX, Get(Poring).CellY));
        }

        [Test]
        public void WorldModel_Walk_TurnsToEachStep_AroundAWall()
        {
            _grid.Wall = (x, y) => x == 52 && y < 52;
            Human(50, 50);
            _world.Walk(Player, 50, 50, 54, 50, 0, 0);
            var facings = new List<UninarokDirection>();
            for (uint t = 0; t <= 3000; t += 10)
            {
                _world.Advance(t);
                var e = Get(Player);
                if (facings.Count == 0 || facings[facings.Count - 1] != e.Facing) facings.Add(e.Facing);
                Assert.IsTrue(_grid.IsPassable(e.CellX, e.CellY), $"{e.CellX},{e.CellY}");
            }
            Assert.IsFalse(Get(Player).Walking);
            Assert.AreEqual((54, 50), (Get(Player).CellX, Get(Player).CellY));
            Assert.Greater(facings.Count, 2);
        }

        [Test]
        public void WorldModel_ServerCellChangesAtStepEnd()
        {
            Monster();
            _world.Walk(Poring, 50, 50, 52, 50, 0, 0);
            _world.Advance(199);
            Assert.AreEqual((50, 50), (Get(Poring).CellX, Get(Poring).CellY), "PLAN-0010 D4");
            Assert.AreEqual(50.995f, Get(Poring).PosX, 1e-3f);
            _world.Advance(200);
            Assert.AreEqual(51, Get(Poring).CellX);
            _world.Advance(399);
            Assert.AreEqual(51, Get(Poring).CellX);
            _world.Advance(400);
            Assert.AreEqual(52, Get(Poring).CellX);
        }

        [Test]
        public void WorldModel_MidWalkEntryAppearsOnPath()
        {
            _world.UpsertWalking(Poring, EntityKind.Monster, Look, 200, 50, 50, 55, 50, HeadDirection.Straight, 10_000, 10_700);
            var e = Get(Poring);
            Assert.IsTrue(e.Walking);
            Assert.AreEqual((53, 53.5f), (e.CellX, e.PosX));
            Assert.AreEqual(UninarokDirection.East, e.Facing);
            _world.UpsertWalking(77, EntityKind.Monster, Look, 200, 50, 50, 55, 50, HeadDirection.Straight, 10_000, 99_000);
            Assert.IsFalse(Get(77).Walking, "its walk is long over");
            Assert.AreEqual(55f, Get(77).PosX);
        }

        [Test]
        public void WorldModel_Walk_BeforeItsStart_WaitsOnTheSourceCell()
        {
            Monster();
            _world.Walk(Poring, 50, 50, 52, 50, 10_000, 9_950);   // the clock is a little behind
            _world.Advance(9_960);
            Assert.AreEqual(50f, Get(Poring).PosX);
            _world.Advance(10_100);
            Assert.AreEqual(50.5f, Get(Poring).PosX);
        }

        [Test]
        public void WorldModel_NewOrderWhileWalking_NoSnapWithinOneCell()
        {
            Monster();
            _world.Walk(Poring, 50, 50, 55, 50, 0, 0);
            _world.Advance(500);
            Assert.AreEqual(52.5f, Get(Poring).PosX);
            int animation = Get(Poring).AnimationVersion;
            _world.Walk(Poring, 52, 50, 52, 55, 500, 500);      // from the server's cell, half a cell behind the shown place
            Assert.AreEqual((52.5f, 50f), (Get(Poring).PosX, Get(Poring).PosY), "no snap");
            Assert.AreEqual(animation, Get(Poring).AnimationVersion, "the walk animation goes on");
            _world.Advance(600);
            Assert.AreEqual((52.25f, 50.5f), (Get(Poring).PosX, Get(Poring).PosY), "the first step starts from where it was shown");
            _world.Advance(700);
            Assert.AreEqual((52f, 51f), (Get(Poring).PosX, Get(Poring).PosY));
            Assert.AreEqual(51, Get(Poring).CellY);

            _world.Walk(Poring, 70, 70, 72, 70, 700, 700);      // farther than a cell: a real desync
            Assert.AreEqual((70f, 70f), (Get(Poring).PosX, Get(Poring).PosY));
        }

        [Test]
        public void WorldModel_MoveEntryOfAKnownUnit_GoesOnWithTheNewSpeed_AndNoSnap()
        {
            Monster();
            _world.Walk(Poring, 50, 50, 55, 50, 0, 0);
            _world.Advance(300);
            _world.UpsertWalking(Poring, EntityKind.Monster, Look, 100, 51, 50, 55, 50, HeadDirection.Straight, 300, 300);
            Assert.AreEqual(1, _world.Count);
            Assert.AreEqual(51.5f, Get(Poring).PosX);
            _world.Advance(700);
            Assert.IsFalse(Get(Poring).Walking, "four cells of 100 ms");
            Assert.AreEqual(55f, Get(Poring).PosX);
        }

        [Test]
        public void WorldModel_Walk_WithoutAPath_IsPlacedOnTheDestination()
        {
            _grid.Wall = (x, y) => x == 52;
            Human(50, 50);
            _world.Walk(Player, 50, 50, 54, 50, 0, 0);
            var e = Get(Player);
            Assert.IsFalse(e.Walking);
            Assert.AreEqual((54, 50, 54f), (e.CellX, e.CellY, e.PosX), "the server got there, so the client must too");
        }

        [Test]
        public void WorldModel_Walk_OfAPlayer_TurnsTheHeadStraight_AndStandsItUp()
        {
            Human(state: EntityState.Sitting);
            _world.Walk(Player, 50, 50, 52, 50, 0, 0);
            Assert.AreEqual((HeadDirection.Straight, EntityState.Standing), (Get(Player).Head, Get(Player).State));
        }

        [Test]
        public void WorldModel_Walk_AcrossTheTickWrap()
        {
            Monster();
            uint start = uint.MaxValue - 299;                    // 300 ms before the wrap
            _world.Walk(Poring, 50, 50, 55, 50, start, start);
            _world.Advance(200);                                 // 500 ms later
            Assert.AreEqual(52.5f, Get(Poring).PosX);
            _world.Advance(700);
            Assert.IsFalse(Get(Poring).Walking);
        }

        [Test]
        public void WorldModel_UnknownId_WalkIsIgnored()
        {
            _world.Walk(123, 50, 50, 52, 50, 0, 0);
            Assert.AreEqual(0, _world.Count);
        }

        // ---- stop and slide (FR-7) ----

        [Test]
        public void WorldModel_Stop_SnapsAndIdles()
        {
            Monster();
            _world.Walk(Poring, 50, 50, 55, 50, 0, 0);
            _world.Advance(500);
            int animation = Get(Poring).AnimationVersion;
            _world.Stop(Poring, 52, 50);
            var e = Get(Poring);
            Assert.IsFalse(e.Walking);
            Assert.AreEqual((52, 50, 52f, 50f), (e.CellX, e.CellY, e.PosX, e.PosY));
            Assert.AreNotEqual(animation, e.AnimationVersion);
            _world.Advance(2000);
            Assert.AreEqual(52f, Get(Poring).PosX);
            _world.Stop(999, 1, 1);
        }

        [Test]
        public void WorldModel_Slide_KeepsFacing()
        {
            Monster();
            int version = Get(Poring).Version, animation = Get(Poring).AnimationVersion;
            _world.Slide(Poring, 44, 47);
            var e = Get(Poring);
            Assert.AreEqual((44, 47, 44f, 47f, UninarokDirection.South), (e.CellX, e.CellY, e.PosX, e.PosY, e.Facing));
            Assert.AreNotEqual(version, e.Version);
            Assert.AreEqual(animation, e.AnimationVersion, "it did not walk");

            _world.Walk(Poring, 44, 47, 49, 47, 0, 0);
            _world.Advance(300);
            _world.Slide(Poring, 40, 40);
            e = Get(Poring);
            Assert.IsFalse(e.Walking);
            Assert.AreEqual((40f, 40f, UninarokDirection.East), (e.PosX, e.PosY, e.Facing));
        }

        // ---- vanish (FR-5) ----

        [Test]
        public void WorldModel_Vanish_RemovesAtOnce([Values(VanishReason.OutOfSight, VanishReason.LoggedOut, VanishReason.Teleported)] VanishReason reason,
                                                    [Values(EntityKind.Monster, EntityKind.Player)] EntityKind kind)
        {
            uint id = kind == EntityKind.Monster ? Poring : Player;
            if (kind == EntityKind.Monster) Monster(); else Human();
            _world.Vanish(id, reason, 1000);
            Assert.IsFalse(Has(id));
            Assert.AreEqual(1, _vanished.Count);
            Assert.AreEqual((id, kind, reason, 50, 50), (_vanished[0].Id, _vanished[0].Kind, _vanished[0].Reason, _vanished[0].X, _vanished[0].Y));
        }

        [Test]
        public void WorldModel_Vanish_Died_Monster_LiesDead_ThenIsRemoved()
        {
            Monster();
            _world.Walk(Poring, 50, 50, 55, 50, 0, 0);
            _world.Advance(300);
            int animation = Get(Poring).AnimationVersion;
            _world.Vanish(Poring, VanishReason.Died, 300);
            var e = Get(Poring);
            Assert.AreEqual((EntityState.Dead, false), (e.State, e.Walking));
            Assert.AreNotEqual(animation, e.AnimationVersion);
            Assert.AreEqual((VanishReason.Died, 51, 50), (_vanished[0].Reason, _vanished[0].X, _vanished[0].Y));
            _world.Advance(300 + 4_999);
            Assert.IsTrue(Has(Poring));
            _world.Advance(300 + 5_000);
            Assert.IsFalse(Has(Poring));
            Assert.AreEqual(1, _vanished.Count, "the removal is not another disappearance");
        }

        [Test]
        public void WorldModel_Vanish_Died_Player_StaysDead()
        {
            Human();
            _world.Vanish(Player, VanishReason.Died, 0);
            _world.Advance(1_000_000);
            Assert.AreEqual(EntityState.Dead, Get(Player).State);
            Assert.AreEqual(1, _vanished.Count);
            _world.SetState(Player, EntityState.Standing);
            Assert.AreEqual(EntityState.Standing, Get(Player).State);
        }

        [Test]
        public void WorldModel_Vanish_FeignedDeath_KeepsTheUnit([Values(EntityKind.Monster, EntityKind.Player)] EntityKind kind)
        {
            uint id = kind == EntityKind.Monster ? Poring : Player;
            if (kind == EntityKind.Monster) Monster(); else Human();
            _world.Vanish(id, VanishReason.FeignedDeath, 0);
            _world.Advance(1_000_000);
            Assert.AreEqual(EntityState.Dead, Get(id).State);
            Assert.AreEqual(VanishReason.FeignedDeath, _vanished[0].Reason);
        }

        [Test]
        public void WorldModel_Vanish_AMonsterThatRespawns_IsAlive_AndNotRemoved()
        {
            Monster();
            _world.Vanish(Poring, VanishReason.Died, 0);
            Monster(70, 70);                                     // Hercules keeps the id of a respawning monster
            _world.Advance(100_000);
            Assert.AreEqual((EntityState.Standing, 70), (Get(Poring).State, Get(Poring).CellX));
        }

        [Test]
        public void WorldModel_Vanish_UnknownId_Ignored()
        {
            _world.Vanish(4711, VanishReason.Died, 0);
            Assert.AreEqual((0, 0), (_vanished.Count, _world.Count));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void WorldModel_Vanish_RaisesVanishedOnce_BeforeTheRemoval()
        {
            Monster();
            bool known = false;
            _world.Vanished += v => known = Has(v.Id);
            _world.Vanish(Poring, VanishReason.OutOfSight, 0);
            _world.Vanish(Poring, VanishReason.OutOfSight, 0);
            Assert.AreEqual(1, _vanished.Count);
            Assert.IsTrue(known);
        }

        [Test]
        public void WorldModel_Vanish_TheOwnCharacterIsNeverRemoved()
        {
            Human();
            _world.OwnId = Player;
            _world.Vanish(Player, VanishReason.Teleported, 0);
            Assert.IsTrue(Has(Player));
            _world.Vanish(Player, VanishReason.Died, 0);
            Assert.AreEqual(EntityState.Dead, Get(Player).State);
        }

        // ---- look, facing, posture (FR-8, FR-19) ----

        static EntityLook With(Func<EntityLook, EntityLook> change) => change(new EntityLook { Job = 1, HairStyle = 2 });

        static IEnumerable<TestCaseData> LookCases()
        {
            TestCaseData Case(LookType type, int value, int value2, Func<EntityLook, EntityLook> expected) => new TestCaseData(type, value, value2, With(expected)).SetName($"WorldModel_SetLook_{type}");
            yield return Case(LookType.Base, 4008, 0, l => { l.Job = 4008; return l; });
            yield return Case(LookType.Hair, 12, 0, l => { l.HairStyle = 12; return l; });
            yield return Case(LookType.Weapon, 1201, 2101, l => { l.Weapon = 1201; l.Shield = 2101; return l; });
            yield return Case(LookType.HeadBottom, 8, 0, l => { l.HeadBottom = 8; return l; });
            yield return Case(LookType.HeadTop, 16, 0, l => { l.HeadTop = 16; return l; });
            yield return Case(LookType.HeadMid, 10, 0, l => { l.HeadMid = 10; return l; });
            yield return Case(LookType.HairColour, 7, 0, l => { l.HairColour = 7; return l; });
            yield return Case(LookType.ClothColour, 3, 0, l => { l.ClothColour = 3; return l; });
            yield return Case(LookType.Shield, 2101, 0, l => { l.Shield = 2101; return l; });
            yield return Case(LookType.Robe, 1, 0, l => { l.Robe = 1; return l; });
            yield return Case(LookType.BodyStyle, 2, 0, l => { l.BodyStyle = 2; return l; });
            yield return Case((LookType)9, 5, 0, l => l);
            yield return Case((LookType)200, 5, 0, l => l);
        }

        [TestCaseSource(nameof(LookCases))]
        public void WorldModel_SetLook_EachLookType(LookType type, int value, int value2, EntityLook expected)
        {
            Human();
            int version = Get(Player).Version;
            _world.SetLook(Player, type, value, value2);
            Assert.AreEqual(expected, Get(Player).Look);
            Assert.AreEqual(!expected.Equals(new EntityLook { Job = 1, HairStyle = 2 }), version != Get(Player).Version);
            version = Get(Player).Version;
            _world.SetLook(Player, type, value, value2);
            Assert.AreEqual(version, Get(Player).Version, "the same value again changes nothing");
        }

        [Test]
        public void WorldModel_SetLook_MidWalk_GoesOnWalking()
        {
            Human();
            _world.Walk(Player, 50, 50, 55, 50, 0, 0);
            int animation = Get(Player).AnimationVersion;
            _world.SetLook(Player, LookType.HeadTop, 16, 0);
            Assert.IsTrue(Get(Player).Walking);
            Assert.AreEqual(animation, Get(Player).AnimationVersion);
        }

        [Test]
        public void WorldModel_Turn()
        {
            Human();
            int version = Get(Player).Version;
            _world.Turn(Player, UninarokDirection.West, HeadDirection.Right);
            Assert.AreEqual((UninarokDirection.West, HeadDirection.Right), (Get(Player).Facing, Get(Player).Head));
            Assert.AreNotEqual(version, Get(Player).Version);
            _world.Turn(Player, (UninarokDirection)10, (HeadDirection)7);
            Assert.AreEqual((UninarokDirection.West, HeadDirection.Straight), (Get(Player).Facing, Get(Player).Head), "10 is 2; an unknown head is straight");
        }

        [Test]
        public void WorldModel_SitStand()
        {
            Human();
            int animation = Get(Player).AnimationVersion;
            _world.SetState(Player, EntityState.Sitting);
            Assert.AreEqual(EntityState.Sitting, Get(Player).State);
            Assert.AreNotEqual(animation, animation = Get(Player).AnimationVersion);
            _world.SetState(Player, EntityState.Sitting);
            Assert.AreEqual(animation, Get(Player).AnimationVersion);
            _world.SetState(Player, EntityState.Standing);
            Assert.AreEqual(EntityState.Standing, Get(Player).State);
            _world.Walk(Player, 50, 50, 55, 50, 0, 0);
            _world.Advance(200);
            _world.SetState(Player, EntityState.Sitting);
            Assert.IsFalse(Get(Player).Walking, "sitting ends a walk");
        }

        [Test]
        public void WorldModel_SetSpeed_CountsFromTheNextStep()
        {
            Monster();
            _world.Walk(Poring, 50, 50, 53, 50, 0, 0);
            _world.Advance(100);
            _world.SetSpeed(Poring, 100);
            _world.Advance(200);
            Assert.AreEqual(51f, Get(Poring).PosX, "the step that had begun keeps its time");
            _world.Advance(300);
            Assert.AreEqual(52f, Get(Poring).PosX);
        }

        // ---- items (FR-16) ----

        [Test]
        public void WorldModel_Item_AddedAtItsSubCell_AndRemoved()
        {
            _world.AddItem(501, 909, true, 50, 60, 3, 12, 2);
            var e = Get(501);
            Assert.AreEqual((EntityKind.Item, 909, 2, true), (e.Kind, e.ItemId, e.ItemAmount, e.Identified));
            Assert.AreEqual((50, 60, 49.75f, 60.5f), (e.CellX, e.CellY, e.PosX, e.PosY), "sub / 12 from the cell's corner");
            _world.AddItem(501, 909, true, 50, 60, 3, 12, 2);
            Assert.AreEqual(1, _world.Count);
            _world.RemoveItem(501);
            Assert.AreEqual((0, 0), (_world.Count, _vanished.Count));
            _world.RemoveItem(501);
        }

        // ---- the whole ----

        [Test]
        public void WorldModel_Clear_EmptiesEverything()
        {
            Monster();
            Human();
            _world.AddItem(501, 909, true, 50, 60, 3, 12, 2);
            _world.OwnId = Player;
            _world.Clear();
            Assert.AreEqual((0, 0, 0u), (_world.Count, _world.SlotLimit, _world.OwnId));
            Assert.AreEqual(0, _vanished.Count);
            _world.Advance(100);
        }

        [Test]
        public void WorldModel_ZeroAllocation_300Walkers()
        {
            var world = new WorldModel(_grid, _settings);
            for (uint i = 1; i <= 300; i++)
                world.Upsert(i, i <= 100 ? EntityKind.Player : EntityKind.Monster, Look, 150, (int)(20 + i % 250), (int)(20 + i * 7 % 250), 0, 0, 0);
            uint now = 0;
            Action frames = () =>
            {
                for (int frame = 0; frame < 600; frame++)
                {
                    now += 16;
                    if (frame % 60 == 0)
                        for (uint i = 1; i <= 300; i++)
                        {
                            world.TryGetSlot(i, out int slot);
                            int x = world[slot].CellX, y = world[slot].CellY;
                            world.Walk(i, x, y, Math.Max(5, Math.Min(290, x + (int)((i + frame) % 13) - 6)), Math.Max(5, Math.Min(290, y + (int)((i * 3 + frame) % 13) - 6)), now, now);
                        }
                    world.Advance(now);
                }
            };
            frames();
            Assert.That(() => frames(), UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory());
        }
    }
}
