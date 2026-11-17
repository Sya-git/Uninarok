using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Uninarok.Networking.Client;
using Uninarok.Networking.Core.Transport;
using Uninarok.Networking.Packets;
using Uninarok.Networking.Packets.In.Map;
using Uninarok.Networking.Packets.Out.Map;

namespace Uninarok.Networking.Tests
{
    // SPEC-0001 FR-6 end to end over real sockets: a received packet costs the main thread no managed memory
    // (framing, routing, decoding, dispatching and the handler), and a sent packet costs none either.
    public class NetworkAllocationTests
    {
        private LoopbackServer _server;
        private GameObject _go;
        private NetworkClient _client;

        [SetUp]
        public void SetUp()
        {
            _server = new LoopbackServer();
            _go = new GameObject("NetworkAllocationTests");
            _client = _go.AddComponent<NetworkClient>();
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

        [Test]
        public void ThousandReceivedPackets_AreFramedRoutedAndDispatched_WithoutAllocatingOnTheMainThread()
        {
            int handled = 0;
            uint last = 0;
            _client.Subscribe<SERVER_HEARTBEAT_RESPONSE>((in SERVER_HEARTBEAT_RESPONSE p) => { handled++; last = p.ServerTimestamp; });

            _client.MaxPacketsPerFrame = 1000;
            _client.Connect("127.0.0.1", _server.Port, ConnectionRole.Map);
            using var peer = _server.Accept();
            Assert.IsTrue(PumpUntil(() => _client.Status == ConnectionStatus.Connected));

            // 11 warm-up packets, then 2000 more that all arrive before the measured pump.
            var one = new byte[] { 0x7F, 0x00, 1, 0, 0, 0 };
            peer.Send(Wire.Repeat(one, 11));
            Assert.IsTrue(PumpUntil(() => handled >= 11), "warm-up packets are handled");
            peer.Send(Wire.Repeat(one, 2000));
            System.Threading.Thread.Sleep(300);                      // loopback: everything is in the framer's inbox by now

            int before = handled;
            Assert.That(() => { _client.Pump(); }, UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory(),
                        "Pump over a burst of received packets must allocate nothing on the main thread");
            Assert.GreaterOrEqual(handled - before, 100, "the measured pump really processed a burst of packets");
            Assert.AreEqual(1u, last);
        }

        [Test]
        public void SendingAPacket_ThroughTheClient_AllocatesNothingOnTheMainThread()
        {
            _client.Connect("127.0.0.1", _server.Port, ConnectionRole.Map);
            using var peer = _server.Accept();
            Assert.IsTrue(PumpUntil(() => _client.Status == ConnectionStatus.Connected));

            var heartbeat = new HEARTBEAT_REQUEST(1234);
            for (int i = 0; i < 20; i++) _client.Send(in heartbeat);             // warm-up
            LoopbackServer.ReadExactly(peer, 20 * 6);

            Assert.That(() => { _client.Send(in heartbeat); }, UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void AnIdlePump_AllocatesNothing()
        {
            _client.Connect("127.0.0.1", _server.Port, ConnectionRole.Map);
            using var peer = _server.Accept();
            Assert.IsTrue(PumpUntil(() => _client.Status == ConnectionStatus.Connected));
            for (int i = 0; i < 10; i++) _client.Pump();

            Assert.That(() => { _client.Pump(); }, UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void TheFlowTick_AtRest_AllocatesNothing()
        {
            var flow = _client.Flow;
            flow.Tick();
            Assert.That(() => { flow.Tick(); }, UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory());
        }
    }
}
