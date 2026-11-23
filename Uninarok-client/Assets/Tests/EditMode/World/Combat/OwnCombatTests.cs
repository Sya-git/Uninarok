using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Uninarok.Networking.Packets;
using Uninarok.Networking.Packets.In.Map;
using static Uninarok.World.Tests.WorldSessionTests;

namespace Uninarok.World.Tests
{
    // SPEC-0011 FR-2, FR-3, FR-10 (PLAN-0011 4.5): the own character's attacks and pickups through the simulated connection, no
    // scene. The own character enters at (150, 200) with a walk speed of 150; the clock moves in steps of 50 ms.
    public class OwnCombatTests
    {
        const uint MonsterA = 0x068EB5EC, MonsterB = 0x068EB5ED, MonsterC = 0x068EB5EE, PlayerId = 2000003, NpcId = 110000123, PetId = 0x068EB600;
        const uint ItemA = 9001, ItemB = 9002, ItemC = 9003;
        const byte Single = 0, Continuous = 7;

        Rig _rig;
        readonly List<string> _info = new List<string>();

        [SetUp]
        public void SetUp()
        {
            _rig = new Rig().ToInGame(OwnCharacterTests.Character());
            Application.logMessageReceived += OnLog;
        }

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= OnLog;
            _info.Clear();
            _rig.Dispose();
        }

        void OnLog(string message, string stack, LogType type)
        {
            if (message.StartsWith("[Net] ", StringComparison.Ordinal)) _info.Add(message);
        }

        OwnCombat Combat => _rig.Session.Combat;
        uint Own => _rig.Flow.Context.AccountId;
        uint Now => _rig.Clock.Now;
        List<Uninarok.Networking.Tests.FakeNetworkClient.SentPacket> Sent => _rig.Client.Sent;

        // ---- helpers ----

        void Monster(uint id, int x, int y, byte state = 0) => _rig.Client.Deliver(Stand(id, x, y, state: state));

        void Item(uint id, int x, int y) => _rig.Model.AddItem(id, 501, true, x, y, 6, 6, 1);

        /// <summary>The clock, the model and the combat, in steps of 50 ms.</summary>
        void Run(int ms)
        {
            for (int t = 0; t < ms; t += 50)
            {
                _rig.Local.AdvanceMs(50);
                _rig.Model.Advance(Now);
                Combat.Tick(Now);
            }
        }

        /// <summary>The server accepts the own walk to a cell now.</summary>
        void ServerWalks(int x, int y)
        {
            var own = _rig.Get(Own);
            _rig.Client.Deliver(OwnMove(own.CellX, own.CellY, x, y, Now));
        }

        void Blow(uint source, uint target, int attackMt = 500) =>
            _rig.Client.Deliver(new DAMAGE(Hex(Le(source) + Le(target) + Le(Now) + Le((uint)attackMt) + Le(300u) + Le(10u) + "00" + Le16(1) + "00" + Le(0u))));

        void TooFar(uint target, int tx, int ty, int range)
        {
            var own = _rig.Get(Own);
            _rig.Client.Deliver(new ATTACK_TOO_FAR(Hex(Le(target) + Le16(tx) + Le16(ty) + Le16(own.CellX) + Le16(own.CellY) + Le16(range))));
        }

        void Range(int range) => _rig.Client.Deliver(new ATTACK_RANGE(Hex(Le16(range))));

        void PickupAnswer(int result) =>
            _rig.Client.Deliver(new ITEM_PICKUP_ACK(Hex("0200" + "0100" + Le(501u) + "01" + "00" + new string('0', 32) + "00000000" + "00" + $"{result:X2}" + new string('0', 72))));

        static byte[] AttackBytes(uint id, byte action) => Hex("3704" + Le(id) + $"{action:X2}");
        static byte[] MoveBytes(int x, int y) => Hex("5F03" + Pos(x, y, 0));
        static byte[] PickUpBytes(uint id) => Hex("6203" + Le(id));

        int Count(ushort id) => _rig.Client.SentCount(id);
        byte[] Last => Sent[Sent.Count - 1].Bytes;

        // ---- FR-2: what is attacked, and how ----

        [Test, Ignore("pending T3.3")]
        public void OwnCombat_Click_SendsSingle()
        {
            Monster(MonsterA, 151, 200);
            Assert.AreEqual(AttackResult.Sent, Combat.Attack(MonsterA, false));
            Assert.AreEqual(1, Sent.Count);
            Assert.AreEqual(AttackBytes(MonsterA, Single), Sent[0].Bytes);
            Assert.AreEqual((CombatState.Attacking, MonsterA, false), (Combat.State, Combat.TargetId, Combat.Continuous));

            Assert.AreEqual(AttackResult.Sent, Combat.Attack(MonsterA, false));
            Assert.AreEqual(1, Sent.Count, "the same target and kind again sends nothing");

            Blow(Own, MonsterA);
            Assert.AreEqual(CombatState.Idle, Combat.State, "a single attack ends with its blow");
            Run(3000);
            Assert.AreEqual(1, Sent.Count);
        }

        [Test, Ignore("pending T3.3")]
        public void OwnCombat_CtrlHoldNoCtrl_SendContinuous()
        {
            Monster(MonsterA, 151, 200);
            Monster(MonsterB, 149, 200);
            Combat.Attack(MonsterA, false);
            Assert.AreEqual(AttackResult.Sent, Combat.Attack(MonsterA, true));
            Assert.AreEqual(AttackBytes(MonsterA, Continuous), Last, "single to continuous asks again");
            Assert.AreEqual((CombatState.Attacking, true), (Combat.State, Combat.Continuous));
            Blow(Own, MonsterA);
            Assert.AreEqual(CombatState.Attacking, Combat.State, "a continuous attack goes on after a blow");

            Combat.NoCtrl = true;
            Assert.AreEqual(AttackResult.Sent, Combat.Attack(MonsterB, false));
            Assert.AreEqual(AttackBytes(MonsterB, Continuous), Last, "/noctrl: a plain click is continuous");
            Assert.AreEqual((MonsterB, true), (Combat.TargetId, Combat.Continuous));
        }

        [Test, Ignore("pending T3.3")]
        public void OwnCombat_PlayerNpcOwn_SendNothing()
        {
            _rig.Client.Deliver(Stand(PlayerId, 151, 200, type: 0, gid: 150001, job: 0));
            _rig.Client.Deliver(Stand(NpcId, 149, 200, type: 6, job: 100));
            _rig.Client.Deliver(Stand(PetId, 150, 201, type: 7));
            Monster(MonsterA, 150, 199, state: 1);
            Item(ItemA, 151, 201);
            foreach (var id in new[] { PlayerId, NpcId, PetId, MonsterA, ItemA, Own, 12345u })
            {
                Assert.AreEqual(AttackResult.NotAttackable, Combat.Attack(id, false), id.ToString());
                Assert.AreEqual(AttackResult.NotAttackable, Combat.Attack(id, true), id.ToString());
            }
            Assert.AreEqual(0, Sent.Count);
            Assert.AreEqual(CombatState.Idle, Combat.State);
        }

        [Test, Ignore("pending T3.3")]
        public void OwnCombat_SittingOrDead_SendNothing()
        {
            Monster(MonsterA, 151, 200);
            _rig.Client.Deliver(Act(Own, 2));
            Assert.AreEqual(AttackResult.Sitting, Combat.Attack(MonsterA, true));
            _rig.Client.Deliver(Act(Own, 3));
            _rig.Client.Deliver(VanishPacket(Own, 1));
            Assert.AreEqual(AttackResult.Dead, Combat.Attack(MonsterA, true));
            Assert.AreEqual(0, Sent.Count);
            Assert.AreEqual(CombatState.Idle, Combat.State);

            using (var other = new Rig())
            {
                other.ToLoadingMap(OwnCharacterTests.Character());
                other.Session.BeginMap(other.Grid);
                other.Client.Deliver(Stand(MonsterA, 151, 200));
                other.Client.ClearRecords();
                Assert.AreEqual(AttackResult.NotInGame, other.Session.Combat.Attack(MonsterA, false), "before MAP_LOADED");
                Assert.AreEqual(0, other.Client.Sent.Count);
            }
        }

        [Test, Ignore("pending T3.4")]
        public void OwnCombat_GroundClick_StopsThenWalks()
        {
            Monster(MonsterA, 151, 200);
            Combat.Attack(MonsterA, true);
            _rig.Grid.Wall = (x, y) => x == 140 && y == 200;
            Assert.AreEqual(WalkRequestResult.Blocked, _rig.Session.Own.RequestWalk(140, 200));
            Assert.AreEqual((1, CombatState.Attacking), (Sent.Count, Combat.State), "a click that cannot walk changes nothing");

            Assert.AreEqual(WalkRequestResult.Sent, _rig.Session.Own.RequestWalk(155, 200));
            Assert.AreEqual(3, Sent.Count);
            Assert.AreEqual(PacketIds.STOP_ATTACK, Sent[1].Id, "the attack stops first");
            Assert.AreEqual(MoveBytes(155, 200), Sent[2].Bytes);
            Assert.AreEqual(CombatState.Idle, Combat.State);

            Monster(MonsterB, 165, 200);
            Assert.AreEqual(AttackResult.Approaching, Combat.Attack(MonsterB, true));
            _rig.Client.ClearRecords();
            Assert.AreEqual(WalkRequestResult.Sent, _rig.Session.Own.RequestWalk(145, 200));
            Assert.AreEqual(1, Sent.Count, "no attack was asked for: no stop");
            Assert.AreEqual(MoveBytes(145, 200), Sent[0].Bytes);
            Assert.AreEqual(CombatState.Idle, Combat.State);
            Run(1000);
            Assert.AreEqual(1, Sent.Count);
        }

        [Test, Ignore("pending T3.4")]
        public void OwnCombat_Cancel_SitMapChangeDeath()
        {
            Monster(MonsterA, 151, 200);
            Combat.Attack(MonsterA, true);
            Assert.IsTrue(_rig.Session.Own.Sit());
            Assert.AreEqual(CombatState.Idle, Combat.State, "a sit request");
            Run(5000);
            Assert.AreEqual(2, Sent.Count, "the attack and the sit, nothing after");

            _rig.Client.ClearRecords();
            Combat.Attack(MonsterA, true);
            _rig.Session.BeginMap(_rig.Grid);
            Assert.AreEqual(CombatState.Idle, Combat.State, "a map change");

            Monster(MonsterB, 151, 200);
            Combat.Attack(MonsterB, true);
            _rig.Client.Deliver(VanishPacket(Own, 1));
            Assert.AreEqual(CombatState.Idle, Combat.State, "own death");
            Run(5000);
            Assert.AreEqual(2, Sent.Count, "the two attacks, nothing after");
        }

        // ---- FR-3: range and approach ----

        [Test, Ignore("pending T3.3")]
        public void OwnCombat_Approach_StopsAtRange1And9()
        {
            Monster(MonsterC, 130, 200);
            _rig.Grid.Wall = (x, y) => Math.Max(Math.Abs(x - 130), Math.Abs(y - 200)) == 1;
            Assert.AreEqual(AttackResult.NoPath, Combat.Attack(MonsterC, true), "walled in");
            Assert.AreEqual((0, CombatState.Idle), (Sent.Count, Combat.State));

            Monster(MonsterA, 160, 200);
            Assert.AreEqual(AttackResult.Approaching, Combat.Attack(MonsterA, true));
            Assert.AreEqual(1, Sent.Count);
            Assert.AreEqual(MoveBytes(158, 200), Sent[0].Bytes, "range 1: two cells straight are in range");
            Assert.AreEqual((CombatState.Approaching, MonsterA), (Combat.State, Combat.TargetId));

            Range(9);
            Monster(MonsterB, 150, 215);
            Assert.AreEqual(AttackResult.Approaching, Combat.Attack(MonsterB, true));
            Assert.AreEqual(MoveBytes(150, 205), Last, "range 9: the first cell of the path from which the target is in range");
            Assert.AreEqual(0, Count(PacketIds.REQUEST_ACT));
        }

        [Test, Ignore("pending T3.3")]
        public void OwnCombat_TargetMoves_WalksAgain()
        {
            Monster(MonsterA, 160, 200);
            Combat.Attack(MonsterA, true);
            Assert.AreEqual(1, Count(PacketIds.REQUEST_MOVE));
            _rig.Client.Deliver(new UNIT_STOP_MOVE(Hex(Le(MonsterA) + Le16(160) + Le16(210))));
            Run(100);
            Assert.AreEqual(1, Count(PacketIds.REQUEST_MOVE), "at most one walk request every 200 ms");
            Run(150);
            Assert.AreEqual(2, Count(PacketIds.REQUEST_MOVE));
            Assert.AreEqual(MoveBytes(159, 209), Last);
            ServerWalks(159, 209);
            Run(1000);
            Assert.AreEqual(2, Count(PacketIds.REQUEST_MOVE), "the target stands: no new walk while the own walk goes on");
        }

        [Test, Ignore("pending T3.3")]
        public void OwnCombat_AsksWhileWalkingOnceInRange()
        {
            Monster(MonsterA, 160, 200);
            Combat.Attack(MonsterA, true);
            ServerWalks(158, 200);
            Run(300);
            var own = _rig.Get(Own);
            Assert.IsTrue(own.Walking);
            _rig.Client.Deliver(new UNIT_STOP_MOVE(Hex(Le(MonsterA) + Le16(own.CellX + 2) + Le16(200))));
            Run(50);
            Assert.IsTrue(_rig.Get(Own).Walking, "still walking");
            Assert.AreEqual(AttackBytes(MonsterA, Continuous), Last, "asked on the own server cell, while walking");
            Assert.AreEqual(CombatState.Attacking, Combat.State);
            Run(2000);
            Assert.AreEqual(1, Count(PacketIds.REQUEST_MOVE), "no walk after the request");
        }

        [Test, Ignore("pending T3.4")]
        public void OwnCombat_TooFar_ApproachesAgainAndCloser()
        {
            Range(3);
            Monster(MonsterA, 154, 200);
            Assert.AreEqual(AttackResult.Sent, Combat.Attack(MonsterA, true), "4 cells straight are 3 by the client distance");

            TooFar(MonsterA, 154, 200, 3);
            Run(50);
            Assert.AreEqual(CombatState.Approaching, Combat.State);
            Assert.AreEqual(MoveBytes(151, 200), Last, "one cell stricter: range 2");
            ServerWalks(151, 200);
            Run(300);
            Assert.AreEqual(AttackBytes(MonsterA, Continuous), Last);
            Assert.AreEqual(CombatState.Attacking, Combat.State);

            TooFar(MonsterA, 154, 200, 3);
            Run(50);
            Assert.AreEqual(MoveBytes(152, 200), Last, "again: range 1");
            ServerWalks(152, 200);
            Run(300);
            Assert.AreEqual(AttackBytes(MonsterA, Continuous), Last);

            Blow(Own, MonsterA);
            TooFar(MonsterA, 154, 200, 3);
            Run(50);
            Assert.AreEqual(AttackBytes(MonsterA, Continuous), Last, "after an own blow one cell stricter only: in range from here");
            Assert.AreEqual(2, Count(PacketIds.REQUEST_MOVE));

            Monster(MonsterB, 150, 200);
            Combat.Attack(MonsterB, false);
            TooFar(MonsterB, 150, 200, 3);
            Run(50);
            Assert.AreEqual(AttackBytes(MonsterB, Single), Last, "a single attack is asked for again too");
        }

        [Test, Ignore("pending T3.4")]
        public void OwnCombat_Silence_AsksAgainThenGivesUp()
        {
            Monster(MonsterB, 152, 200);
            Combat.Attack(MonsterB, true);
            Run(1000);
            _rig.Client.Deliver(new UNIT_STOP_MOVE(Hex(Le(MonsterB) + Le16(160) + Le16(200))));
            Run(400);
            Assert.AreEqual((CombatState.Attacking, 0), (Combat.State, Count(PacketIds.REQUEST_MOVE)), "no walk before the server is silent (D5)");
            Run(150);
            Assert.AreEqual(CombatState.Approaching, Combat.State, "silent and out of range: approach");
            Assert.AreEqual(MoveBytes(158, 200), Last);
            _rig.Client.ClearRecords();

            Monster(MonsterA, 151, 200);
            Combat.Attack(MonsterA, true);
            Run(1450);
            Assert.AreEqual(1, Count(PacketIds.REQUEST_ACT));
            Run(100);
            Assert.AreEqual(2, Count(PacketIds.REQUEST_ACT), "1,500 ms without an own blow");

            Blow(Own, MonsterA, attackMt: 1000);
            Run(2400);
            Assert.AreEqual(2, Count(PacketIds.REQUEST_ACT), "2 × 1,000 + 500 after a slow blow");
            Run(150);
            Assert.AreEqual(3, Count(PacketIds.REQUEST_ACT));
            Run(5000);
            Assert.AreEqual(5, Count(PacketIds.REQUEST_ACT), "three tries in a row");
            Assert.AreEqual(CombatState.Attacking, Combat.State);
            Run(2500);
            Assert.AreEqual(CombatState.Idle, Combat.State, "then it gives up");
            Assert.AreEqual(5, Count(PacketIds.REQUEST_ACT));
            Assert.AreEqual(1, _info.Count, string.Join("\n", _info));
        }

        [Test, Ignore("pending T3.3")]
        public void OwnCombat_TargetDies_Idle()
        {
            Monster(MonsterA, 151, 200);
            Combat.Attack(MonsterA, true);
            _rig.Client.Deliver(VanishPacket(MonsterA, 1));
            Run(50);
            Assert.AreEqual(CombatState.Idle, Combat.State, "died");

            Monster(MonsterB, 165, 200);
            Combat.Attack(MonsterB, true);
            _rig.Client.Deliver(VanishPacket(MonsterB, 0));
            Run(50);
            Assert.AreEqual(CombatState.Idle, Combat.State, "out of sight while approaching");
            int sent = Sent.Count;
            Run(3000);
            Assert.AreEqual(sent, Sent.Count);
        }

        [Test, Ignore("pending T3.4")]
        public void OwnCombat_NoAmmo_Idle()
        {
            Range(9);
            Monster(MonsterA, 155, 200);
            Combat.Attack(MonsterA, true);
            _rig.Client.Deliver(new ACTION_FAILURE(Hex("0000")));
            Assert.AreEqual(CombatState.Idle, Combat.State);
            Assert.AreEqual(1, _info.Count, string.Join("\n", _info));
            Run(5000);
            Assert.AreEqual(1, Count(PacketIds.REQUEST_ACT));
        }

        // ---- FR-10: pickup ----

        [Test, Ignore("pending T3.5")]
        public void OwnCombat_PickUp_WalksWithinTwoThenAsks()
        {
            Monster(MonsterA, 151, 200);
            Assert.IsFalse(Combat.PickUp(MonsterA), "not an item");
            Assert.IsFalse(Combat.PickUp(12345u), "unknown");

            Item(ItemA, 152, 202);
            Assert.IsTrue(Combat.PickUp(ItemA));
            Assert.AreEqual(PickUpBytes(ItemA), Last, "within two cells: at once");
            Assert.AreEqual(CombatState.Idle, Combat.State);

            Item(ItemB, 160, 200);
            Assert.IsTrue(Combat.PickUp(ItemB));
            Assert.AreEqual(MoveBytes(158, 200), Last);
            Assert.AreEqual(CombatState.Fetching, Combat.State);
            ServerWalks(158, 200);
            for (int i = 0; i < 40 && _rig.Get(Own).Walking; i++)
            {
                Run(50);
                Assert.AreEqual(1, Count(PacketIds.PICKUP_ITEM), "not before the walk ends: the server keeps nothing");
            }
            Run(50);
            Assert.AreEqual(PickUpBytes(ItemB), Last);
            Assert.AreEqual(CombatState.Idle, Combat.State);
        }

        [Test, Ignore("pending T3.5")]
        public void OwnCombat_PickUp_FailureRetriedOnce()
        {
            Item(ItemA, 151, 200);
            Combat.PickUp(ItemA);
            PickupAnswer(6);
            Run(350);
            Assert.AreEqual(1, Count(PacketIds.PICKUP_ITEM));
            Run(100);
            Assert.AreEqual(2, Count(PacketIds.PICKUP_ITEM), "once more after 400 ms");
            PickupAnswer(6);
            Run(2000);
            Assert.AreEqual(2, Count(PacketIds.PICKUP_ITEM), "only once");

            Item(ItemB, 149, 200);
            Combat.PickUp(ItemB);
            PickupAnswer(6);
            _rig.Client.Deliver(new ITEM_DISAPPEAR(Hex(Le(ItemB))));
            Run(1000);
            Assert.AreEqual(3, Count(PacketIds.PICKUP_ITEM), "the item is gone: no retry");

            Item(ItemC, 150, 201);
            Combat.PickUp(ItemC);
            PickupAnswer(0);
            Run(1000);
            Assert.AreEqual(4, Count(PacketIds.PICKUP_ITEM), "success needs nothing");
        }

        [Test, Ignore("pending T3.5")]
        public void OwnCombat_PickUp_FailureReportedOnce()
        {
            Item(ItemA, 151, 200);
            Combat.PickUp(ItemA);
            PickupAnswer(6);
            Assert.AreEqual(0, _info.Count, "the first failure is retried silently");
            Run(450);
            PickupAnswer(4);
            Assert.AreEqual(1, _info.Count);
            StringAssert.Contains("4", _info[0], "the code as received");
            PickupAnswer(4);
            Run(1000);
            Assert.AreEqual(1, _info.Count, "an answer with nothing asked for is not reported");
        }
    }
}
