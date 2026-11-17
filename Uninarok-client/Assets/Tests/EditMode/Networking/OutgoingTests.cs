#if PENDING_T3_7 // T3.6: enabled (guard removed) by T3.7 when the outbox and Send exist.
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using NUnit.Framework;
using Uninarok.Networking.Core.Transport;
using UnityEngine.TestTools.Constraints;

namespace Uninarok.Networking.Tests
{
    // SPEC-0001 FR-6 (no allocation), FR-8 (non-blocking, ordered, never partial); plan 4.4.
    public class OutgoingTests
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

        private TcpConnection Connect()
            => TcpConnection.ConnectAsync("127.0.0.1", _server.Port, ConnectionRole.Map, _sink, CancellationToken.None).GetAwaiter().GetResult();

        // 8 bytes: sequence number and its bitwise complement, so any torn or shifted packet is detectable.
        private readonly struct SeqPacket : IOutgoingPacket
        {
            private readonly uint _seq;
            public SeqPacket(uint seq) { _seq = seq; }
            public int WireSize => 8;
            public void Serialize(Span<byte> d)
            {
                uint inv = ~_seq;
                d[0] = (byte)_seq; d[1] = (byte)(_seq >> 8); d[2] = (byte)(_seq >> 16); d[3] = (byte)(_seq >> 24);
                d[4] = (byte)inv; d[5] = (byte)(inv >> 8); d[6] = (byte)(inv >> 16); d[7] = (byte)(inv >> 24);
            }
        }

        // 12 bytes: sender id, per-sender sequence, six filler bytes equal to the sender id, and a check byte.
        private readonly struct ThreadPacket : IOutgoingPacket
        {
            private readonly byte _sender;
            private readonly uint _seq;
            public ThreadPacket(byte sender, uint seq) { _sender = sender; _seq = seq; }
            public int WireSize => 12;
            public void Serialize(Span<byte> d)
            {
                d[0] = _sender;
                d[1] = (byte)_seq; d[2] = (byte)(_seq >> 8); d[3] = (byte)(_seq >> 16); d[4] = (byte)(_seq >> 24);
                for (int i = 5; i < 11; i++) d[i] = _sender;
                d[11] = (byte)(_sender ^ 0x5A);
            }
        }

        private readonly struct ThrowingPacket : IOutgoingPacket
        {
            public int WireSize => 4;
            public void Serialize(Span<byte> destination)
            {
                destination[0] = 0xDE; destination[1] = 0xAD;     // partial garbage must not reach the wire
                throw new InvalidOperationException("serialize failed");
            }
        }

        private readonly struct BigPacket : IOutgoingPacket
        {
            private readonly int _size;
            public BigPacket(int size) { _size = size; }
            public int WireSize => _size;
            public void Serialize(Span<byte> destination) { destination.Clear(); }
        }

        [Test]
        public void TenThousandSends_ArriveCompleteAndInOrder()
        {
            using var conn = Connect();
            using var peer = _server.Accept();
            const int count = 10_000;
            for (uint i = 0; i < count; i++) Assert.IsTrue(conn.Send(new SeqPacket(i)));

            var bytes = LoopbackServer.ReadExactly(peer, count * 8, 10_000);
            for (uint i = 0; i < count; i++)
            {
                uint seq = BitConverter.ToUInt32(bytes, (int)i * 8);
                uint inv = BitConverter.ToUInt32(bytes, (int)i * 8 + 4);
                Assert.AreEqual(i, seq, $"packet {i} out of order");
                Assert.AreEqual(~seq, inv, $"packet {i} torn");
            }
        }

        [Test]
        public void FourConcurrentSenders_ProduceNoPartialOrInterleavedPackets()
        {
            using var conn = Connect();
            using var peer = _server.Accept();
            const int perThread = 2_000;
            var threads = new List<Thread>();
            for (byte t = 1; t <= 4; t++)
            {
                byte id = t;
                var th = new Thread(() =>
                {
                    for (uint i = 0; i < perThread; i++) conn.Send(new ThreadPacket(id, i));
                });
                threads.Add(th);
            }
            foreach (var th in threads) th.Start();
            foreach (var th in threads) th.Join();

            var bytes = LoopbackServer.ReadExactly(peer, 4 * perThread * 12, 10_000);
            var next = new uint[5];
            for (int off = 0; off < bytes.Length; off += 12)
            {
                byte sender = bytes[off];
                Assert.That(sender, Is.InRange((byte)1, (byte)4), $"bad sender id at offset {off}");
                for (int i = 5; i < 11; i++) Assert.AreEqual(sender, bytes[off + i], $"interleaved bytes at offset {off}");
                Assert.AreEqual((byte)(sender ^ 0x5A), bytes[off + 11], $"bad check byte at offset {off}");
                uint seq = BitConverter.ToUInt32(bytes, off + 1);
                Assert.AreEqual(next[sender], seq, $"sender {sender} out of order at offset {off}");
                next[sender]++;
            }
            for (int t = 1; t <= 4; t++) Assert.AreEqual((uint)perThread, next[t]);
        }

        [Test]
        public void Send_OfAFixedPacket_AllocatesNothing_AfterWarmUp()
        {
            using var conn = Connect();
            using var peer = _server.Accept();
            var packet = new SeqPacket(1);
            for (int i = 0; i < 20; i++) conn.Send(in packet);                // warm-up (JIT, first signal)
            LoopbackServer.ReadExactly(peer, 20 * 8);

            Assert.That(() => { conn.Send(in packet); }, UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void ThrowingSerialize_LeavesNoPartialBytes_AndTheConnectionStaysUsable()
        {
            using var conn = Connect();
            using var peer = _server.Accept();
            Assert.Throws<InvalidOperationException>(() => conn.Send(new ThrowingPacket()));
            Assert.IsTrue(conn.Send(new SeqPacket(7)));
            Assert.IsFalse(conn.IsClosed);

            var bytes = LoopbackServer.ReadExactly(peer, 8);
            Assert.AreEqual(7u, BitConverter.ToUInt32(bytes, 0), "the first bytes on the wire are the next good packet");
        }

        [Test]
        public void BacklogOverflow_ClosesWithError_AndSendReturnsFalse()
        {
            using var conn = Connect();
            using var peer = _server.Accept();          // the server never reads, so the backlog builds up
            bool refused = false;
            for (int i = 0; i < 400 && !refused; i++)
                refused = !conn.Send(new BigPacket(64 * 1024));

            Assert.IsTrue(refused, "Send must report failure once the backlog limit is hit");
            Assert.IsTrue(conn.IsClosed);
            Assert.AreEqual(CloseReason.Error, conn.Closure.Reason);
            StringAssert.Contains("backlog", conn.Closure.Message);
            Assert.IsFalse(conn.Send(new SeqPacket(1)), "later sends fail without throwing");
        }

        [Test]
        public void SinglePacketBiggerThanTheBacklogLimit_ClosesWithError()
        {
            using var conn = Connect();
            using var peer = _server.Accept();
            Assert.IsFalse(conn.Send(new BigPacket(2 * 1024 * 1024)));
            Assert.AreEqual(CloseReason.Error, conn.Closure.Reason);
        }

        [Test]
        public void SendBytes_AndSendStruct_KeepCallOrder()
        {
            using var conn = Connect();
            using var peer = _server.Accept();
            Assert.IsTrue(conn.Send(new byte[] { 1, 2 }));
            Assert.IsTrue(conn.Send(new SeqPacket(3)));
            Assert.IsTrue(conn.Send(new byte[] { 9 }));
            var bytes = LoopbackServer.ReadExactly(peer, 2 + 8 + 1);
            Assert.AreEqual(1, bytes[0]);
            Assert.AreEqual(2, bytes[1]);
            Assert.AreEqual(3u, BitConverter.ToUInt32(bytes, 2));
            Assert.AreEqual(9, bytes[10]);
        }
    }
}
#endif
