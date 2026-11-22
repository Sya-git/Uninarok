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
            _input.Bind();
            _now = 100.0;
            (_x, _y, _over) = (155, 200, true);
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
            _input.Step(_now, _over, _x, _y);
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
            var shown = new System.Collections.Generic.List<(Texture2D texture, Vector2 hotspot)>();
            mouse.Apply = (texture, hotspot) => shown.Add((texture, hotspot));
            mouse.Step(10.0);
            mouse.Step(10.01);
            Assert.AreEqual(1, shown.Count, "only when the frame changes");
            Assert.AreEqual((new Vector2Int(22, 32), Vector2.zero), (new Vector2Int(shown[0].texture.width, shown[0].texture.height), shown[0].hotspot));
            int opaque = 0;
            foreach (var p in shown[0].texture.GetPixels32()) if (p.a > 0) opaque++;
            Assert.Greater(opaque, 100);

            mouse.Kind = MouseCursorKind.NoWalk;
            for (double t = 20.0; t < 22.2; t += 0.01) mouse.Step(t);
            Assert.AreEqual(new Vector2(13f, 6f), shown[1].hotspot, "the arrow's tip stays where it was");
            int distinct = new System.Collections.Generic.HashSet<Texture2D>(shown.ConvertAll(s => s.texture)).Count;
            Assert.AreEqual(1 + 22, distinct, "22 frames in 2.2 s");
            mouse.Apply = (_, _) => { };
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
    }
}
