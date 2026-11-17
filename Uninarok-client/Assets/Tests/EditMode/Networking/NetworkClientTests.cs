#if PENDING_T5_5 // T5.4: enabled (guard removed) by T5.5 when NetworkClient exists.
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Uninarok.Networking.Client;
using Uninarok.Networking.Core.Dispatch;
using Uninarok.Networking.Core.Transport;
using Uninarok.Networking.Packets;
using Uninarok.Networking.Packets.In.Login;
using Uninarok.Networking.Packets.Out.Map;

namespace Uninarok.Networking.Tests
{
    // SPEC-0001 FR-2, FR-5, FR-7, FR-8, FR-20; plan 4.7 and 6. A real NetworkClient component, a loopback server, Pump called directly.
    public class NetworkClientTests
    {
        private GameObject _go;
        private NetworkClient _client;
        private LoopbackServer _server;
        private readonly List<string> _events = new List<string>();
        private readonly List<byte> _notices = new List<byte>();
        private readonly List<CloseInfo> _closures = new List<CloseInfo>();
        private int _connectedCount;

        [SetUp]
        public void SetUp()
        {
            _events.Clear(); _notices.Clear(); _closures.Clear(); _connectedCount = 0;
            _server = new LoopbackServer();
            _go = new GameObject("NetworkClientTests");
            _client = _go.AddComponent<NetworkClient>();
            _client.Connected += role => { _connectedCount++; _events.Add("Connected " + role); };
            _client.Disconnected += (role, info) => { _closures.Add(info); _events.Add("Disconnected " + info.Reason); };
            _client.Subscribe<LOGIN_SERVER_NOTICE>((in LOGIN_SERVER_NOTICE p) => { _notices.Add(p.ErrorCode); _events.Add("Notice " + p.ErrorCode); });
        }

        [TearDown]
        public void TearDown()
        {
            _client.Disconnect();
            UnityEngine.Object.DestroyImmediate(_go);
            _server.Dispose();
        }

        private bool PumpUntil(Func<bool> done, int timeoutMs = 4000)
            => LoopbackServer.WaitUntil(() => { _client.Pump(); return done(); }, timeoutMs);

        private Socket ConnectLogin()
        {
            _client.Connect("127.0.0.1", _server.Port, ConnectionRole.Login);
            Assert.IsTrue(PumpUntil(() => _client.Status == ConnectionStatus.Connected), "should connect");
            return _server.Accept();
        }

        private static byte[] Notice(byte code) => Wire.Fixed(PacketIds.LOGIN_SERVER_NOTICE, code);

        // ---- connecting ----

        [Test]
        public void Connect_RaisesConnectedOnce_AndStatusFollows()
        {
            Assert.AreEqual(ConnectionStatus.Idle, _client.Status);
            _client.Connect("127.0.0.1", _server.Port, ConnectionRole.Character);
            Assert.AreEqual(ConnectionStatus.Connecting, _client.Status);
            Assert.AreEqual(ConnectionRole.Character, _client.Role);

            Assert.IsTrue(PumpUntil(() => _connectedCount > 0));
            for (int i = 0; i < 20; i++) _client.Pump();
            using var peer = _server.Accept();

            Assert.AreEqual(1, _connectedCount, "Connected is raised exactly once");
            Assert.AreEqual(ConnectionStatus.Connected, _client.Status);
        }

        [Test]
        public void Connect_ToAClosedPort_RaisesDisconnectedConnectFailed()
        {
            LogAssert.Expect(LogType.Error, new Regex("connect failed"));
            _client.Connect("127.0.0.1", LoopbackServer.ClosedPort(), ConnectionRole.Login);

            Assert.IsTrue(PumpUntil(() => _closures.Count > 0, 8000), "the failure must surface");
            Assert.AreEqual(CloseReason.ConnectFailed, _closures[0].Reason);
            Assert.AreEqual(0, _connectedCount);
            Assert.AreEqual(ConnectionStatus.Idle, _client.Status);
        }

        [Test]
        public void Connect_ToAPortAbove32767_Works()
        {
            // FR-12: the port is a ushort end to end. The OS picks ephemeral ports above 32767 on Windows in most cases;
            // the signature itself (ushort) is what makes 40000 representable.
            _client.Connect("127.0.0.1", _server.Port, ConnectionRole.Map);
            Assert.IsTrue(PumpUntil(() => _client.Status == ConnectionStatus.Connected));
            using var peer = _server.Accept();
        }

        // ---- receiving ----

        [Test]
        public void ReceivedPackets_AreDispatchedOnPump_InOrder()
        {
            using var peer = ConnectLogin();
            peer.Send(Wire.Concat(Notice(1), Notice(2), Notice(3)));

            Assert.IsTrue(PumpUntil(() => _notices.Count == 3));
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, _notices);
        }

        [Test]
        public void PacketsAreSplitAcrossReads_AndStillFramed()
        {
            using var peer = ConnectLogin();
            var bytes = Wire.Concat(Notice(7), Notice(9));
            peer.NoDelay = true;
            for (int i = 0; i < bytes.Length; i++) { peer.Send(new[] { bytes[i] }); System.Threading.Thread.Sleep(2); }

            Assert.IsTrue(PumpUntil(() => _notices.Count == 2));
            CollectionAssert.AreEqual(new byte[] { 7, 9 }, _notices);
        }

        [Test]
        public void Pump_RespectsMaxPacketsPerFrame()
        {
            _client.MaxPacketsPerFrame = 4;
            using var peer = ConnectLogin();
            peer.Send(Wire.Repeat(Notice(5), 10));
            Assert.IsTrue(LoopbackServer.WaitUntil(() => { _client.Pump(); return _notices.Count > 0; }));

            int before = _notices.Count;
            Assert.LessOrEqual(before, 4, "no more than the cap in one frame");
            Assert.IsTrue(PumpUntil(() => _notices.Count == 10));
        }

        // ---- closing ----

        [Test]
        public void DrainBeforeClose_APacketSentJustBeforeTheServerClosesIsHandledFirst()
        {
            using var peer = ConnectLogin();
            peer.Send(Notice(8));            // a login refusal, then the server closes the socket (FR-7)
            peer.Close();

            Assert.IsTrue(PumpUntil(() => _closures.Count > 0));
            LogAssert.Expect(LogType.Warning, new Regex("server closed"));
            CollectionAssert.AreEqual(new[] { "Connected Login", "Notice 8", "Disconnected RemoteClosed" }, _events,
                "the packet is delivered before the closure is reported");
            Assert.AreEqual(ConnectionStatus.Idle, _client.Status);
        }

        [Test]
        public void ServerClose_IsReportedOnce()
        {
            using var peer = ConnectLogin();
            peer.Close();
            Assert.IsTrue(PumpUntil(() => _closures.Count > 0));
            for (int i = 0; i < 30; i++) _client.Pump();
            LogAssert.Expect(LogType.Warning, new Regex("server closed"));
            Assert.AreEqual(1, _closures.Count);
        }

        [Test]
        public void Disconnect_RaisesNothing_AndLogsNothing()
        {
            using var peer = ConnectLogin();
            _client.Disconnect();
            for (int i = 0; i < 30; i++) _client.Pump();

            Assert.AreEqual(0, _closures.Count);
            Assert.AreEqual(ConnectionStatus.Idle, _client.Status);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Disconnect_WhileConnecting_CancelsSilently()
        {
            _client.Connect("127.0.0.1", _server.Port, ConnectionRole.Login);
            _client.Disconnect();
            for (int i = 0; i < 50; i++) { _client.Pump(); System.Threading.Thread.Sleep(2); }
            Assert.AreEqual(0, _connectedCount);
            Assert.AreEqual(0, _closures.Count);
            Assert.AreEqual(ConnectionStatus.Idle, _client.Status);
        }

        [Test]
        public void ReconnectFromAHandler_StopsTheOldStream()
        {
            using var peer = ConnectLogin();
            using var second = new LoopbackServer();
            int reconnects = 0;
            _client.Subscribe<LOGIN_SERVER_NOTICE>((in LOGIN_SERVER_NOTICE p) =>
            {
                if (reconnects++ == 0) _client.Connect("127.0.0.1", second.Port, ConnectionRole.Character);
            });

            peer.Send(Wire.Concat(Notice(1), Notice(2), Notice(3)));       // all three arrive in one read
            Assert.IsTrue(PumpUntil(() => _client.Role == ConnectionRole.Character && _client.Status == ConnectionStatus.Connected));
            for (int i = 0; i < 20; i++) _client.Pump();

            CollectionAssert.AreEqual(new byte[] { 1 }, _notices, "packets 2 and 3 of the old connection must not be dispatched");
            Assert.AreEqual(0, _closures.Count, "replacing a connection is silent");
        }

        // ---- faults ----

        [Test]
        public void UnknownPacketId_ClosesWithProtocolFault_AndLogsExactlyOneError()
        {
            using var peer = ConnectLogin();
            LogAssert.Expect(LogType.Error, new Regex("protocol fault: Unknown packet 0xFFFF"));
            peer.Send(new byte[] { 0xFF, 0xFF, 1, 2, 3 });

            Assert.IsTrue(PumpUntil(() => _closures.Count > 0));
            for (int i = 0; i < 30; i++) _client.Pump();

            Assert.AreEqual(1, _closures.Count);
            Assert.AreEqual(CloseReason.ProtocolFault, _closures[0].Reason);
            StringAssert.Contains("Unknown packet 0xFFFF", _closures[0].Message);
            Assert.AreEqual(ConnectionStatus.Idle, _client.Status);
        }

        [Test]
        public void PacketsBeforeAFault_AreStillDispatched()
        {
            using var peer = ConnectLogin();
            LogAssert.Expect(LogType.Error, new Regex("protocol fault"));
            peer.Send(Wire.Concat(Notice(4), new byte[] { 0xFF, 0xFF }));

            Assert.IsTrue(PumpUntil(() => _closures.Count > 0));
            CollectionAssert.AreEqual(new byte[] { 4 }, _notices);
        }

        [Test]
        public void KnownButUnhandledPacket_IsSkipped_WithoutClosing()
        {
            using var peer = ConnectLogin();
            peer.Send(Wire.Concat(Wire.Fixed(0x0066, 9), Notice(6)));        // 0x0066 is in the table, but not an incoming packet

            Assert.IsTrue(PumpUntil(() => _notices.Count == 1));
            Assert.AreEqual(0, _closures.Count);
            Assert.AreEqual(ConnectionStatus.Connected, _client.Status);
        }

        // ---- sending ----

        [Test]
        public void Send_WhenNotConnected_ReturnsFalse()
        {
            Assert.IsFalse(_client.Send(new MAP_LOADED()));
        }

        [Test]
        public void Send_WhenConnected_ReachesTheServer()
        {
            using var peer = ConnectLogin();
            Assert.IsTrue(_client.Send(new HEARTBEAT_REQUEST(0x01020304)));
            Assert.AreEqual(new byte[] { 0x60, 0x03, 0x04, 0x03, 0x02, 0x01 }, LoopbackServer.ReadExactly(peer, 6));
        }

        [Test]
        public void Send_AfterTheServerClosed_ReturnsFalse_WithoutThrowing()
        {
            using var peer = ConnectLogin();
            peer.Close();
            Assert.IsTrue(PumpUntil(() => _closures.Count > 0));
            LogAssert.Expect(LogType.Warning, new Regex("server closed"));
            Assert.IsFalse(_client.Send(new MAP_LOADED()));
        }

        // ---- skipping raw bytes ----

        [Test]
        public void SkipNextBytes_DiscardsRawBytesBeforeTheNextPacket()
        {
            using var peer = ConnectLogin();
            _client.SkipNextBytes(4);
            peer.Send(Wire.Concat(new byte[] { 0, 0, 0, 0 }, Notice(5)));       // the char server's account-ID echo, then a packet

            Assert.IsTrue(PumpUntil(() => _notices.Count == 1));
            Assert.AreEqual(0, _closures.Count);
        }

        // ---- subscriptions ----

        [Test]
        public void Unsubscribe_StopsDelivery()
        {
            using var peer = ConnectLogin();
            int calls = 0;
            PacketHandler<LOGIN_SERVER_NOTICE> h = (in LOGIN_SERVER_NOTICE p) => calls++;
            _client.Subscribe(h);
            peer.Send(Notice(1));
            Assert.IsTrue(PumpUntil(() => calls == 1));

            _client.Unsubscribe(h);
            peer.Send(Notice(2));
            Assert.IsTrue(PumpUntil(() => _notices.Count == 2));
            Assert.AreEqual(1, calls);
        }
    }
}
#endif
