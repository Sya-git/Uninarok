using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Uninarok.Map;
using Uninarok.Networking.Client;
using Uninarok.Networking.Core.Transport;
using Uninarok.Networking.Flow;
using Uninarok.Networking.Packets;
using Uninarok.Networking.Packets.In.Map;
using Uninarok.Networking.Tests;
using Uninarok.World.View;

namespace Uninarok.World.PlayTests
{
    // SPEC-0010 FR-1, FR-12, FR-15 (PLAN-0010 4.7): map entry with the simulated connection and the real map scenes.
    public class WorldBootstrapPlayTests
    {
        internal sealed class Rig : System.IDisposable
        {
            public readonly FakeNetworkClient Client = new FakeNetworkClient();
            public readonly FakeClock Local = new FakeClock { NowMs = 1_000 };
            public readonly NetworkFlow Flow;
            public readonly WorldSettings Settings = ScriptableObject.CreateInstance<WorldSettings>();
            public readonly WorldBootstrap World;

            public Rig()
            {
                Flow = new NetworkFlow(Client, Local, new NetworkSettings(), new ServerEndpoint("127.0.0.1", 6900, default, 0, 0, 0));
                var go = new GameObject("World");
                go.SetActive(false);
                World = go.AddComponent<WorldBootstrap>();
                World.Settings = Settings;
                World.Connect(Client, Flow, Local);
                go.SetActive(true);
            }

            static byte[] Character()
            {
                var c = new Uninarok.Networking.Data.CharInfo
                {
                    CharId = 140593, Hp = 40, MaxHp = 40, DefaultWalkSpeed = 150, Class = 0, Hair = 1, Sex = Uninarok.Networking.Data.AccountSex.SEX_MALE,
                };
                var b = new byte[Uninarok.Networking.Data.CharInfo.WireSize];
                var w = new Uninarok.Networking.Core.Wire.PacketWriter(b);
                c.Write(ref w);
                return b;
            }

            public uint OwnId => Flow.Context.AccountId;

            /// <summary>Login to the map server, which accepts the entry on 150,200 facing south.</summary>
            public void Enter(string map)
            {
                Flow.StartLogin("u", "p");
                Client.RaiseConnected(ConnectionRole.Login);
                Client.Deliver(TestPackets.AcceptLogin(TestPackets.ServerEntry(new byte[] { 10, 0, 0, 1 }, 6121, "First")));
                Flow.SelectServer(0);
                Client.RaiseConnected(ConnectionRole.Character);
                Client.Deliver(TestPackets.CharacterData(Character()));
                Flow.SelectCharacter(0);
                Client.Deliver(TestPackets.MapAccepted(140593, map, new byte[] { 10, 0, 0, 2 }, 6122));
                Client.RaiseConnected(ConnectionRole.Map);
                Client.ClearRecords();
                Client.Deliver(TestPackets.AuthenticationAccepted());
            }

            public void ChangeMap(string map, int x, int y)
            {
                var b = new byte[MAP_CHANGE.Size - 2];
                System.Text.Encoding.ASCII.GetBytes(map).CopyTo(b, 0);
                b[16] = (byte)x;
                b[17] = (byte)(x >> 8);
                b[18] = (byte)y;
                b[19] = (byte)(y >> 8);
                Client.Deliver(new MAP_CHANGE(b));
            }

            public IEnumerator WaitInGame()
            {
                for (int i = 0; i < 1800 && Flow.State == FlowState.LoadingMap; i++) yield return null;
                Assert.AreEqual(FlowState.InGame, Flow.State);
            }

            public Entity Own
            {
                get
                {
                    Assert.IsTrue(World.Session.Model.TryGetSlot(OwnId, out int slot));
                    return World.Session.Model[slot];
                }
            }

            public void Dispose()
            {
                if (World != null) Object.Destroy(World.gameObject);
                Flow.Dispose();
                Object.Destroy(Settings);
            }
        }

        internal static void NeedsMap(string name)
        {
            var group = AddressableAssetSettingsDefaultObject.Settings != null ? AddressableAssetSettingsDefaultObject.Settings.FindGroup("maps") : null;
            if (group != null)
                foreach (var e in group.entries)
                    if (e.address == MapSceneKeys.Scene(name)) return;
            Assert.Ignore($"map {name} is not registered (Tools/Ragnarok/Register Map Scenes)");
        }

        static bool IsLoaded(string map)
        {
            var scene = SceneManager.GetSceneByName(map);
            return scene.IsValid() && scene.isLoaded;
        }

        Rig _rig;

        [SetUp]
        public void SetUp() => _rig = new Rig();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _rig.Dispose();
            for (int i = 0; i < 300 && SceneManager.sceneCount > 1; i++) yield return null;
        }

        [UnityTest]
        public IEnumerator MissingScene_OneWarning_TheSessionIsKept_AndTheMapIsReportedLoaded()
        {
            LogAssert.Expect(LogType.Warning, new Regex("nowhere.*no converted scene"));
            int warnings = 0;
            void Count(string text, string stack, LogType type)
            {
                if (type == LogType.Warning) warnings++;
            }
            Application.logMessageReceived += Count;
            _rig.Enter("nowhere.gat");
            yield return _rig.WaitInGame();
            Assert.AreEqual(1, _rig.Client.SentCount(PacketIds.MAP_LOADED));
            Assert.AreEqual(0, _rig.Client.DisconnectCalls);
            Assert.IsNull(_rig.World.Map);
            Assert.AreEqual((150, 200), (_rig.Own.CellX, _rig.Own.CellY), "the world is tracked");
            Assert.AreEqual(WalkRequestResult.NoPath, _rig.World.Session.Own.RequestWalk(151, 200), "nothing is known to be walkable");
            for (int i = 0; i < 30; i++) yield return null;
            Application.logMessageReceived -= Count;
            Assert.AreEqual(1, warnings, "one warning, not one per frame");
        }

        [UnityTest, Category("NeedsData")]
        public IEnumerator Entry_LoadsTheMap_ThenReportsIt_AndAMapChangeSwapsTheScene()
        {
            NeedsMap("prt_fild08");
            _rig.Enter("prt_fild08.gat");
            Assert.AreEqual(0, _rig.Client.SentCount(PacketIds.MAP_LOADED), "not before the scene is there");
            Assert.AreEqual(FlowState.LoadingMap, _rig.Flow.State);
            yield return _rig.WaitInGame();
            Assert.IsTrue(IsLoaded("prt_fild08"));
            Assert.AreEqual("prt_fild08", _rig.World.Map.MapName);
            Assert.AreEqual("prt_fild08", SceneManager.GetActiveScene().name, "its lighting settings are used");
            Assert.AreEqual((150, 200), (_rig.Own.CellX, _rig.Own.CellY));

            // the same map again: the scene stays
            _rig.Client.ClearRecords();
            _rig.ChangeMap("prt_fild08.gat", 170, 300);
            yield return _rig.WaitInGame();
            Assert.AreEqual((170, 300), (_rig.Own.CellX, _rig.Own.CellY));
            Assert.AreEqual(1, _rig.Client.SentCount(PacketIds.MAP_LOADED));

            LogAssert.Expect(LogType.Warning, new Regex("nowhere.*no converted scene"));
            _rig.ChangeMap("nowhere.gat", 10, 11);
            yield return _rig.WaitInGame();
            for (int i = 0; i < 300 && IsLoaded("prt_fild08"); i++) yield return null;
            Assert.IsFalse(IsLoaded("prt_fild08"), "unloaded");
            Assert.AreEqual((10, 11), (_rig.Own.CellX, _rig.Own.CellY));
        }

        [UnityTest, Category("NeedsData")]
        public IEnumerator AMapChangeWhileLoading_EndsOnTheNewMap()
        {
            NeedsMap("prt_fild08");
            LogAssert.Expect(LogType.Warning, new Regex("nowhere.*no converted scene"));
            _rig.Enter("prt_fild08.gat");
            yield return null;
            _rig.ChangeMap("nowhere.gat", 10, 11);
            yield return _rig.WaitInGame();
            Assert.AreEqual(1, _rig.Client.SentCount(PacketIds.MAP_LOADED));
            Assert.AreEqual((10, 11), (_rig.Own.CellX, _rig.Own.CellY));
            for (int i = 0; i < 600 && SceneManager.sceneCount > 1; i++) yield return null;
            Assert.IsFalse(IsLoaded("prt_fild08"));
        }

        [UnityTest, Category("NeedsData")]
        public IEnumerator ADisconnect_LeavesNothing()
        {
            NeedsMap("prt_fild08");
            _rig.Enter("prt_fild08.gat");
            yield return _rig.WaitInGame();
            _rig.Client.RaiseDisconnected(new CloseInfo(CloseReason.RemoteClosed, "gone"), ConnectionRole.Map);
            Assert.IsNull(_rig.World.Session.Model);
            Assert.IsNull(_rig.World.Map);
            for (int i = 0; i < 300 && IsLoaded("prt_fild08"); i++) yield return null;
            Assert.IsFalse(IsLoaded("prt_fild08"));
        }
    }
}
