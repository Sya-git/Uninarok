using System;
using NUnit.Framework;
using Unity.Collections;
using Uninarok.Networking.Core.Wire;
using Uninarok.Networking.Data;
using Uninarok.Networking.Flow;
using Uninarok.Networking.Packets;
using Uninarok.Networking.Packets.In.Map;
using Uninarok.Networking.Tests;

namespace Uninarok.World.Tests
{
    // SPEC-0010 FR-1, FR-9, FR-13, FR-19 (PLAN-0010 4.6).
    public class OwnCharacterTests
    {
        WorldSessionTests.Rig _rig;

        [SetUp]
        public void SetUp() => _rig = new WorldSessionTests.Rig();

        [TearDown]
        public void TearDown() => _rig.Dispose();

        internal static byte[] Character(Func<CharInfo, CharInfo> change = null)
        {
            var c = new CharInfo
            {
                CharId = 140593, Hp = 40, MaxHp = 40, DefaultWalkSpeed = 150, Class = 7, Hair = 5, HairColor = 3, ClothesColor = 2, Body = 1,
                Weapon = 1201, LookShield = 2101, LookHeadTop = 16, LookHeadMid = 10, LookHeadBottom = 8, LookRobe = 1,
                Name = new FixedString32Bytes("One"), MapName = new FixedString32Bytes("prontera.gat"), Sex = AccountSex.SEX_MALE,
            };
            if (change != null) c = change(c);
            var b = new byte[CharInfo.WireSize];
            var w = new PacketWriter(b);
            c.Write(ref w);
            return b;
        }

        uint Id => _rig.Flow.Context.AccountId;
        Entity Own => _rig.Get(Id);

        // ---- entry (FR-1, T5.2) ----

        [Test]
        public void OwnCharacter_AtEntry_HasTheSelectedLook_AndTheServersCellAndFacing()
        {
            _rig.ToLoadingMap(Character());
            _rig.Session.BeginMap(_rig.Grid);
            Assert.AreEqual(Id, _rig.Model.OwnId);
            var e = Own;
            Assert.AreEqual((EntityKind.Player, EntityState.Standing, 150), (e.Kind, e.State, (int)e.Speed));
            Assert.AreEqual((150, 200, UninarokDirection.South), (e.CellX, e.CellY, e.Facing), "AUTHENTICATION_ACCEPTED");
            var l = e.Look;
            Assert.AreEqual((7, 5, 3, 2, 1), ((int)l.Job, (int)l.HairStyle, (int)l.HairColour, (int)l.ClothColour, (int)l.BodyStyle));
            Assert.AreEqual((1201, 2101, 16, 10, 8, 1, 1), (l.Weapon, l.Shield, (int)l.HeadTop, (int)l.HeadMid, (int)l.HeadBottom, (int)l.Robe, (int)l.Sex));
            Assert.IsFalse(_rig.Session.Own.CanAct, "not in game before MAP_LOADED");
            _rig.Flow.MapLoaded();
            Assert.IsTrue(_rig.Session.Own.CanAct);
        }

        [Test]
        public void OwnCharacter_Speed_ComesFromTheStatusValue_AlsoBeforeTheMapIsLoaded()
        {
            _rig.ToLoadingMap(Character());
            _rig.Client.Deliver(WorldSessionTests.Parameter(0, 120));
            _rig.Client.Deliver(WorldSessionTests.Parameter(5, 999));
            _rig.Session.BeginMap(_rig.Grid);
            Assert.AreEqual(120, Own.Speed);
            _rig.Client.Deliver(WorldSessionTests.Parameter(0, 90));
            Assert.AreEqual(90, Own.Speed);
        }

        [Test]
        public void OwnCharacter_AfterAMapChange_KeepsWhatTheServerSaid_AndFacesNorth()
        {
            _rig.ToInGame(Character());
            _rig.Client.Deliver(new UNIT_LOOK_CHANGE(WorldSessionTests.Hex(WorldSessionTests.Le(Id) + "04" + WorldSessionTests.Le(220) + WorldSessionTests.Le(0))));
            _rig.Client.Deliver(WorldSessionTests.Parameter(0, 90));
            var change = new byte[20];
            change[16] = 30;
            change[18] = 40;
            _rig.Client.Deliver(new MAP_CHANGE(change));
            _rig.Session.BeginMap(_rig.Grid);
            _rig.Flow.MapLoaded();
            Assert.AreEqual((30, 40, UninarokDirection.North), (Own.CellX, Own.CellY, Own.Facing), "PLAN-0010 D14");
            Assert.AreEqual((220, 90), ((int)Own.Look.HeadTop, (int)Own.Speed));

            _rig.Settings.WarpKeepsDirection = true;
            _rig.Client.Deliver(new UNIT_DIRECTION(WorldSessionTests.Hex(WorldSessionTests.Le(Id) + "0000" + "02")));
            _rig.Client.Deliver(new MAP_CHANGE(change));
            _rig.Session.BeginMap(_rig.Grid);
            Assert.AreEqual(UninarokDirection.West, Own.Facing);
        }

        [Test]
        public void OwnCharacter_DeadOnEntry_IsNotDeadAfterAMapChange()
        {
            _rig.ToInGame(Character(c => { c.Hp = 0; return c; }));
            Assert.AreEqual(EntityState.Dead, Own.State);
            _rig.Client.Deliver(new MAP_CHANGE(new byte[20]));    // respawned at the save point
            _rig.Session.BeginMap(_rig.Grid);
            Assert.AreEqual(EntityState.Standing, Own.State);
        }

        [Test]
        public void OwnCharacter_DeadOnEntry_NoInput()
        {
            _rig.ToInGame(Character(c => { c.Hp = 0; return c; }));
            Assert.AreEqual(EntityState.Dead, Own.State);
            Assert.IsFalse(_rig.Session.Own.CanAct);
            Assert.AreEqual(WalkRequestResult.Dead, _rig.Session.Own.RequestWalk(155, 200));
            Assert.IsFalse(_rig.Session.Own.TurnTowards(155, 200) || _rig.Session.Own.Doridori() || _rig.Session.Own.Bangbang());
            Assert.IsFalse(_rig.Session.Own.Sit() || _rig.Session.Own.Stand() || _rig.Session.Own.ToggleSit());
            Assert.AreEqual(0, _rig.Client.Sent.Count);
        }

        [Test]
        public void OwnCharacter_AnotherCharacter_StartsOver()
        {
            _rig.ToInGame(Character());
            _rig.Client.Deliver(WorldSessionTests.Parameter(0, 90));
            _rig.Client.RaiseDisconnected(new Uninarok.Networking.Core.Transport.CloseInfo(Uninarok.Networking.Core.Transport.CloseReason.RemoteClosed, "gone"), Uninarok.Networking.Core.Transport.ConnectionRole.Map);
            _rig.Flow.Reset();
            _rig.ToInGame(Character(c => { c.Class = 4; return c; }));
            Assert.AreEqual((4, 150), ((int)Own.Look.Job, (int)Own.Speed));
        }

        [Test]
        public void OwnCharacter_OnAMapWithoutData_IsTracked()
        {
            _rig.ToLoadingMap(Character());
            _rig.Session.BeginMap((Uninarok.Map.MapData)null);
            _rig.Flow.MapLoaded();
            Assert.AreEqual((150, 200), (Own.CellX, Own.CellY));
            _rig.Client.Deliver(WorldSessionTests.Stand(77, 10, 10));
            Assert.AreEqual(10, _rig.Get(77).CellX);
            _rig.Client.Deliver(WorldSessionTests.Move(77, 10, 10, 12, 10, _rig.Clock.Now));
            Assert.AreEqual(12, _rig.Get(77).CellX, "no cells to find a path on: the unit is placed where it goes");
        }

        [Test]
        public void OwnCharacter_Vanish_DiedPacket_LiesDead()
        {
            _rig.ToInGame(Character());
            _rig.Client.Deliver(WorldSessionTests.VanishPacket(Id, 1));
            Assert.AreEqual(EntityState.Dead, Own.State);
            Assert.IsFalse(_rig.Session.Own.CanAct);
        }

        // ---- walk (FR-9, T5.3); the own character stands on 150,200 ----

        WalkRequestResult Request(int x, int y, bool sends = false)
        {
            var own = _rig.Session.Own;
            _rig.Client.ClearRecords();
            var check = own.CheckWalk(x, y);
            Assert.AreEqual(0, _rig.Client.Sent.Count, "the dry run sends nothing");
            var result = own.RequestWalk(x, y);
            Assert.AreEqual(check, result, "the dry run says the same");
            Assert.AreEqual(sends ? 1 : 0, _rig.Client.Sent.Count);
            return result;
        }

        [Test]
        public void OwnCharacter_RequestWalk_Sent_AndTheWalkStartsOnlyOnTheAnswer()
        {
            _rig.ToInGame(Character());
            Assert.AreEqual(WalkRequestResult.Sent, Request(160, 200, sends: true));
            Assert.AreEqual(WorldSessionTests.Hex("5F03" + WorldSessionTests.Pos(160, 200, 0)), _rig.Client.Sent[0].Bytes);
            Assert.IsFalse(Own.Walking, "no prediction");
            _rig.Local.AdvanceMs(500);
            _rig.Model.Advance(_rig.Clock.Now);
            Assert.AreEqual(150f, Own.PosX);

            _rig.Client.Deliver(WorldSessionTests.OwnMove(150, 200, 160, 200, _rig.Clock.Now));
            Assert.IsTrue(Own.Walking);
            _rig.Local.AdvanceMs(1500);
            _rig.Model.Advance(_rig.Clock.Now);
            Assert.AreEqual((160, false), (Own.CellX, Own.Walking));
        }

        [Test]
        public void OwnCharacter_RequestWalk_NotInGame()
        {
            Assert.AreEqual(WalkRequestResult.NotInGame, _rig.Session.Own.RequestWalk(1, 1));
            _rig.ToLoadingMap(Character());
            _rig.Session.BeginMap(_rig.Grid);
            Assert.AreEqual(WalkRequestResult.NotInGame, Request(155, 200), "the map is loading");
            _rig.Flow.MapLoaded();
            _rig.Client.AcceptSends = false;
            Assert.AreEqual(WalkRequestResult.NotInGame, _rig.Session.Own.RequestWalk(155, 200), "nothing was queued");
        }

        [Test]
        public void OwnCharacter_RequestWalk_Dead_Sitting_SameCell()
        {
            _rig.ToInGame(Character());
            Assert.AreEqual(WalkRequestResult.SameCell, Request(150, 200));
            _rig.Client.Deliver(WorldSessionTests.Act(Id, 2));
            Assert.AreEqual(WalkRequestResult.Sitting, Request(155, 200));
            _rig.Client.Deliver(WorldSessionTests.VanishPacket(Id, 1));
            Assert.AreEqual(WalkRequestResult.Dead, Request(155, 200));
        }

        [Test]
        public void OwnCharacter_RequestWalk_Blocked_NoPath_TooLong()
        {
            _rig.Grid.Wall = (x, y) => (x == 155 && y == 200) || (Math.Max(Math.Abs(x - 145), Math.Abs(y - 200)) == 2);
            _rig.ToInGame(Character());
            Assert.AreEqual(WalkRequestResult.Blocked, Request(155, 200));
            Assert.AreEqual(WalkRequestResult.Blocked, Request(-1, 200));
            Assert.AreEqual(WalkRequestResult.NoPath, Request(145, 200), "inside a closed wall");
            Assert.AreEqual(WalkRequestResult.TooLong, Request(150, 220), "20 straight cells");
            Assert.AreEqual(WalkRequestResult.Sent, Request(150, 217, sends: true));
            Assert.AreEqual(WalkRequestResult.TooLong, Request(165, 200), "15 cells around an obstacle");
            Assert.AreEqual(WalkRequestResult.Sent, Request(164, 200, sends: true));
        }

        [Test]
        public void OwnCharacter_RequestWalk_Occupied()
        {
            _rig.ToInGame(Character());
            _rig.Client.Deliver(WorldSessionTests.Stand(77, 151, 201));
            Assert.AreEqual(WalkRequestResult.Occupied, Request(151, 201), "one cell, onto a monster");
            _rig.Client.Deliver(WorldSessionTests.Move(77, 151, 201, 160, 201, _rig.Clock.Now));
            Assert.AreEqual(WalkRequestResult.Sent, Request(151, 201, sends: true), "it walks away");
            _rig.Client.Deliver(WorldSessionTests.Stand(78, 152, 200));
            Assert.AreEqual(WalkRequestResult.Sent, Request(152, 200, sends: true), "two cells away the server does not look");
            _rig.Client.Deliver(WorldSessionTests.Stand(79, 149, 200, type: 6, job: 117));
            Assert.AreEqual(WalkRequestResult.Occupied, Request(149, 200), "an NPC");
            _rig.Client.Deliver(WorldSessionTests.Stand(80, 149, 199, type: 6, job: 139));
            Assert.AreEqual(WalkRequestResult.Sent, Request(149, 199, sends: true), "a hidden warp");
            _rig.Client.Deliver(WorldSessionTests.Stand(81, 150, 199));
            _rig.Client.Deliver(WorldSessionTests.VanishPacket(81, 1));
            Assert.AreEqual(WalkRequestResult.Sent, Request(150, 199, sends: true), "a dead monster");
            _rig.Settings.CellStackLimit = 0;
            Assert.AreEqual(WalkRequestResult.Sent, Request(149, 200, sends: true));
        }

        [Test]
        public void OwnCharacter_RequestWalk_WhileWalking_StartsFromTheServersCell()
        {
            _rig.ToInGame(Character());
            _rig.Client.Deliver(WorldSessionTests.OwnMove(150, 200, 160, 200, _rig.Clock.Now));
            _rig.Local.AdvanceMs(400);                          // shown at 152.67, the server's cell is 152
            _rig.Model.Advance(_rig.Clock.Now);
            Assert.AreEqual(152, Own.CellX);
            Assert.AreEqual(WalkRequestResult.Sent, Request(169, 200, sends: true), "17 cells from 152");
            Assert.AreEqual(WalkRequestResult.TooLong, Request(170, 200));
        }

        // ---- turn (FR-13, T5.4) ----

        static byte[] ChangeDirection(HeadDirection head, UninarokDirection body) => WorldSessionTests.Hex($"6103{(int)head:X2}00{(int)body:X2}");

        static object[][] Neighbours() => new[]
        {
            // facing south (4): one step beside is the head; farther, the head looks there and the body stops one step short;
            // behind, both (the owner's comparison with the original client, 2026-11-22)
            new object[] { 0, -1, UninarokDirection.South, HeadDirection.Straight },
            new object[] { 1, -1, UninarokDirection.South, HeadDirection.Left },      // south-east is 5, one step counter-clockwise
            new object[] { -1, -1, UninarokDirection.South, HeadDirection.Right },    // south-west is 3
            new object[] { 1, 0, UninarokDirection.SouthEast, HeadDirection.Left },
            new object[] { -1, 0, UninarokDirection.SouthWest, HeadDirection.Right },
            new object[] { 1, 1, UninarokDirection.East, HeadDirection.Left },
            new object[] { 0, 1, UninarokDirection.North, HeadDirection.Straight },
            new object[] { -1, 1, UninarokDirection.West, HeadDirection.Right },
        };

        [TestCaseSource(nameof(Neighbours))]
        public void OwnCharacter_TurnTowards_8Neighbours(int dx, int dy, UninarokDirection body, HeadDirection head)
        {
            _rig.ToInGame(Character());
            Assert.IsTrue(_rig.Session.Own.TurnTowards(150 + dx, 200 + dy));
            Assert.AreEqual(1, _rig.Client.Sent.Count);
            Assert.AreEqual(ChangeDirection(head, body), _rig.Client.Sent[0].Bytes);
            Assert.AreEqual((body, head), (Own.Facing, Own.Head), "applied when it was sent: the server does not echo it (PLAN-0010 D9)");
        }

        [TestCase(1, HeadDirection.Left, UninarokDirection.SouthEast)]
        [TestCase(-1, HeadDirection.Right, UninarokDirection.SouthWest)]
        public void OwnCharacter_TurnTowards_TheHeadsSideAgain_TurnsTheBody(int dx, HeadDirection head, UninarokDirection body)
        {
            _rig.ToInGame(Character());
            Assert.IsTrue(_rig.Session.Own.TurnTowards(150 + dx, 199));
            Assert.AreEqual((UninarokDirection.South, head), (Own.Facing, Own.Head));
            Assert.IsTrue(_rig.Session.Own.TurnTowards(150 - dx, 199), "the other side");
            Assert.AreEqual(UninarokDirection.South, Own.Facing, "turns the head over");
            Assert.IsTrue(_rig.Session.Own.TurnTowards(150 + dx, 199));
            Assert.AreEqual((UninarokDirection.South, head), (Own.Facing, Own.Head));
            Assert.IsTrue(_rig.Session.Own.TurnTowards(150 + dx, 199));
            Assert.AreEqual(ChangeDirection(HeadDirection.Straight, body), _rig.Client.Sent[3].Bytes);
            Assert.AreEqual((body, HeadDirection.Straight), (Own.Facing, Own.Head));
        }

        // The owner's pictures: body west, head south-west; a click on the east turns both east.
        [Test]
        public void OwnCharacter_TurnTowards_Behind_TurnsBodyAndHead()
        {
            _rig.ToInGame(Character());
            var own = _rig.Session.Own;
            own.TurnTowards(149, 200);
            Assert.AreEqual((UninarokDirection.SouthWest, HeadDirection.Right), (Own.Facing, Own.Head));
            own.TurnTowards(149, 200);
            Assert.AreEqual((UninarokDirection.West, HeadDirection.Straight), (Own.Facing, Own.Head), "the same click again");
            own.TurnTowards(149, 199);
            Assert.AreEqual((UninarokDirection.West, HeadDirection.Left), (Own.Facing, Own.Head));
            own.TurnTowards(151, 200);
            Assert.AreEqual((UninarokDirection.East, HeadDirection.Straight), (Own.Facing, Own.Head));
        }

        [Test]
        public void OwnCharacter_TurnTowards_FarCells_OwnCell_AndWhileWalking()
        {
            _rig.ToInGame(Character());
            Assert.IsTrue(_rig.Session.Own.TurnTowards(150, 260));
            Assert.AreEqual(UninarokDirection.North, Own.Facing, "any distance, also farther than a walk");
            Assert.IsTrue(_rig.Session.Own.TurnTowards(152, 203));
            Assert.AreEqual((UninarokDirection.North, HeadDirection.Right), (Own.Facing, Own.Head), "north-east by map_calc_dir");
            Assert.IsFalse(_rig.Session.Own.TurnTowards(150, 200));
            _rig.Client.Deliver(WorldSessionTests.OwnMove(150, 200, 160, 200, _rig.Clock.Now));
            _rig.Client.ClearRecords();
            Assert.IsFalse(_rig.Session.Own.TurnTowards(150, 190));
            Assert.AreEqual(0, _rig.Client.Sent.Count);
        }

        [Test]
        public void OwnCharacter_Turn_NotSent_ChangesNothing()
        {
            _rig.ToInGame(Character());
            _rig.Client.AcceptSends = false;
            Assert.IsFalse(_rig.Session.Own.TurnTowards(150, 201));
            Assert.AreEqual(UninarokDirection.South, Own.Facing);
        }

        [Test]
        public void OwnCharacter_Directions_MatchMapCalcDir()
        {
            var expected = new[] { (0, 5, 0), (1, 5, 0), (3, 5, 7), (5, 5, 7), (5, 2, 7), (6, 2, 6), (5, 0, 6), (3, -5, 5), (6, -2, 6), (1, -5, 4), (0, -5, 4), (-3, -5, 3), (-6, -2, 2), (-1, -5, 4), (-5, 0, 2), (-3, 5, 1), (-6, 2, 2), (-1, 5, 0) };
            foreach (var (dx, dy, d) in expected) Assert.AreEqual(d, OwnCharacter.DirectionTo(50, 50, 50 + dx, 50 + dy), $"{dx},{dy}");
        }

        [Test]
        public void OwnCharacter_Doridori_Sequence()
        {
            _rig.ToInGame(Character());
            var own = _rig.Session.Own;
            foreach (var head in new[] { HeadDirection.Left, HeadDirection.Right, HeadDirection.Straight, HeadDirection.Left })
            {
                _rig.Client.ClearRecords();
                Assert.IsTrue(own.Doridori());
                Assert.AreEqual(1, _rig.Client.Sent.Count, "one step for each command");
                Assert.AreEqual(ChangeDirection(head, UninarokDirection.South), _rig.Client.Sent[0].Bytes);
                Assert.AreEqual((UninarokDirection.South, head), (Own.Facing, Own.Head));
            }
            _rig.Client.Deliver(WorldSessionTests.OwnMove(150, 200, 160, 200, _rig.Clock.Now));
            Assert.IsFalse(own.Doridori(), "not while walking");
        }

        [Test]
        public void OwnCharacter_Bangbang_Clockwise()
        {
            _rig.ToInGame(Character());
            var seen = new System.Collections.Generic.List<UninarokDirection>();
            for (int i = 0; i < 8; i++)
            {
                Assert.IsTrue(_rig.Session.Own.Bangbang());
                seen.Add(Own.Facing);
            }
            CollectionAssert.AreEqual(new[]
            {
                UninarokDirection.SouthWest, UninarokDirection.West, UninarokDirection.NorthWest, UninarokDirection.North,
                UninarokDirection.NorthEast, UninarokDirection.East, UninarokDirection.SouthEast, UninarokDirection.South,
            }, seen, "south, west, north, east: clockwise from above");
            Assert.AreEqual(ChangeDirection(HeadDirection.Straight, UninarokDirection.SouthWest), _rig.Client.Sent[0].Bytes);
        }

        // ---- sit (FR-19, T5.5) ----

        [Test]
        public void OwnCharacter_Sit_TogglesOnConfirmOnly()
        {
            _rig.ToInGame(Character());
            var own = _rig.Session.Own;
            Assert.IsTrue(own.ToggleSit());
            Assert.AreEqual(WorldSessionTests.Hex("37040000000002"), _rig.Client.Sent[0].Bytes);
            Assert.AreEqual(EntityState.Standing, Own.State, "not before the server says so: a refused one stays standing");

            _rig.Client.Deliver(WorldSessionTests.Act(Id, 2));
            Assert.AreEqual(EntityState.Sitting, Own.State);
            _rig.Client.ClearRecords();
            Assert.AreEqual(WalkRequestResult.Sitting, own.RequestWalk(155, 200));
            Assert.AreEqual(0, _rig.Client.Sent.Count);
            Assert.IsTrue(own.TurnTowards(150, 201), "turning works as standing");
            Assert.IsFalse(own.Sit(), "it sits already");

            _rig.Client.ClearRecords();
            Assert.IsTrue(own.ToggleSit());
            Assert.AreEqual(WorldSessionTests.Hex("37040000000003"), _rig.Client.Sent[0].Bytes);
            Assert.AreEqual(EntityState.Sitting, Own.State);
            _rig.Client.Deliver(WorldSessionTests.Act(Id, 3));
            Assert.AreEqual(EntityState.Standing, Own.State);
            Assert.IsFalse(own.Stand());
        }

        [Test]
        public void OwnCharacter_Sit_BeforeTheMap_SendsNothing()
        {
            Assert.IsFalse(_rig.Session.Own.ToggleSit() || _rig.Session.Own.Sit() || _rig.Session.Own.Stand());
            Assert.AreEqual(0, _rig.Client.Sent.Count);
        }
    }
}
