using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Uninarok.Map;
using Uninarok.Networking.Packets;
using Uninarok.Sprites;
using Uninarok.World.View;
using MouseCursor = Uninarok.World.View.MouseCursor;

namespace Uninarok.World.Tests
{
    // SPEC-0010 FR-9, FR-13, FR-19 (PLAN-0010 4.11, T7.3): every action through simulated devices, to the own character of a session.
    public class OwnInputTests : InputTestFixture
    {
        WorldSessionTests.Rig _rig;
        GameObject _go;
        OwnInput _input;
        Mouse _mouse;
        Keyboard _keyboard;
        double _now;
        int _x = 155, _y = 200;
        uint _unit;
        bool _over = true;

        public override void Setup()
        {
            base.Setup();
            _mouse = InputSystem.AddDevice<Mouse>();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _rig = new WorldSessionTests.Rig();
            _rig.ToInGame(OwnCharacterTests.Character());
            _go = new GameObject("Input");
            _input = _go.AddComponent<OwnInput>();
            _input.Actions = Object.Instantiate(AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Scripts/World/View/WorldInputActions.inputactions"));
            _input.Own = () => _rig.Session.Own;
            _input.Combat = () => _rig.Session.Combat;
            _input.Bind();
            _now = 100.0;
            (_x, _y, _over, _unit) = (155, 200, true, 0u);
        }

        public override void TearDown()
        {
            var actions = _input.Actions;
            _input.Unbind();
            Object.DestroyImmediate(_go);
            Object.DestroyImmediate(actions);
            _rig.Dispose();
            base.TearDown();
        }

        void Frame(double seconds = 0.016)
        {
            _now += seconds;
            _input.Step(_now, _over, _x, _y, _unit);
        }

        int Moves => _rig.Client.SentCount(PacketIds.REQUEST_MOVE);

        [Test]
        public void LeftClick_AsksToWalk_Once()
        {
            Press(_mouse.leftButton);
            Frame();
            Assert.AreEqual(1, Moves);
            Assert.AreEqual(WorldSessionTests.Hex("5F03" + WorldSessionTests.Pos(155, 200, 0)), _rig.Client.Sent[0].Bytes);
            Release(_mouse.leftButton);
            for (int i = 0; i < 100; i++) Frame();
            Assert.AreEqual(1, Moves);
        }

        [Test]
        public void Holding_AsksAgainEvery500ms_AndWhenTheCellChanges()
        {
            Press(_mouse.leftButton);
            Frame();
            InputSystem.Update();
            for (int i = 0; i < 49; i++) Frame(0.01);
            Assert.AreEqual(1, Moves, "490 ms");
            Frame(0.011);
            Assert.AreEqual(2, Moves, "500 ms");
            _x = 156;
            Frame();
            Assert.AreEqual(3, Moves, "another cell at once");
            Frame(0.4);
            Assert.AreEqual(3, Moves);
            _over = false;
            Frame(2);
            Assert.AreEqual(3, Moves, "no cell under the mouse");
            _over = true;
            Frame();
            Assert.AreEqual(4, Moves);
            Release(_mouse.leftButton);
            InputSystem.Update();
            Frame(5);
            Assert.AreEqual(4, Moves);
        }

        // D36: the server starts a repeated walk over on its next cell, which is felt.
        [Test]
        public void Holding_OnACellItWalksTo_AsksNoMore()
        {
            Press(_mouse.leftButton);
            Frame();
            InputSystem.Update();
            _rig.Client.Deliver(WorldSessionTests.OwnMove(150, 200, 155, 200, _rig.Clock.Now));
            Assert.IsTrue(_rig.Session.Own.IsWalkingTo(155, 200));
            Assert.IsFalse(_rig.Session.Own.IsWalkingTo(154, 200));
            Frame(0.6);
            Assert.AreEqual(1, Moves, "on its way there");
            _x = 157;
            Frame();
            Assert.AreEqual(2, Moves, "another cell at once");
            Frame(0.6);
            Assert.AreEqual(3, Moves, "not walking there yet: again");
        }

        [Test]
        public void ShiftClick_Turns_AndDoesNotWalk()
        {
            Press(_keyboard.leftShiftKey);
            Press(_mouse.leftButton);
            (_x, _y) = (155, 200);
            Frame();
            InputSystem.Update();
            Frame(1);
            Assert.AreEqual((0, 1), (Moves, _rig.Client.SentCount(PacketIds.CHANGE_DIRECTION)));
            Release(_keyboard.leftShiftKey);
            InputSystem.Update();
            Frame(1);
            Assert.AreEqual(0, Moves, "letting shift go does not start a walk");
        }

        [Test]
        public void Insert_TogglesSitting()
        {
            Press(_keyboard.insertKey);
            Frame();
            Assert.AreEqual(1, _rig.Client.SentCount(PacketIds.REQUEST_ACT));
            InputSystem.Update();
            Frame();
            Assert.AreEqual(1, _rig.Client.SentCount(PacketIds.REQUEST_ACT), "once per press");
        }

        [Test]
        public void WithoutFocus_NothingIsSent()
        {
            _input.InputEnabled = false;
            Press(_mouse.leftButton);
            Press(_keyboard.insertKey);
            Frame();
            Assert.AreEqual(0, _rig.Client.Sent.Count);
        }

        [Test]
        public void AClickBesideTheMap_SendsNothing()
        {
            _over = false;
            Press(_mouse.leftButton);
            Frame();
            InputSystem.Update();
            _over = true;
            Frame(1);
            Assert.AreEqual(0, Moves, "nor when the held button comes back over the ground");
        }

        [Test]
        public void ARefusedClick_SendsNothing()
        {
            _rig.Grid.Wall = (x, y) => x == 155;
            Press(_mouse.leftButton);
            Frame();
            Assert.AreEqual(0, _rig.Client.Sent.Count);
        }

        // FR-11 as the owner corrected it (2026-11-22): a walkable cell is marked, over another the mouse cursor says it.
        [Test]
        public void TheCursor_MarksWalkableCells_AndTheMouseSaysTheOthers()
        {
            const int n = 200;
            var map = ScriptableObject.CreateInstance<MapData>();
            map.Assign("flat", n, n * 2, 0.5f, Vector2.zero, 1f, false, new float[n * n * 2 * 4], new byte[n * n * 2], new byte[n * n * 2], new byte[n * n * 2]);
            var cursor = _go.AddComponent<CellCursor>();
            cursor.Map = map;
            _input.Cursor = cursor;
            var mouse = _go.AddComponent<MouseCursor>();
            _input.Mouse = mouse;
            _rig.Grid.Wall = (x, y) => x == 156;
            Frame();
            Assert.AreEqual((true, 155, 200, MouseCursorKind.Default), (cursor.Visible, cursor.CellX, cursor.CellY, mouse.Kind));
            _x = 156;
            Frame();
            Assert.AreEqual((false, MouseCursorKind.NoWalk), (cursor.Visible, mouse.Kind), "a wall");
            (_x, _y) = (150, 230);
            Frame();
            Assert.AreEqual((true, MouseCursorKind.Default), (cursor.Visible, mouse.Kind), "walkable, though too far for one walk");
            _x = 156;
            Frame();
            _over = false;
            Frame();
            Assert.AreEqual((false, MouseCursorKind.Default), (cursor.Visible, mouse.Kind), "over no cell");
            (_x, _over) = (156, true);
            Frame();
            _input.InputEnabled = false;
            Frame();
            Assert.AreEqual((false, MouseCursorKind.Default), (cursor.Visible, mouse.Kind), "typing");
            Object.DestroyImmediate(map);
        }

        [Test, Category("NeedsData")]
        public void TheMouseCursor_PlaysTheClientsCursors()
        {
            var animation = AssetDatabase.LoadAssetAtPath<SpriteAnimation>("Assets/Art/Imported/sprite/cursors.asset");
            if (animation == null) Assert.Ignore("no converted cursors");
            var mouse = _go.AddComponent<MouseCursor>();
            mouse.Animation = animation;
            var shown = new System.Collections.Generic.List<(Texture2D texture, Vector2 hotspot, CursorMode mode)>();
            mouse.Apply = (texture, hotspot, mode) => shown.Add((texture, hotspot, mode));
            mouse.Step(10.0);
            mouse.Step(10.01);
            Assert.AreEqual(1, shown.Count, "only when the frame changes");
            Assert.AreEqual((32, 32, Vector2.zero, CursorMode.Auto), (shown[0].texture.width, shown[0].texture.height, shown[0].hotspot, shown[0].mode), "Unity stretches another size to 32 x 32");
            int opaque = 0;
            foreach (var p in shown[0].texture.GetPixels32()) if (p.a > 0) opaque++;
            Assert.Greater(opaque, 100);
            for (double t = 10.0; t < 11.0; t += 0.01) mouse.Step(t);
            Assert.AreEqual(10, shown.Count, "100 ms each, half the ACT's speed");
            shown.RemoveRange(1, shown.Count - 1);

            mouse.Kind = MouseCursorKind.NoWalk;
            for (double t = 20.0; t < 22.2; t += 0.01) mouse.Step(t);
            Assert.AreEqual((43, 43, new Vector2(13f, 6f), CursorMode.ForceSoftware), (shown[1].texture.width, shown[1].texture.height, shown[1].hotspot, shown[1].mode), "the arrow's tip stays where it was");
            int distinct = new System.Collections.Generic.HashSet<Texture2D>(shown.ConvertAll(s => s.texture)).Count;
            Assert.AreEqual(1 + 22, distinct, "22 frames in 2.2 s");
            mouse.Apply = (_, _, _) => { };
        }

        [Test]
        public void TheCursor_LiesOnTheCellsSlope()
        {
            var map = ScriptableObject.CreateInstance<MapData>();
            map.Assign("slope", 2, 2, 0.5f, new Vector2(10f, 20f), -1f, false, new[] { 0f, 1f, 2f, 3f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f },
                       new byte[4], new byte[4], new byte[4]);
            var cursor = _go.AddComponent<CellCursor>();
            cursor.Map = map;
            cursor.Show(0, 0);
            var v = cursor.Mesh.vertices;
            Assert.AreEqual(new Vector3(10f, 0.01f, 20f), v[0]);
            Assert.AreEqual(new Vector3(10.5f, 1.01f, 20f), v[1]);
            Assert.AreEqual(new Vector3(10f, 2.01f, 19.5f), v[2]);
            Assert.AreEqual(new Vector3(10.5f, 3.01f, 19.5f), v[3]);
            var t = cursor.Mesh.triangles;
            for (int i = 0; i < 6; i += 3)
                Assert.Greater(Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]).y, 0f, "both triangles face up");
            cursor.Show(5, 5);
            Assert.IsFalse(cursor.Visible, "outside the map");
            Object.DestroyImmediate(map);
        }

        // ---- SPEC-0011 T7.2 (FR-2, FR-8, FR-10; PLAN-0011 4.10): monsters, players, NPCs and items under the pointer ----

        const uint MonsterA = 0x068EB5EC, PlayerId = 2000003, NpcId = 110000123, ItemA = 9001;
        const byte Single = 0, Continuous = 7;

        uint OwnId => _rig.Flow.Context.AccountId;
        int Attacks => _rig.Client.SentCount(PacketIds.REQUEST_ACT);
        byte LastAttack => _rig.Client.Sent.FindLast(p => p.Bytes[0] == 0x37 && p.Bytes[1] == 0x04).Bytes[6];

        void Monster() => _rig.Client.Deliver(WorldSessionTests.Stand(MonsterA, 151, 200));

        void Click()
        {
            Press(_mouse.leftButton);
            Frame();
            Release(_mouse.leftButton);
            InputSystem.Update();
            Frame();
        }

        [Test]
        public void OwnInput_ClickOnAMonster_AttacksOnce_AndNeverWalks()
        {
            Monster();
            _unit = MonsterA;
            Click();
            Assert.AreEqual((1, Single, 0), (Attacks, LastAttack, Moves));
            Frame(1);
            Assert.AreEqual((1, 0), (Attacks, Moves));
        }

        [Test]
        public void OwnInput_CtrlClick_AttacksWithoutEnd()
        {
            Monster();
            _unit = MonsterA;
            Press(_keyboard.leftCtrlKey);
            Click();
            Assert.AreEqual((1, Continuous, 0), (Attacks, LastAttack, Moves));
        }

        [Test]
        public void OwnInput_Hold300_AttacksWithoutEnd_ReleaseDoesNotStop()
        {
            Monster();
            _unit = MonsterA;
            Press(_mouse.leftButton);
            Frame();
            InputSystem.Update();
            Frame(0.2);
            Assert.AreEqual((1, Single), (Attacks, LastAttack), "200 ms: still the single attack");
            Frame(0.11);
            Assert.AreEqual((2, Continuous), (Attacks, LastAttack), "300 ms held: continuous");
            Frame(1);
            Assert.AreEqual(2, Attacks, "once");
            _unit = 0;
            Frame(1);
            Release(_mouse.leftButton);
            InputSystem.Update();
            Frame(1);
            Assert.AreEqual((0, 0), (Moves, _rig.Client.SentCount(PacketIds.STOP_ATTACK)), "a hold that began on a monster never walks; letting go does not stop");
            Assert.IsTrue(_rig.Session.Combat.Continuous);
        }

        // ---- Phase 12 (owner, 2026-11-24): holding the button on a monster, as it is played ----

        void Blow(uint target) =>
            _rig.Client.Deliver(new Uninarok.Networking.Packets.In.Map.DAMAGE(WorldSessionTests.Hex(WorldSessionTests.Le(OwnId) + WorldSessionTests.Le(target)
                + WorldSessionTests.Le(_rig.Clock.Now) + WorldSessionTests.Le(500u) + WorldSessionTests.Le(300u) + WorldSessionTests.Le(10u) + "00"
                + WorldSessionTests.Le16(1) + "00" + WorldSessionTests.Le(0u))));

        // FR-2, D38: in range the single blow comes back before HoldAttackMs and ends the single attack; the hold still turns it
        // into a continuous one.
        [Test]
        public void OwnInput_Hold_InRange_BlowBeforeHoldAttackMs_StillContinuous()
        {
            Monster();
            _unit = MonsterA;
            Press(_mouse.leftButton);
            Frame();
            InputSystem.Update();
            Assert.AreEqual((1, Single), (Attacks, LastAttack));
            Frame(0.1);
            Blow(MonsterA);
            Assert.AreEqual(CombatState.Idle, _rig.Session.Combat.State, "the single blow ended the single attack");
            Frame(0.1);
            Assert.AreEqual(1, Attacks, "200 ms: not yet");
            Frame(0.11);
            Assert.AreEqual((2, Continuous), (Attacks, LastAttack), "300 ms held: continuous");
            Assert.AreEqual((CombatState.Attacking, MonsterA, true), (_rig.Session.Combat.State, _rig.Session.Combat.TargetId, _rig.Session.Combat.Continuous));
            Blow(MonsterA);
            Frame(1);
            Release(_mouse.leftButton);
            InputSystem.Update();
            Frame(1);
            Assert.AreEqual((2, 0, 0), (Attacks, Moves, _rig.Client.SentCount(PacketIds.STOP_ATTACK)), "once; letting go does not stop");
            Assert.AreEqual((CombatState.Attacking, true), (_rig.Session.Combat.State, _rig.Session.Combat.Continuous));
        }

        // FR-2, FR-3: a monster 8 cells away; the approach is under way when the hold reaches HoldAttackMs.
        [Test]
        public void OwnInput_Hold_Approaching_BecomesContinuous()
        {
            _rig.Client.Deliver(WorldSessionTests.Stand(MonsterA, 158, 200));
            _unit = MonsterA;
            Press(_mouse.leftButton);
            Frame();
            InputSystem.Update();
            Assert.AreEqual((0, CombatState.Approaching), (Attacks, _rig.Session.Combat.State));
            Frame(0.31);
            Assert.AreEqual((CombatState.Approaching, MonsterA, true), (_rig.Session.Combat.State, _rig.Session.Combat.TargetId, _rig.Session.Combat.Continuous));
            Release(_mouse.leftButton);
            InputSystem.Update();
            Frame(1);
            Assert.IsTrue(_rig.Session.Combat.Continuous, "letting go does not stop");
        }

        // D38: something else during the hold (here: sitting) is not undone by the hold.
        [Test]
        public void OwnInput_Hold_SitDuringHold_DoesNotAttackAgain()
        {
            Monster();
            _unit = MonsterA;
            Press(_mouse.leftButton);
            Frame();
            InputSystem.Update();
            Frame(0.1);
            Blow(MonsterA);
            _rig.Session.Combat.Cancel();
            Frame(0.3);
            Assert.AreEqual(1, Attacks, "a cancelled attack is not started again by the hold");
        }

        [Test]
        public void OwnInput_ShiftOnAMonster_Turns()
        {
            Monster();
            _unit = MonsterA;
            Press(_keyboard.leftShiftKey);
            Click();
            Assert.AreEqual((0, 0, 1), (Attacks, Moves, _rig.Client.SentCount(PacketIds.CHANGE_DIRECTION)));
        }

        [Test]
        public void OwnInput_PlayerOrNpc_WalksAsOverTheGround()
        {
            _rig.Client.Deliver(WorldSessionTests.Stand(PlayerId, 155, 200, type: 0, gid: 150001, job: 0));
            _rig.Client.Deliver(WorldSessionTests.Stand(NpcId, 155, 201, type: 6, job: 100));
            _unit = PlayerId;
            Click();
            Assert.AreEqual((0, 1), (Attacks, Moves), "a player");
            _unit = NpcId;
            _y = 201;
            Click();
            Assert.AreEqual((0, 2), (Attacks, Moves), "an NPC");
        }

        [Test]
        public void OwnInput_Item_PicksUp()
        {
            _rig.Model.AddItem(ItemA, 501, true, 151, 200, 6, 6, 1);
            _unit = ItemA;
            Click();
            Assert.AreEqual((1, 0), (_rig.Client.SentCount(PacketIds.PICKUP_ITEM), Moves));
        }

        [Test]
        public void OwnInput_GroundWhileAttacking_StopsThenWalks()
        {
            Monster();
            _unit = MonsterA;
            Press(_keyboard.leftCtrlKey);
            Click();
            Release(_keyboard.leftCtrlKey);
            InputSystem.Update();
            _unit = 0;
            Click();
            var sent = _rig.Client.Sent;
            Assert.AreEqual(PacketIds.STOP_ATTACK, (ushort)(sent[sent.Count - 2].Bytes[0] | sent[sent.Count - 2].Bytes[1] << 8));
            Assert.AreEqual(1, Moves);
            Assert.AreEqual(CombatState.Idle, _rig.Session.Combat.State);
        }

        [Test]
        public void OwnInput_Dead_SendsNothing()
        {
            Monster();
            _rig.Client.Deliver(WorldSessionTests.VanishPacket(OwnId, 1));
            _unit = MonsterA;
            Click();
            _unit = 0;
            Click();
            Press(_keyboard.insertKey);
            Frame();
            Assert.AreEqual(0, _rig.Client.Sent.Count);
        }

        [Test]
        public void OwnInput_OverAMonster_AttackCursorAndNoCell()
        {
            const int n = 200;
            var map = ScriptableObject.CreateInstance<MapData>();
            map.Assign("flat", n, n * 2, 0.5f, Vector2.zero, 1f, false, new float[n * n * 2 * 4], new byte[n * n * 2], new byte[n * n * 2], new byte[n * n * 2]);
            var cursor = _go.AddComponent<CellCursor>();
            cursor.Map = map;
            _input.Cursor = cursor;
            var mouse = _go.AddComponent<MouseCursor>();
            _input.Mouse = mouse;
            Monster();
            _rig.Client.Deliver(WorldSessionTests.Stand(PlayerId, 155, 201, type: 0, gid: 150001, job: 0));
            _unit = MonsterA;
            Frame();
            Assert.AreEqual((false, MouseCursorKind.Attack), (cursor.Visible, mouse.Kind), "a monster");
            _unit = PlayerId;
            Frame();
            Assert.AreEqual((true, MouseCursorKind.Default), (cursor.Visible, mouse.Kind), "a player: as the ground");
            _unit = 0;
            Frame();
            Assert.AreEqual((true, MouseCursorKind.Default), (cursor.Visible, mouse.Kind), "the ground");
            Object.DestroyImmediate(map);
        }
    }
}
