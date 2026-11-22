using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using Uninarok.Networking.Client;
using Uninarok.Networking.Core.Transport;
using Uninarok.Networking.Data;
using Uninarok.Networking.Dev;
using Uninarok.Networking.Flow;
using Uninarok.Networking.Packets;
using Uninarok.Networking.Packets.In.Character;

namespace Uninarok.Networking.Tests
{
    // SPEC-0001 FR-11, FR-15; plan 7.1. The driver runs against the REAL flow over a FakeNetworkClient.
    public class DevAutoLoginTests
    {
        private FakeNetworkClient _client;
        private FakeClock _clock;
        private NetworkFlow _flow;
        private DevAutoLoginDriver _driver;
        private DevCredentials? _credentials;

        private static readonly ServerEndpoint LoginServer = new ServerEndpoint("127.0.0.1", 6900, default, 0, 0, 0);

        private static DevAutoLoginSettings Settings(int serverIndex = 0, byte slot = 0, int attempts = 1, string name = "DevHero",
                                                     byte createSlot = 2, ushort hairColor = 3, ushort hairStyle = 7, uint job = 4001, AccountSex sex = AccountSex.SEX_FEMALE, bool withoutWorld = false)
            => new DevAutoLoginSettings
            {
                ServerIndex = serverIndex,
                CharacterSlot = slot,
                MaxLoginAttempts = attempts,
                CompleteMapLoadWithoutWorld = withoutWorld,
                Draft = new CharacterDraft(new FixedString32Bytes(name), createSlot, hairColor, hairStyle, job, sex),
            };

        private void Make(DevAutoLoginSettings settings, DevCredentials? credentials = null)
        {
            _credentials = credentials ?? new DevCredentials("user", "pass");
            _client = new FakeNetworkClient();
            _clock = new FakeClock { NowMs = 1000 };
            _flow = new NetworkFlow(_client, _clock, new NetworkSettings(), LoginServer);
            _driver = new DevAutoLoginDriver(_flow, settings, () => _credentials);
        }

        [TearDown]
        public void TearDown()
        {
            _driver?.Dispose();
            _flow?.Dispose();
        }

        private void LoginAccepted(params ServerEndpoint[] _)
        {
            _client.RaiseConnected(ConnectionRole.Login);
            _client.Deliver(TestPackets.AcceptLogin(
                TestPackets.ServerEntry(new byte[] { 10, 0, 0, 1 }, 6121, "First"),
                TestPackets.ServerEntry(new byte[] { 10, 0, 0, 2 }, 40000, "Second")));
        }

        private void CharServerReady(bool withCharacter)
        {
            _client.RaiseConnected(ConnectionRole.Character);
            if (withCharacter) _client.Deliver(TestPackets.CharacterData(TestPackets.CharBytes(1, "One", 0)));
            else _client.Deliver(TestPackets.CharacterData());
        }

        private static PINCODE_LOGINSTATE Pin(short state) => new PINCODE_LOGINSTATE(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, (byte)state, (byte)(state >> 8) });

        // ---- login ----

        [Test]
        public void Start_LogsInOnce_WithTheCredentials()
        {
            Make(Settings());
            _driver.Start();
            _driver.Start();                                         // a second Start does nothing

            Assert.AreEqual(FlowState.LoggingIn, _flow.State);
            Assert.AreEqual(1, _client.Connects.Count);
            _client.RaiseConnected(ConnectionRole.Login);
            Assert.AreEqual(1, _client.SentCount(PacketIds.REQUEST_LOGIN));
            Assert.AreEqual((byte)'u', _client.Sent[0].Bytes[6]);
            Assert.AreEqual((byte)'p', _client.Sent[0].Bytes[30]);
        }

        [Test]
        public void WithoutCredentials_LogsAWarning_AndDoesNothing()
        {
            Make(Settings());
            _credentials = null;
            LogAssert.Expect(LogType.Warning, new Regex("no credentials"));
            _driver.Start();
            Assert.AreEqual(FlowState.Idle, _flow.State);
            Assert.AreEqual(0, _client.Connects.Count);
        }

        [Test]
        public void EmptyUserName_CountsAsNoCredentials()
        {
            Make(Settings(), new DevCredentials("", "x"));
            LogAssert.Expect(LogType.Warning, new Regex("no credentials"));
            _driver.Start();
            Assert.AreEqual(FlowState.Idle, _flow.State);
        }

        // ---- server and character ----

        [Test]
        public void ServerSelection_PicksTheConfiguredServer()
        {
            Make(Settings(serverIndex: 1));
            _driver.Start();
            LoginAccepted();

            Assert.AreEqual(FlowState.ConnectingToChar, _flow.State);
            Assert.AreEqual("10.0.0.2", _client.Connects[1].Host);
            Assert.AreEqual(40000, _client.Connects[1].Port);
        }

        [Test]
        public void ServerSelection_ActsOnce()
        {
            Make(Settings(serverIndex: 0));
            _driver.Start();
            LoginAccepted();
            Assert.AreEqual(2, _client.Connects.Count, "login + one character-server connection");
        }

        [Test]
        public void WithCharacters_SelectsTheConfiguredSlot_Once()
        {
            Make(Settings(slot: 4));
            _driver.Start();
            LoginAccepted();
            CharServerReady(withCharacter: true);
            _client.Deliver(Pin(7));                                  // a late PIN state must not trigger a second selection
            _client.Deliver(Pin(7));

            Assert.AreEqual(FlowState.SelectingCharacter, _flow.State);
            Assert.AreEqual(1, _client.SentCount(PacketIds.SELECT_CHARACTER));
            Assert.AreEqual(4, _client.Sent[_client.Sent.Count - 1].Bytes[2]);
        }

        // ---- creating a character ----

        [Test]
        public void NoCharacters_WaitsForThePinState_ThenCreatesWithTheDraft()
        {
            Make(Settings(name: "Newbie", createSlot: 2, hairColor: 3, hairStyle: 7, job: 4001, sex: AccountSex.SEX_FEMALE));
            _driver.Start();
            LoginAccepted();
            CharServerReady(withCharacter: false);

            Assert.AreEqual(FlowState.CharSelection, _flow.State);
            Assert.AreEqual(0, _client.SentCount(PacketIds.CREATE_CHARACTER), "PIN state still unknown");

            _client.Deliver(Pin((short)PinState_Unused));
            Assert.AreEqual(FlowState.CreatingCharacter, _flow.State);
            Assert.AreEqual(1, _client.SentCount(PacketIds.CREATE_CHARACTER));

            var bytes = _client.Sent[_client.Sent.Count - 1].Bytes;
            Assert.AreEqual((byte)'N', bytes[2]);
            Assert.AreEqual(2, bytes[26], "slot");
            Assert.AreEqual(new byte[] { 3, 0 }, new[] { bytes[27], bytes[28] }, "hair color");
            Assert.AreEqual(new byte[] { 7, 0 }, new[] { bytes[29], bytes[30] }, "hair style");
            Assert.AreEqual(new byte[] { 0xA1, 0x0F, 0, 0 }, new[] { bytes[31], bytes[32], bytes[33], bytes[34] }, "job class");
            Assert.AreEqual((byte)AccountSex.SEX_FEMALE, bytes[35]);
        }

        private const short PinState_Unused = 7;

        [Test]
        public void PinStateThatRequiresInput_StopsWithAWarning()
        {
            Make(Settings());
            _driver.Start();
            LoginAccepted();
            CharServerReady(withCharacter: false);

            LogAssert.Expect(LogType.Warning, new Regex("does not allow creating"));
            _client.Deliver(Pin(1));                                  // Ask: a PIN must be entered first
            Assert.AreEqual(0, _client.SentCount(PacketIds.CREATE_CHARACTER));
            Assert.AreEqual(FlowState.CharSelection, _flow.State);
        }

        [Test]
        public void CreationSuccess_ThenSelectsTheNewCharacter()
        {
            Make(Settings(slot: 0));
            _driver.Start();
            LoginAccepted();
            CharServerReady(withCharacter: false);
            _client.Deliver(Pin(PinState_Unused));
            _client.Deliver(TestPackets.CreationSuccess(9, "DevHero", 0));

            Assert.AreEqual(FlowState.SelectingCharacter, _flow.State);
            Assert.AreEqual(1, _client.SentCount(PacketIds.CREATE_CHARACTER));
            Assert.AreEqual(1, _client.SentCount(PacketIds.SELECT_CHARACTER));
        }

        [Test]
        public void CreationFailure_IsNotRetriedInALoop()
        {
            Make(Settings());
            _driver.Start();
            LoginAccepted();
            CharServerReady(withCharacter: false);
            _client.Deliver(Pin(PinState_Unused));

            LogAssert.Expect(LogType.Warning, new Regex("character creation failed"));
            _client.Deliver(TestPackets.CreationFailed(CHARACTER_CREATION_FAILED.NameAlreadyExists));

            Assert.AreEqual(FlowState.CharSelection, _flow.State);
            Assert.AreEqual(1, _client.SentCount(PacketIds.CREATE_CHARACTER), "exactly one attempt");
        }

        [Test]
        public void RefusedMapEntry_IsNotRetriedInALoop()
        {
            Make(Settings());
            _driver.Start();
            LoginAccepted();
            CharServerReady(withCharacter: true);
            LogAssert.Expect(LogType.Warning, new Regex("selection was refused"));
            _client.Deliver(TestPackets.MapEnterRefused(0));

            Assert.AreEqual(FlowState.CharSelection, _flow.State);
            Assert.AreEqual(1, _client.SentCount(PacketIds.SELECT_CHARACTER));
        }

        // ---- map ----

        [Test]
        public void Authenticated_LeavesTheMapLoadToTheWorld()
        {
            // SPEC-0010 FR-1 (PLAN-0010 section 7): whoever loads the scene calls MapLoaded
            Make(Settings());
            _driver.Start();
            LoginAccepted();
            CharServerReady(withCharacter: true);
            _client.Deliver(TestPackets.MapAccepted(1, "prontera", new byte[] { 10, 0, 0, 2 }, 6122));
            _client.RaiseConnected(ConnectionRole.Map);
            _client.Deliver(TestPackets.AuthenticationAccepted());

            Assert.AreEqual(FlowState.LoadingMap, _flow.State);
            Assert.AreEqual(0, _client.SentCount(PacketIds.MAP_LOADED));
            _flow.MapLoaded();
            Assert.AreEqual(FlowState.InGame, _flow.State);
        }

        [Test]
        public void Authenticated_WithoutAWorld_FinishesTheMapLoad_Once()
        {
            Make(Settings(withoutWorld: true));
            _driver.Start();
            LoginAccepted();
            CharServerReady(withCharacter: true);
            _client.Deliver(TestPackets.MapAccepted(1, "prontera", new byte[] { 10, 0, 0, 2 }, 6122));
            _client.RaiseConnected(ConnectionRole.Map);
            _client.Deliver(TestPackets.AuthenticationAccepted());
            _client.Deliver(TestPackets.AuthenticationAccepted());   // a duplicate must not send a second MAP_LOADED

            Assert.AreEqual(FlowState.InGame, _flow.State);
            Assert.AreEqual(1, _client.SentCount(PacketIds.MAP_LOADED));
        }

        [Test]
        public void WithoutAWorld_AMapChangeRightAfterMapLoaded_IsFinishedToo()
        {
            // Hercules moves a character that a login script warped while it was loading (MAP_CHANGE after MAP_LOADED)
            Make(Settings(withoutWorld: true));
            _driver.Start();
            LoginAccepted();
            CharServerReady(withCharacter: true);
            _client.Deliver(TestPackets.MapAccepted(1, "prontera", new byte[] { 10, 0, 0, 2 }, 6122));
            _client.RaiseConnected(ConnectionRole.Map);
            _client.Deliver(TestPackets.AuthenticationAccepted());
            _client.Deliver(new Packets.In.Map.MAP_CHANGE(new byte[20]));

            Assert.AreEqual(FlowState.InGame, _flow.State);
            Assert.AreEqual(2, _client.SentCount(PacketIds.MAP_LOADED));
        }

        // ---- failure and retries (FR-11) ----

        [Test]
        public void WrongCredentials_OneAttempt_SendsExactlyOneLoginRequest()
        {
            Make(Settings(attempts: 1));
            _driver.Start();
            LogAssert.Expect(LogType.Warning, new Regex("flow failed"));
            _client.RaiseConnected(ConnectionRole.Login);
            _client.Deliver(TestPackets.Refusal(1));

            Assert.AreEqual(FlowState.Failed, _flow.State);
            Assert.AreEqual(1, _client.SentCount(PacketIds.REQUEST_LOGIN));
            Assert.AreEqual(1, _client.Connects.Count);
        }

        [Test]
        public void MaxLoginAttempts_BoundsTheRetries()
        {
            Make(Settings(attempts: 3));
            _driver.Start();
            LogAssert.Expect(LogType.Warning, new Regex("flow failed"));
            LogAssert.Expect(LogType.Warning, new Regex("flow failed"));
            LogAssert.Expect(LogType.Warning, new Regex("flow failed"));

            for (int i = 0; i < 3; i++)
            {
                _client.RaiseConnected(ConnectionRole.Login);
                _client.Deliver(TestPackets.Refusal(1));
            }

            Assert.AreEqual(FlowState.Failed, _flow.State, "the third refusal ends the attempts");
            Assert.AreEqual(3, _client.SentCount(PacketIds.REQUEST_LOGIN), "exactly three requests, then no more");
            Assert.AreEqual(3, _client.Connects.Count);
        }

        [Test]
        public void ARetry_StartsFromACleanSession()
        {
            Make(Settings(attempts: 2));
            _driver.Start();
            LogAssert.Expect(LogType.Warning, new Regex("flow failed"));
            _client.RaiseConnected(ConnectionRole.Login);
            _client.Deliver(TestPackets.Refusal(1));

            Assert.AreEqual(FlowState.LoggingIn, _flow.State, "reset and logged in again");
            Assert.AreEqual(1, _client.Dispatcher.HandlerCount<Uninarok.Networking.Packets.In.Login.ACCEPT_LOGIN>(), "one subscription, not two");
        }

        // ---- lifetime ----

        [Test]
        public void Dispose_StopsReacting()
        {
            Make(Settings());
            _driver.Start();
            _driver.Dispose();
            LoginAccepted();
            Assert.AreEqual(FlowState.ServerSelection, _flow.State, "a disposed driver no longer picks a server");
        }

        [Test]
        public void BootstrapScene_ContainsTheDevTools_ButNoCredentials()
        {
            string scene = File.ReadAllText(Path.Combine(Application.dataPath, "Scenes", "Bootstrap.unity"));
            StringAssert.Contains("Uninarok.Networking.Client.NetworkClient", scene);
            StringAssert.Contains("Uninarok.Networking.Dev.DevAutoLogin", scene);
            StringAssert.DoesNotContain("password", scene.ToLowerInvariant(), "credentials must never be serialized in the scene (FR-15, D16)");
            StringAssert.DoesNotContain("credential", scene.ToLowerInvariant());
            StringAssert.DoesNotContain("dev-login", scene.ToLowerInvariant());
            Assert.IsFalse(Regex.IsMatch(scene, @"^\s*user\w*:", RegexOptions.Multiline | RegexOptions.IgnoreCase), "no user/account name field");
        }

        // ---- credentials source ----

        [Test]
        public void Parse_ReadsUserAndPassword()
        {
            var c = DevCredentialsSource.Parse("{\"user\": \"dev1\", \"password\": \"secret\"}");
            Assert.IsTrue(c.HasValue);
            Assert.AreEqual("dev1", c.Value.User);
            Assert.AreEqual("secret", c.Value.Password);
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("not json")]
        [TestCase("{\"password\": \"only\"}")]
        [TestCase("{\"user\": \"\", \"password\": \"x\"}")]
        public void Parse_RejectsMissingOrMalformedInput(string json)
        {
            Assert.IsFalse(DevCredentialsSource.Parse(json).HasValue);
        }

        [Test]
        public void LoadFromFile_MissingFile_IsNull_AndAValidFileIsRead()
        {
            string dir = Path.Combine(Path.GetTempPath(), "uninarok-dev-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(dir, DevCredentialsSource.FileName);
            Assert.IsFalse(DevCredentialsSource.LoadFromFile(path).HasValue);

            Directory.CreateDirectory(dir);
            File.WriteAllText(path, "{\"user\":\"u\",\"password\":\"p\"}");
            try
            {
                Assert.AreEqual("u", DevCredentialsSource.LoadFromFile(path).Value.User);
            }
            finally { Directory.Delete(dir, true); }
        }

        [Test]
        public void TheCredentialsFile_LivesOutsideTheProject()
        {
            StringAssert.StartsWith(Path.GetFullPath(Application.persistentDataPath), Path.GetFullPath(DevCredentialsSource.FilePath));
            StringAssert.DoesNotContain(Path.GetFullPath(Application.dataPath), Path.GetFullPath(DevCredentialsSource.FilePath));
        }
    }
}
