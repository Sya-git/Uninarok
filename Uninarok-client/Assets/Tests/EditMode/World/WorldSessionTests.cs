using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Uninarok.Networking.Client;
using Uninarok.Networking.Core.Transport;
using Uninarok.Networking.Data;
using Uninarok.Networking.Flow;
using Uninarok.Networking.Packets.In.Map;
using Uninarok.Networking.Tests;

namespace Uninarok.World.Tests
{
    // SPEC-0010 FR-3 to FR-8, FR-15, FR-16, FR-18, FR-19 (PLAN-0010 4.6): what each packet does to the model, with the simulated
    // connection and no scene.
    public class WorldSessionTests
    {
        internal sealed class OpenGrid : IWalkGrid
        {
            public Func<int, int, bool> Wall = (x, y) => false;
            public int Width => 400;
            public int Height => 400;
            public bool IsPassable(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height && !Wall(x, y);
            public bool BlocksSight(int x, int y) => !IsPassable(x, y);
        }

        /// <summary>A flow over the simulated connection, brought to a map.</summary>
        internal sealed class Rig : IDisposable
        {
            public readonly FakeNetworkClient Client = new FakeNetworkClient();
            public readonly FakeClock Local = new FakeClock { NowMs = 1_000 };
            public readonly NetworkFlow Flow;
            public readonly ServerClock Clock;
            public readonly WorldSettings Settings = ScriptableObject.CreateInstance<WorldSettings>();
            public readonly WorldSession Session;
            public readonly OpenGrid Grid = new OpenGrid();

            public Rig()
            {
                Flow = new NetworkFlow(Client, Local, new NetworkSettings(), new ServerEndpoint("127.0.0.1", 6900, default, 0, 0, 0));
                Clock = new ServerClock(Local);
                Session = new WorldSession(Client, Flow, Clock, Settings);
            }

            public WorldModel Model => Session.Model;

            /// <summary>Login to LoadingMap with one character, accepted by the map server.</summary>
            public Rig ToLoadingMap(byte[] character = null, bool authenticate = true)
            {
                Flow.StartLogin("u", "p");
                Client.RaiseConnected(ConnectionRole.Login);
                Client.Deliver(TestPackets.AcceptLogin(TestPackets.ServerEntry(new byte[] { 10, 0, 0, 1 }, 6121, "First")));
                Flow.SelectServer(0);
                Client.RaiseConnected(ConnectionRole.Character);
                Client.Deliver(TestPackets.CharacterData(character ?? TestPackets.CharBytes(140593, "One", 0)));
                Flow.SelectCharacter(0);
                Client.Deliver(TestPackets.MapAccepted(140593, "prontera.gat", new byte[] { 10, 0, 0, 2 }, 6122));
                Client.RaiseConnected(ConnectionRole.Map);
                if (authenticate) Client.Deliver(TestPackets.AuthenticationAccepted());
                return this;
            }

            /// <summary>In game on a map whose cells are <see cref="Grid"/>.</summary>
            public Rig ToInGame(byte[] character = null)
            {
                ToLoadingMap(character);
                Session.BeginMap(Grid);
                Flow.MapLoaded();
                Client.ClearRecords();
                return this;
            }

            public Entity Get(uint id)
            {
                Assert.IsTrue(Model.TryGetSlot(id, out int slot), $"no entity {id}");
                return Model[slot];
            }

            public bool Has(uint id) => Model.TryGetSlot(id, out _);

            public void Dispose()
            {
                Session.Dispose();
                Flow.Dispose();
                UnityEngine.Object.DestroyImmediate(Settings);
            }
        }

        // ---- bytes ----

        internal static byte[] Hex(string s)
        {
            var b = new byte[s.Length / 2];
            for (int i = 0; i < b.Length; i++) b[i] = Convert.ToByte(s.Substring(i * 2, 2), 16);
            return b;
        }

        internal static string Le(uint v) => $"{v & 0xFF:X2}{(v >> 8) & 0xFF:X2}{(v >> 16) & 0xFF:X2}{v >> 24:X2}";
        internal static string Le16(int v) => $"{v & 0xFF:X2}{(v >> 8) & 0xFF:X2}";

        internal static string Pos(int x, int y, int dir) => $"{(x >> 2) & 0xFF:X2}{((x << 6) | ((y >> 4) & 0x3F)) & 0xFF:X2}{((y << 4) | dir) & 0xFF:X2}";

        internal static string MoveData(int sx, int sy, int dx, int dy) =>
            $"{(sx >> 2) & 0xFF:X2}{((sx << 6) | ((sy >> 4) & 0x3F)) & 0xFF:X2}{((sy << 4) | ((dx >> 6) & 0xF)) & 0xFF:X2}{((dx << 2) | ((dy >> 8) & 3)) & 0xFF:X2}{dy & 0xFF:X2}88";

        static string Head(byte type, uint id, uint gid, short job, short speed = 200) =>
            $"{type:X2}" + Le(id) + Le(gid) + Le16(speed) + "0000" + "0000" + "00000000" + Le16(job) + "0300" + "B1040000" + "35080000" + "0500";

        const string Middle = "1000" + "0A00" + "0700" + "0200" + "0200" + "0300" + "11000000" + "0100" + "0200" + "04000000" + "01" + "01";
        const string Tail = "6300" + "0000" + "10270000" + "E8030000" + "00" + "0100" + "000000000000000000000000000000000000000000000000";

        internal static UNIT_STAND_ENTRY Stand(uint id, int x, int y, byte type = 5, uint gid = 0, short job = 1002, byte state = 0, int dir = 4) =>
            new UNIT_STAND_ENTRY(Hex(Head(type, id, gid, job) + Middle + Pos(x, y, dir) + "0505" + $"{state:X2}" + Tail));

        internal static UNIT_NEW_ENTRY New(uint id, int x, int y, byte type = 5, uint gid = 0, short job = 1002) =>
            new UNIT_NEW_ENTRY(Hex(Head(type, id, gid, job) + Middle + Pos(x, y, 4) + "0505" + Tail));

        internal static UNIT_MOVE_ENTRY MoveEntry(uint id, int sx, int sy, int dx, int dy, uint tick, byte type = 5, short speed = 200) =>
            new UNIT_MOVE_ENTRY(Hex(Head(type, id, 0, 1002, speed) + Le(tick) + Middle + MoveData(sx, sy, dx, dy) + "0505" + Tail));

        internal static UNIT_MOVE Move(uint id, int sx, int sy, int dx, int dy, uint tick) => new UNIT_MOVE(Hex(Le(id) + MoveData(sx, sy, dx, dy) + Le(tick)));
        internal static PLAYER_MOVE OwnMove(int sx, int sy, int dx, int dy, uint tick) => new PLAYER_MOVE(Hex(Le(tick) + MoveData(sx, sy, dx, dy)));
        internal static UNIT_ACT Act(uint id, byte action) => new UNIT_ACT(Hex(Le(id) + new string('F', 40) + $"{action:X2}" + "FFFF"));
        internal static UNIT_VANISH VanishPacket(uint id, byte type) => new UNIT_VANISH(Hex(Le(id) + $"{type:X2}"));
        internal static PARAMETER_CHANGE Parameter(int var, int value) => new PARAMETER_CHANGE(Hex(Le16(var) + Le((uint)value)));

        const uint Poring = 0x068EB5EC;

        Rig _rig;

        [SetUp]
        public void SetUp() => _rig = new Rig();

        [TearDown]
        public void TearDown() => _rig.Dispose();

        // ---- one packet, one change ----

        [Test]
        public void Stand_Entry_AddsTheUnit_WithKindLookAndState()
        {
            _rig.ToInGame();
            _rig.Client.Deliver(Stand(Poring, 150, 200, dir: 2));
            var e = _rig.Get(Poring);
            Assert.AreEqual((EntityKind.Monster, 1002, 200, EntityState.Standing), (e.Kind, (int)e.Look.Job, (int)e.Speed, e.State));
            Assert.AreEqual((150, 200, UninarokDirection.West), (e.CellX, e.CellY, e.Facing));
            Assert.AreEqual((3, 1201, 2101, 5, 16, 10), ((int)e.Look.HairStyle, e.Look.Weapon, e.Look.Shield, (int)e.Look.HeadBottom, (int)e.Look.HeadTop, (int)e.Look.HeadMid));
            Assert.AreEqual((7, 2, 3, 1, 1), ((int)e.Look.HairColour, (int)e.Look.ClothColour, (int)e.Look.Robe, (int)e.Look.Sex, (int)e.Look.BodyStyle));
            Assert.AreEqual(HeadDirection.Left, e.Head);

            _rig.Client.Deliver(Stand(77, 10, 10, type: 0, gid: 5, job: 7, state: 2));
            Assert.AreEqual((EntityKind.Player, EntityState.Sitting), (_rig.Get(77).Kind, _rig.Get(77).State));
            _rig.Client.Deliver(Stand(78, 10, 10, type: 0, gid: 6, job: 7, state: 1));
            Assert.AreEqual(EntityState.Dead, _rig.Get(78).State);
        }

        [Test]
        public void ObjectTypes_AreTheKindsOfTheDoc()
        {
            var kinds = new List<EntityKind>();
            for (byte t = 0; t <= 11; t++) kinds.Add(WorldSession.KindOf(t, 1));
            CollectionAssert.AreEqual(new[]
            {
                EntityKind.Player, EntityKind.Npc, EntityKind.Other, EntityKind.Other, EntityKind.Other, EntityKind.Monster, EntityKind.Npc,
                EntityKind.Pet, EntityKind.Homunculus, EntityKind.Mercenary, EntityKind.Elemental, EntityKind.Other,
            }, kinds);
            Assert.AreEqual(EntityKind.Monster, WorldSession.KindOf(0, 0), "a monster that looks like a player class");
        }

        [Test]
        public void New_Entry_AddsAStandingUnit()
        {
            _rig.ToInGame();
            _rig.Client.Deliver(New(Poring, 15, 20));
            Assert.AreEqual((15, 20, EntityState.Standing), (_rig.Get(Poring).CellX, _rig.Get(Poring).CellY, _rig.Get(Poring).State));
            _rig.Client.Deliver(New(Poring, 16, 20));
            Assert.AreEqual(16, _rig.Get(Poring).CellX);
            int count = 0;
            for (int i = 0; i < _rig.Model.SlotLimit; i++)
                if (_rig.Model.IsUsed(i) && _rig.Model[i].Id == Poring) count++;
            Assert.AreEqual(1, count, "never two entities for one id (FR-4)");
        }

        [Test]
        public void Move_Entry_PlacesTheUnitOnItsPath_ByTheServerClock()
        {
            _rig.ToInGame();
            uint now = _rig.Clock.Now;
            _rig.Client.Deliver(MoveEntry(Poring, 50, 50, 55, 50, now - 500));
            Assert.AreEqual((52, 52.5f), (_rig.Get(Poring).CellX, _rig.Get(Poring).PosX), "half a second into the walk");
        }

        [Test]
        public void Move_WalksAKnownUnit_FromThePacketsTick()
        {
            _rig.ToInGame();
            _rig.Client.Deliver(Stand(Poring, 50, 50));
            uint now = _rig.Clock.Now;
            _rig.Client.Deliver(Move(Poring, 50, 50, 55, 50, now));
            _rig.Local.AdvanceMs(1000);
            _rig.Model.Advance(_rig.Clock.Now);
            Assert.AreEqual((55, false), (_rig.Get(Poring).CellX, _rig.Get(Poring).Walking));
            _rig.Client.Deliver(Move(999, 50, 50, 55, 50, now));
            Assert.IsFalse(_rig.Has(999));
        }

        [Test]
        public void Stop_And_Slide()
        {
            _rig.ToInGame();
            _rig.Client.Deliver(Stand(Poring, 50, 50));
            _rig.Client.Deliver(Move(Poring, 50, 50, 55, 50, _rig.Clock.Now));
            _rig.Client.Deliver(new UNIT_STOP_MOVE(Hex(Le(Poring) + Le16(51) + Le16(50))));
            Assert.AreEqual((51, false), (_rig.Get(Poring).CellX, _rig.Get(Poring).Walking));
            _rig.Client.Deliver(new UNIT_SLIDE(Hex(Le(Poring) + Le16(60) + Le16(61))));
            Assert.AreEqual((60f, 61f), (_rig.Get(Poring).PosX, _rig.Get(Poring).PosY));
        }

        [Test]
        public void Vanish_ByReason()
        {
            _rig.ToInGame();
            var seen = new List<VanishReason>();
            _rig.Model.Vanished += v => seen.Add(v.Reason);
            for (byte reason = 0; reason <= 4; reason++)
            {
                uint id = 100u + reason;
                _rig.Client.Deliver(Stand(id, 50, 50));
                _rig.Client.Deliver(VanishPacket(id, reason));
                Assert.AreEqual(reason == 1 || reason == 4, _rig.Has(id), $"reason {reason}");
            }
            CollectionAssert.AreEqual(new[] { VanishReason.OutOfSight, VanishReason.Died, VanishReason.LoggedOut, VanishReason.Teleported, VanishReason.FeignedDeath }, seen);
        }

        [Test]
        public void Direction_Look_Act()
        {
            _rig.ToInGame();
            _rig.Client.Deliver(Stand(77, 10, 10, type: 0, gid: 5, job: 7));
            _rig.Client.Deliver(new UNIT_DIRECTION(Hex(Le(77) + "0100" + "06")));
            Assert.AreEqual((UninarokDirection.East, HeadDirection.Right), (_rig.Get(77).Facing, _rig.Get(77).Head));
            _rig.Client.Deliver(new UNIT_LOOK_CHANGE(Hex(Le(77) + "04" + Le(220) + Le(0))));
            Assert.AreEqual(220, _rig.Get(77).Look.HeadTop);
            _rig.Client.Deliver(Act(77, 2));
            Assert.AreEqual(EntityState.Sitting, _rig.Get(77).State);
            _rig.Client.Deliver(Act(77, 0));
            Assert.AreEqual(EntityState.Sitting, _rig.Get(77).State, "other actions belong to the combat spec");
            _rig.Client.Deliver(Act(77, 3));
            Assert.AreEqual(EntityState.Standing, _rig.Get(77).State);
        }

        [Test]
        public void Items_AppearFallAndLeave()
        {
            _rig.ToInGame();
            _rig.Client.Deliver(new ITEM_ENTRY(Hex("11000000" + "F5010000" + "01" + "9600" + "C800" + "0300" + "06" + "0C")));
            Assert.AreEqual((501, 3, 150, 200, 150f, 200.5f), (_rig.Get(17).ItemId, _rig.Get(17).ItemAmount, _rig.Get(17).CellX, _rig.Get(17).CellY, _rig.Get(17).PosX, _rig.Get(17).PosY));
            _rig.Client.Deliver(new ITEM_FALL_ENTRY(Hex("12000000" + "F5010000" + "0200" + "00" + "9600" + "C800" + "03" + "09" + "0100" + "01" + "0400")));
            Assert.AreEqual((false, 149.75f, 200.25f), (_rig.Get(18).Identified, _rig.Get(18).PosX, _rig.Get(18).PosY));
            _rig.Client.Deliver(new ITEM_DISAPPEAR(Hex("11000000")));
            Assert.IsFalse(_rig.Has(17));
            Assert.IsTrue(_rig.Has(18));
        }

        // ---- map change, entry, lifetime (FR-3, FR-9 of SPEC-0001, FR-15) ----

        [Test]
        public void MapChange_EmptiesTheWorld_AndPacketsWhileLoadingChangeNothing()
        {
            _rig.ToInGame();
            _rig.Client.Deliver(Stand(Poring, 50, 50));
            var old = _rig.Model;
            _rig.Client.Deliver(new MAP_CHANGE(new byte[20]));
            Assert.IsNull(_rig.Model);
            Assert.AreEqual(0, old.Count);
            _rig.Client.Deliver(Stand(Poring, 50, 50));
            _rig.Client.Deliver(Move(Poring, 50, 50, 51, 50, 0));
            _rig.Client.Deliver(VanishPacket(Poring, 0));
            _rig.Session.BeginMap(_rig.Grid);
            Assert.IsFalse(_rig.Has(Poring));
            Assert.AreEqual(0, _rig.Client.HandlerErrors.Count);
        }

        [Test]
        public void Disconnect_EmptiesTheWorld_AndReleasesTheHandlers()
        {
            _rig.ToInGame();
            Assert.AreEqual(1, _rig.Client.Dispatcher.HandlerCount<UNIT_MOVE>());
            _rig.Client.RaiseDisconnected(new CloseInfo(CloseReason.RemoteClosed, "gone"), ConnectionRole.Map);
            Assert.IsNull(_rig.Model);
            Assert.AreEqual(0, _rig.Client.Dispatcher.HandlerCount<UNIT_MOVE>());
        }

        [Test]
        public void Dispose_ReleasesEveryHandler()
        {
            _rig.ToInGame();
            var d = _rig.Client.Dispatcher;
            int Handlers() => d.HandlerCount<UNIT_STAND_ENTRY>() + d.HandlerCount<UNIT_NEW_ENTRY>() + d.HandlerCount<UNIT_MOVE_ENTRY>() + d.HandlerCount<UNIT_MOVE>()
                              + d.HandlerCount<PLAYER_MOVE>() + d.HandlerCount<UNIT_STOP_MOVE>() + d.HandlerCount<UNIT_SLIDE>() + d.HandlerCount<UNIT_VANISH>()
                              + d.HandlerCount<UNIT_DIRECTION>() + d.HandlerCount<UNIT_LOOK_CHANGE>() + d.HandlerCount<UNIT_ACT>() + d.HandlerCount<ITEM_ENTRY>()
                              + d.HandlerCount<ITEM_FALL_ENTRY>() + d.HandlerCount<ITEM_DISAPPEAR>() + d.HandlerCount<PARAMETER_CHANGE>();
            Assert.AreEqual(15, Handlers());
            _rig.Session.Dispose();
            Assert.AreEqual(0, Handlers());
            Assert.IsNull(_rig.Model);
        }

        [Test]
        public void TheClock_StartsAtEntry_AndIsSampledByTheHeartbeat_ButNotByAMapChange()
        {
            _rig.ToLoadingMap();
            Assert.AreEqual(0x01020304u, _rig.Clock.Now, "AUTHENTICATION_ACCEPTED");
            _rig.Session.BeginMap(_rig.Grid);
            _rig.Flow.MapLoaded();
            _rig.Local.AdvanceMs(100);
            _rig.Client.Deliver(new SERVER_HEARTBEAT_RESPONSE(BitConverter.GetBytes(0x01020304u + 500)));
            Assert.AreEqual(0x01020304u + 550, _rig.Clock.Now);
            _rig.Local.AdvanceMs(1000);
            _rig.Client.Deliver(new MAP_CHANGE(new byte[20]));
            Assert.AreEqual(0x01020304u + 1550, _rig.Clock.Now);
        }

        [Test]
        public void WorldSession_ViewlessEntitiesStillUpdate()
        {
            // no scene, no view: a whole life of a unit
            _rig.ToInGame();
            _rig.Client.Deliver(New(Poring, 50, 50));
            _rig.Client.Deliver(Move(Poring, 50, 50, 53, 50, _rig.Clock.Now));
            _rig.Local.AdvanceMs(300);
            _rig.Model.Advance(_rig.Clock.Now);
            Assert.AreEqual(51.5f, _rig.Get(Poring).PosX);
            _rig.Local.AdvanceMs(1000);
            _rig.Model.Advance(_rig.Clock.Now);
            Assert.AreEqual(53, _rig.Get(Poring).CellX);
            _rig.Client.Deliver(VanishPacket(Poring, 1));
            _rig.Local.AdvanceMs(6000);
            _rig.Model.Advance(_rig.Clock.Now);
            Assert.IsFalse(_rig.Has(Poring));
        }

        [Test]
        public void HotHandlers_AllocateNothing()
        {
            _rig.ToInGame();
            uint tick = _rig.Clock.Now;
            var stand = Stand(Poring, 50, 50);
            var spawn = New(5, 60, 60);
            var walking = MoveEntry(6, 50, 50, 55, 50, tick);
            var move = Move(Poring, 50, 50, 55, 52, tick);
            var own = OwnMove(150, 200, 152, 200, tick);
            var stop = new UNIT_STOP_MOVE(Hex(Le(Poring) + Le16(51) + Le16(50)));
            var slide = new UNIT_SLIDE(Hex(Le(Poring) + Le16(51) + Le16(50)));
            var act = Act(6, 2);
            var item = new ITEM_ENTRY(Hex("11000000" + "F5010000" + "01" + "9600" + "C800" + "0300" + "06" + "0C"));
            var fall = new ITEM_FALL_ENTRY(Hex("12000000" + "F5010000" + "0200" + "00" + "9600" + "C800" + "03" + "09" + "0100" + "01" + "0400"));
            var gone = new ITEM_DISAPPEAR(Hex("11000000"));
            var vanish = VanishPacket(5, 0);
            var look = new UNIT_LOOK_CHANGE(Hex(Le(6) + "04" + Le(220) + Le(0)));
            var turn = new UNIT_DIRECTION(Hex(Le(6) + "0100" + "06"));
            Action run = () =>
            {
                for (int i = 0; i < 100; i++)
                {
                    _rig.Client.Deliver(stand);
                    _rig.Client.Deliver(spawn);
                    _rig.Client.Deliver(walking);
                    _rig.Client.Deliver(move);
                    _rig.Client.Deliver(own);
                    _rig.Client.Deliver(stop);
                    _rig.Client.Deliver(slide);
                    _rig.Client.Deliver(act);
                    _rig.Client.Deliver(item);
                    _rig.Client.Deliver(fall);
                    _rig.Client.Deliver(gone);
                    _rig.Client.Deliver(look);
                    _rig.Client.Deliver(turn);
                    _rig.Client.Deliver(vanish);
                }
            };
            run();
            Assert.That(() => run(), UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory());
            Assert.AreEqual(0, _rig.Client.HandlerErrors.Count);
        }
    }
}
