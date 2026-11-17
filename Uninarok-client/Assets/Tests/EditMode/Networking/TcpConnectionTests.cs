#if PENDING_T3_5 // T3.4: enabled (guard removed) by T3.5 when TcpConnection receive/close exists. Send tests: PENDING_T3_7.
using System;
using System.Net.Sockets;
using System.Threading;
using NUnit.Framework;
using Uninarok.Networking.Core.Transport;

namespace Uninarok.Networking.Tests
{
    // SPEC-0001 FR-2, FR-7, FR-8; plan 4.4. Real sockets on the loopback interface.
    public class TcpConnectionTests
    {
        private LoopbackServer _server;
        private CollectingSink _sink;

        [SetUp]
        public void SetUp()
        {
            _server = new LoopbackServer();
            _sink = new CollectingSink();
        }

        [TearDown]
        public void TearDown() => _server.Dispose();

        private TcpConnection Connect(ConnectionRole role = ConnectionRole.Login)
            => TcpConnection.ConnectAsync("127.0.0.1", _server.Port, role, _sink, CancellationToken.None).GetAwaiter().GetResult();

        [Test]
        public void Connect_ThenReceive_DeliversBytesToTheSink()
        {
            using var conn = Connect();
            using var peer = _server.Accept();
            peer.Send(new byte[] { 1, 2, 3 });
            peer.Send(new byte[] { 4, 5 });

            Assert.IsTrue(LoopbackServer.WaitUntil(() => _sink.Count == 5));
            Assert.AreEqual(new byte[] { 1, 2, 3, 4, 5 }, _sink.Snapshot());
            Assert.IsFalse(conn.IsClosed);
            Assert.IsNull(conn.Closure);
        }

        [Test]
        public void Connect_ReportsRole()
        {
            using var conn = Connect(ConnectionRole.Character);
            Assert.AreEqual(ConnectionRole.Character, conn.Role);
        }

        [Test]
        public void RemoteClose_IsObservedWithin1Second_AsRemoteClosed()
        {
            using var conn = Connect();
            var peer = _server.Accept();
            peer.Close();

            Assert.IsTrue(LoopbackServer.WaitUntil(() => conn.IsClosed, 1000), "closure must be observed within 1 s");
            Assert.AreEqual(CloseReason.RemoteClosed, conn.Closure.Reason);
        }

        [Test]
        public void DataSentBeforeRemoteClose_IsStillDelivered()
        {
            using var conn = Connect();
            var peer = _server.Accept();
            peer.Send(new byte[] { 9, 9, 9, 9 });
            peer.Close();

            Assert.IsTrue(LoopbackServer.WaitUntil(() => conn.IsClosed, 1000));
            Assert.AreEqual(new byte[] { 9, 9, 9, 9 }, _sink.Snapshot());
        }

        [Test]
        public void Dispose_IsSilent_AsLocalDispose_AndBothThreadsExit()
        {
            var conn = Connect();
            using var peer = _server.Accept();
            Assert.AreEqual(2, conn.LiveThreads);

            conn.Dispose();

            Assert.IsTrue(conn.IsClosed);
            Assert.AreEqual(CloseReason.LocalDispose, conn.Closure.Reason);
            Assert.IsTrue(LoopbackServer.WaitUntil(() => conn.LiveThreads == 0, 2000), "reader and writer threads must exit");
        }

        [Test]
        public void Dispose_IsIdempotent_AndFirstCloseWins()
        {
            var conn = Connect();
            using var peer = _server.Accept();
            conn.Close(CloseReason.Error, "first");
            conn.Close(CloseReason.RemoteClosed, "second");
            conn.Dispose();
            conn.Dispose();
            Assert.AreEqual(CloseReason.Error, conn.Closure.Reason);
            Assert.AreEqual("first", conn.Closure.Message);
        }

        [Test]
        public void SinkRefusingData_ClosesWithProtocolFault()
        {
            using var conn = Connect();
            using var peer = _server.Accept();
            _sink.Refuse = true;
            peer.Send(new byte[] { 1, 2, 3 });

            Assert.IsTrue(LoopbackServer.WaitUntil(() => conn.IsClosed, 1000));
            Assert.AreEqual(CloseReason.ProtocolFault, conn.Closure.Reason);
        }

        [Test]
        public void ConnectToClosedPort_Throws()
        {
            ushort port = LoopbackServer.ClosedPort();
            Assert.Catch<SocketException>(() =>
                TcpConnection.ConnectAsync("127.0.0.1", port, ConnectionRole.Login, _sink, CancellationToken.None).GetAwaiter().GetResult());
        }

        [Test]
        public void ConnectWithCancelledToken_ThrowsOperationCanceled()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            Assert.Catch<OperationCanceledException>(() =>
                TcpConnection.ConnectAsync("127.0.0.1", _server.Port, ConnectionRole.Login, _sink, cts.Token).GetAwaiter().GetResult());
        }

#if PENDING_T3_7 // enabled by T3.7 when Send exists
        [Test]
        public void EmptySend_ReturnsTrue_AndSendsNothing()
        {
            using var conn = Connect();
            using var peer = _server.Accept();
            Assert.IsTrue(conn.Send(ReadOnlySpan<byte>.Empty));
            Assert.IsTrue(conn.Send(new ZeroSizePacket()));
            Assert.IsTrue(conn.Send(new byte[] { 0x42 }));

            Assert.AreEqual(new byte[] { 0x42 }, LoopbackServer.ReadExactly(peer, 1), "the first byte on the wire is the real packet");
        }

        [Test]
        public void Send_AfterClose_ReturnsFalse_AndDoesNotThrow()
        {
            var conn = Connect();
            using var peer = _server.Accept();
            conn.Dispose();
            Assert.IsFalse(conn.Send(new byte[] { 1 }));
            Assert.IsFalse(conn.Send(new BytePacket(1)));
        }

        [Test]
        public void Send_DeliversBytesToTheServer()
        {
            using var conn = Connect();
            using var peer = _server.Accept();
            Assert.IsTrue(conn.Send(new byte[] { 1, 2, 3 }));
            Assert.IsTrue(conn.Send(new BytePacket(0x77)));
            Assert.AreEqual(new byte[] { 1, 2, 3, 0x77 }, LoopbackServer.ReadExactly(peer, 4));
        }

        private readonly struct ZeroSizePacket : IOutgoingPacket
        {
            public int WireSize => 0;
            public void Serialize(Span<byte> destination) { }
        }

        private readonly struct BytePacket : IOutgoingPacket
        {
            private readonly byte _value;
            public BytePacket(byte value) { _value = value; }
            public int WireSize => 1;
            public void Serialize(Span<byte> destination) { destination[0] = _value; }
        }
#endif
    }
}
#endif
