using NUnit.Framework;
using Uninarok.Networking.Client;
using Uninarok.Networking.Core.Transport;
using Uninarok.Networking.Packets;
using Uninarok.Networking.Packets.In.Login;
using Uninarok.Networking.Packets.Out.Character;
using Uninarok.Networking.Packets.Out.Map;

namespace Uninarok.Networking.Tests
{
    // T5.3 / SPEC-0001 FR-20: the simulated connection behaves as the manager and flow suites rely on.
    public class FakeNetworkClientTests
    {
        [Test]
        public void Send_WhenNotConnected_ReportsFailure_AndRecordsNothing()
        {
            var fake = new FakeNetworkClient();
            Assert.IsFalse(fake.Send(new MAP_LOADED()));
            Assert.AreEqual(0, fake.Sent.Count);
        }

        [Test]
        public void Send_WhenConnected_RecordsTheSerializedBytes_AndTheirId()
        {
            var fake = new FakeNetworkClient();
            fake.Connect("127.0.0.1", 6121, ConnectionRole.Character);
            fake.RaiseConnected();

            Assert.IsTrue(fake.Send(new CHARACTER_SERVER_PING(2000001)));
            Assert.AreEqual(1, fake.Sent.Count);
            Assert.AreEqual(PacketIds.CHARACTER_SERVER_PING, fake.Sent[0].Id);
            Assert.AreEqual(new byte[] { 0x87, 0x01, 0x81, 0x84, 0x1E, 0x00 }, fake.Sent[0].Bytes);
            Assert.AreEqual(1, fake.SentCount(PacketIds.CHARACTER_SERVER_PING));
        }

        [Test]
        public void Actions_KeepCallOrder_SoSkipBeforeSendCanBeAsserted()
        {
            var fake = new FakeNetworkClient();
            fake.Connect("10.0.0.1", 6121, ConnectionRole.Character);
            fake.RaiseConnected();
            fake.SkipNextBytes(4);
            fake.Send(new HEARTBEAT_REQUEST(1));
            fake.Disconnect();

            CollectionAssert.AreEqual(new[] { "Connect Character 10.0.0.1:6121", "Skip 4", "Send 0x0360", "Disconnect" }, fake.Actions);
        }

        [Test]
        public void AcceptSendsFalse_SimulatesAFailedSend()
        {
            var fake = new FakeNetworkClient();
            fake.RaiseConnected(ConnectionRole.Map);
            fake.AcceptSends = false;
            Assert.IsFalse(fake.Send(new MAP_LOADED()));
        }

        [Test]
        public void ConnectAndDisconnect_UpdateStatus_AndRoleIsKept()
        {
            var fake = new FakeNetworkClient();
            fake.Connect("h", 40000, ConnectionRole.Map);
            Assert.AreEqual(ConnectionStatus.Connecting, fake.Status);
            Assert.AreEqual(ConnectionRole.Map, fake.Role);
            Assert.AreEqual(40000, fake.Connects[0].Port, "a port above 32767 is recorded unchanged (FR-12)");

            fake.RaiseConnected();
            Assert.AreEqual(ConnectionStatus.Connected, fake.Status);
            fake.Disconnect();
            Assert.AreEqual(ConnectionStatus.Idle, fake.Status);
            Assert.AreEqual(1, fake.DisconnectCalls);
        }

        [Test]
        public void RaiseConnectedAndDisconnected_FireTheEvents()
        {
            var fake = new FakeNetworkClient();
            ConnectionRole? connected = null;
            CloseInfo closed = null;
            fake.Connected += r => connected = r;
            fake.Disconnected += (r, i) => closed = i;

            fake.RaiseConnected(ConnectionRole.Login);
            fake.RaiseDisconnected(new CloseInfo(CloseReason.RemoteClosed, "bye"));

            Assert.AreEqual(ConnectionRole.Login, connected);
            Assert.AreEqual(CloseReason.RemoteClosed, closed.Reason);
            Assert.AreEqual(ConnectionStatus.Idle, fake.Status);
        }

        [Test]
        public void Deliver_ReachesSubscribedHandlers_ThroughARealDispatcher()
        {
            var fake = new FakeNetworkClient();
            byte code = 0;
            fake.Subscribe<LOGIN_SERVER_NOTICE>((in LOGIN_SERVER_NOTICE p) => code = p.ErrorCode);
            fake.Deliver(new LOGIN_SERVER_NOTICE(new byte[] { 8 }));
            Assert.AreEqual(8, code);
        }

        [Test]
        public void ThrowingHandler_IsCollected_NotPropagated()
        {
            var fake = new FakeNetworkClient();
            fake.Subscribe<LOGIN_SERVER_NOTICE>((in LOGIN_SERVER_NOTICE p) => throw new System.InvalidOperationException("boom"));
            fake.Deliver(new LOGIN_SERVER_NOTICE(new byte[] { 1 }));
            Assert.AreEqual(1, fake.HandlerErrors.Count);
        }
    }
}
