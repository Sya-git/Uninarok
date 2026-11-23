using System;
using System.Net.Sockets;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using Uninarok.Networking.Client;
using Uninarok.Networking.Core.Transport;
using Uninarok.Networking.Data;
using Uninarok.Networking.Flow;
using Uninarok.Networking.Packets;

namespace Uninarok.Networking.Tests
{
    // SPEC-0001 FR-3, FR-10, FR-11, FR-12 end to end: a real NetworkClient and NetworkFlow over real loopback sockets,
    // against scripted servers that speak the Hercules wire format (T9.5 automated half; the real-Hercules run is T9.4/T9.5).
    public class FlowIntegrationTests
    {
        private GameObject _go;
        private NetworkClient _client;
        private LoopbackServer _login;

        [SetUp]
        public void SetUp()
        {
            _login = new LoopbackServer();
            _go = new GameObject("FlowIntegrationTests");
            _client = _go.AddComponent<NetworkClient>();
            SetPrivate("loginHost", "127.0.0.1");
            SetPrivate("loginPort", (int)_login.Port);
        }

        [TearDown]
        public void TearDown()
        {
            _client.Disconnect();
            UnityEngine.Object.DestroyImmediate(_go);
            _login.Dispose();
        }

        private void SetPrivate(string field, object value)
            => typeof(NetworkClient).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_client, value);

        private NetworkFlow Flow => _client.Flow;

        private bool PumpUntil(Func<bool> done, int timeoutMs = 5000)
            => LoopbackServer.WaitUntil(() => { _client.Pump(); Flow.Tick(); return done(); }, timeoutMs);

        private bool PumpUntilState(FlowState state, int timeoutMs = 5000) => PumpUntil(() => Flow.State == state, timeoutMs);

        // ---- failure checks (FR-10) ----

        [Test]
        public void ClosedPort_ReachesFailedWithConnectFailed()
        {
            _login.Dispose();                                          // nothing listens on the login port any more
            LogAssert.Expect(LogType.Error, new Regex("connect failed"));
            LogAssert.Expect(LogType.Warning, new Regex("Flow failed"));

            Flow.StartLogin("user", "pass");
            Assert.IsTrue(PumpUntilState(FlowState.Failed, 8000), "a closed port must end in Failed, not hang");
            Assert.AreEqual(FailureCode.ConnectFailed, Flow.Failure.Value.Code);
            _login = new LoopbackServer();                             // so TearDown can dispose
        }

        [Test]
        public void SilentServer_ReachesFailedWithReplyTimeout()
        {
            _client.Settings.ReplySeconds = 1f;
            LogAssert.Expect(LogType.Warning, new Regex("Flow failed"));

            Flow.StartLogin("user", "pass");
            using var peer = _login.Accept();                          // accepts, then says nothing
            var sw = System.Diagnostics.Stopwatch.StartNew();
            Assert.IsTrue(PumpUntilState(FlowState.Failed, 6000), "a silent server must end in Failed");

            Assert.AreEqual(FailureCode.ReplyTimeout, Flow.Failure.Value.Code);
            Assert.Less(sw.ElapsedMilliseconds, 4000, "within the configured timeout plus a small margin");
            Assert.AreEqual(55, LoopbackServer.ReadExactly(peer, 55).Length, "the single login request did arrive");
        }

        [Test]
        public void ServerThatClosesImmediately_ReachesFailedWithRemoteClosed()
        {
            LogAssert.Expect(LogType.Warning, new Regex("server closed"));
            LogAssert.Expect(LogType.Warning, new Regex("Flow failed"));

            Flow.StartLogin("user", "pass");
            var peer = _login.Accept();
            peer.Close();
            Assert.IsTrue(PumpUntilState(FlowState.Failed));
            Assert.AreEqual(FailureCode.RemoteClosed, Flow.Failure.Value.Code);
        }

        [Test]
        public void ServerThatSendsGarbage_ReachesFailedWithProtocolFault()
        {
            LogAssert.Expect(LogType.Error, new Regex("protocol fault"));
            LogAssert.Expect(LogType.Warning, new Regex("Flow failed"));

            Flow.StartLogin("user", "pass");
            using var peer = _login.Accept();
            Assert.IsTrue(PumpUntil(() => _client.Status == ConnectionStatus.Connected));
            peer.Send(new byte[] { 0xFF, 0xFF, 0x00, 0x01 });
            Assert.IsTrue(PumpUntilState(FlowState.Failed));
            Assert.AreEqual(FailureCode.ProtocolFault, Flow.Failure.Value.Code);
        }

        [Test]
        public void WrongPassword_OneRequest_FailedLoginRefused()
        {
            LogAssert.Expect(LogType.Warning, new Regex("Flow failed"));
            Flow.StartLogin("user", "wrong");
            using var peer = _login.Accept();
            Assert.IsTrue(PumpUntil(() => _client.Status == ConnectionStatus.Connected));
            LoopbackServer.ReadExactly(peer, 55);
            peer.Send(Wire.Fixed(PacketIds.LOGIN_REFUSAL, Concat(new byte[] { 1 }, new byte[20])));

            Assert.IsTrue(PumpUntilState(FlowState.Failed));
            Assert.AreEqual(FailureCode.LoginRefused, Flow.Failure.Value.Code);
        }

        // ---- the whole journey against scripted Hercules servers ----

        private static byte[] Concat(byte[] a, byte[] b) => Wire.Concat(a, b);

        [Test]
        public void LoginCharacterMap_FullJourney_OverRealSockets()
        {
            using var charServer = new LoopbackServer();
            using var mapServer = LoopbackServer.OnHighPort();            // a map port above 32767 (FR-12)
            Assert.Greater(mapServer.Port, 32767);

            // 1. login
            Flow.StartLogin("user", "pass");
            using (var loginPeer = _login.Accept())
            {
                Assert.IsTrue(PumpUntil(() => _client.Status == ConnectionStatus.Connected));
                var request = LoopbackServer.ReadExactly(loginPeer, 55);
                Assert.AreEqual((byte)'u', request[6]);

                // ACCEPT_LOGIN: 60 fixed payload bytes + one 160-byte server entry
                var fixedPart = new byte[60];
                fixedPart[0] = 0x44; fixedPart[1] = 0x33; fixedPart[2] = 0x22; fixedPart[3] = 0x11;      // AuthCode
                fixedPart[4] = 0x81; fixedPart[5] = 0x84; fixedPart[6] = 0x1E;                           // AccountId 2000001
                fixedPart[8] = 99;                                                                       // UserLevel
                fixedPart[42] = 1;                                                                       // male
                var entry = TestPackets.ServerEntry(new byte[] { 127, 0, 0, 1 }, charServer.Port, "Scripted");
                loginPeer.Send(Wire.Var(PacketIds.ACCEPT_LOGIN, Concat(fixedPart, entry)));
                Assert.IsTrue(PumpUntilState(FlowState.ServerSelection));
            }
            Assert.AreEqual(2000001u, Flow.Context.AccountId);
            Assert.AreEqual("Scripted", Flow.Context.Servers[0].Name.ToString());

            // 2. character server: entry request, then the 4-byte echo (not a packet) followed by the character list
            Flow.SelectServer(0);
            using (var charPeer = charServer.Accept())
            {
                Assert.IsTrue(PumpUntil(() => _client.Status == ConnectionStatus.Connected));
                var entryRequest = LoopbackServer.ReadExactly(charPeer, 17);
                Assert.AreEqual(new byte[] { 0x65, 0x00, 0x81, 0x84, 0x1E, 0x00 }, new[] { entryRequest[0], entryRequest[1], entryRequest[2], entryRequest[3], entryRequest[4], entryRequest[5] });

                var echo = new byte[] { 0x81, 0x84, 0x1E, 0x00 };                                        // FR-3: raw bytes, not a packet
                var meta = new byte[23]; meta[0] = 9; meta[1] = 9;
                charPeer.Send(Concat(echo, Wire.Var(PacketIds.CHARACTER_DATA, Concat(meta, TestPackets.CharBytes(140593, "Poring", 0)))));
                Assert.IsTrue(PumpUntilState(FlowState.CharSelection), "the echo bytes must not fault the stream");
                Assert.AreEqual("Poring", Flow.Context.Characters[0].Name.ToString());

                // 3. select the character: the server answers with the map endpoint
                Flow.SelectCharacter(0);
                var select = LoopbackServer.ReadExactly(charPeer, 3);
                Assert.AreEqual(new byte[] { 0x66, 0x00, 0x00 }, select);

                var accepted = new byte[MAP_ENTER_REQUEST_ACCEPTED_PAYLOAD];
                accepted[0] = 0x31; accepted[1] = 0x25; accepted[2] = 0x02;                              // CharId 140593
                Array.Copy(TestPackets.Text("prontera", 16), 0, accepted, 4, 16);
                accepted[20] = 127; accepted[23] = 1;                                                    // 127.0.0.1
                accepted[24] = (byte)mapServer.Port; accepted[25] = (byte)(mapServer.Port >> 8);
                charPeer.Send(Wire.Fixed(PacketIds.MAP_ENTER_REQUEST_ACCEPTED, accepted));
                Assert.IsTrue(PumpUntilState(FlowState.ConnectingToMap) || Flow.State == FlowState.LoadingMap);
            }

            // 4. map server (port above 32767): enter request, accepted, load, in game
            using (var mapPeer = mapServer.Accept())
            {
                Assert.IsTrue(PumpUntilState(FlowState.LoadingMap));
                var enter = LoopbackServer.ReadExactly(mapPeer, 23);
                Assert.AreEqual(new byte[] { 0x36, 0x04 }, new[] { enter[0], enter[1] }, "REQUEST_ENTER_MAP");
                Assert.AreEqual(new byte[] { 0x31, 0x25, 0x02, 0x00 }, new[] { enter[6], enter[7], enter[8], enter[9] }, "CharId");

                mapPeer.Send(Wire.Fixed(PacketIds.AUTHENTICATION_ACCEPTED, new byte[] { 0x04, 0x03, 0x02, 0x01, 0x25, 0x8C, 0x84, 0x05, 0x05, 0x00, 0x00 }));
                Assert.IsTrue(PumpUntil(() => Flow.Context.Entry.StartTick == 0x01020304u));

                Flow.MapLoaded();
                Assert.AreEqual(FlowState.InGame, Flow.State);
                Assert.AreEqual(new byte[] { 0x7D, 0x00 }, LoopbackServer.ReadExactly(mapPeer, 2), "MAP_LOADED");
                Assert.AreEqual(new byte[] { 0x60, 0x03 }, new Span<byte>(LoopbackServer.ReadExactly(mapPeer, 6), 0, 2).ToArray(), "the first heartbeat (PLAN-0010 D6)");

                // 5. the server stops: the flow ends in Disconnected, never Failed
                LogAssert.Expect(LogType.Warning, new Regex("server closed"));
                mapPeer.Close();
                Assert.IsTrue(PumpUntilState(FlowState.Disconnected));
                Assert.IsNull(Flow.Failure);
            }
        }

        private const int MAP_ENTER_REQUEST_ACCEPTED_PAYLOAD = 154;
    }
}
