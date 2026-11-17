using System;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using Uninarok.Networking.Client;
using Uninarok.Networking.Data;
using Uninarok.Networking.Dev;
using Uninarok.Networking.Flow;

namespace Uninarok.Networking.Tests
{
    // T7.1 Done-when / SPEC-0001 FR-11, FR-15: the whole stack over real sockets, with a login server that refuses.
    public class DevAutoLoginIntegrationTests
    {
        private LoopbackServer _server;
        private GameObject _go;
        private NetworkClient _client;
        private DevAutoLoginDriver _driver;

        [SetUp]
        public void SetUp()
        {
            _server = new LoopbackServer();
            _go = new GameObject("DevAutoLoginIntegrationTests");
            _client = _go.AddComponent<NetworkClient>();
            SetPrivate(_client, "loginHost", "127.0.0.1");
            SetPrivate(_client, "loginPort", (int)_server.Port);
        }

        [TearDown]
        public void TearDown()
        {
            _driver?.Dispose();
            _client.Disconnect();
            UnityEngine.Object.DestroyImmediate(_go);
            _server.Dispose();
        }

        private static void SetPrivate(object target, string field, object value)
            => target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

        private bool PumpUntil(Func<bool> done, int timeoutMs = 4000)
            => LoopbackServer.WaitUntil(() => { _client.Pump(); _client.Flow.Tick(); return done(); }, timeoutMs);

        [Test]
        public void WrongCredentials_SendExactlyOneLoginRequest_ThenFail_WithoutRetrying()
        {
            var settings = new DevAutoLoginSettings
            {
                ServerIndex = 0, CharacterSlot = 0, MaxLoginAttempts = 1,
                Draft = new CharacterDraft(new FixedString32Bytes("DevHero"), 0, 1, 1, 0, AccountSex.SEX_MALE),
            };
            _driver = new DevAutoLoginDriver(_client.Flow, settings, () => new DevCredentials("wrong", "credentials"));

            _driver.Start();
            using var peer = _server.Accept();
            Assert.IsTrue(PumpUntil(() => _client.Status == ConnectionStatus.Connected), "the client connects");

            // exactly one REQUEST_LOGIN (0x0064, 55 bytes) with the credentials
            var request = LoopbackServer.ReadExactly(peer, 55);
            Assert.AreEqual(new byte[] { 0x64, 0x00 }, new[] { request[0], request[1] });
            Assert.AreEqual((byte)'w', request[6]);
            Assert.AreEqual((byte)'c', request[30]);

            // the server refuses: LOGIN_REFUSAL (0x006A, 23 bytes), error code 1
            var refusal = new byte[23];
            refusal[0] = 0x6A; refusal[2] = 1;
            peer.Send(refusal);

            LogAssert.Expect(LogType.Warning, new Regex("flow failed"));
            Assert.IsTrue(PumpUntil(() => _client.Flow.State == FlowState.Failed), "the flow ends in a failed state");
            Assert.AreEqual(FailureCode.LoginRefused, _client.Flow.Failure.Value.Code);

            // time passes: nothing more is sent and no new connection is opened
            LoopbackServer.WaitUntil(() => { _client.Pump(); _client.Flow.Tick(); return false; }, 400);
            peer.ReceiveTimeout = 300;
            int extra = 0;
            try { extra = peer.Receive(new byte[16]); } catch (SocketException) { }       // timeout or reset: nothing arrived
            Assert.AreEqual(0, extra, "no second login request (FR-11)");
            Assert.IsFalse(_server.HasPendingClient, "no second connection");
            Assert.AreEqual(FlowState.Failed, _client.Flow.State);
        }

        [Test]
        public void ARefusalFollowedByTheServerClosing_StillEndsInLoginRefused()
        {
            var settings = new DevAutoLoginSettings { MaxLoginAttempts = 1 };
            _driver = new DevAutoLoginDriver(_client.Flow, settings, () => new DevCredentials("u", "p"));
            _driver.Start();
            var peer = _server.Accept();
            Assert.IsTrue(PumpUntil(() => _client.Status == ConnectionStatus.Connected));
            LoopbackServer.ReadExactly(peer, 55);

            var refusal = new byte[23];
            refusal[0] = 0x6A; refusal[2] = 3;
            peer.Send(refusal);
            peer.Close();                                                                   // Hercules closes right after refusing

            LogAssert.Expect(LogType.Warning, new Regex("flow failed"));
            Assert.IsTrue(PumpUntil(() => _client.Flow.State == FlowState.Failed));
            Assert.AreEqual(FailureCode.LoginRefused, _client.Flow.Failure.Value.Code, "the refusal is handled before the closure (FR-7)");
        }
    }
}
