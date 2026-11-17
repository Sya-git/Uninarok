using System;
using System.Collections.Generic;
using NUnit.Framework;
using Uninarok.Networking.Core.Framing;

namespace Uninarok.Networking.Tests
{
    // SPEC-0001 FR-1: packets are read correctly under any TCP segmentation.
    public class FramerFramingTests
    {
        const ushort FixedId = 0x0010, EmptyId = 0x0020, VarId = 0x0030;

        static TableStub Table() => new TableStub().Fixed(FixedId, 6).Fixed(EmptyId, 2).Variable(VarId);

        static byte[] MixedStream() => Wire.Concat(
            Wire.Fixed(FixedId, 1, 2, 3, 4),
            Wire.Var(VarId, 9, 8, 7),
            Wire.Fixed(EmptyId),
            Wire.Var(VarId),                         // variable packet with an empty payload (total length 4)
            Wire.Fixed(FixedId, 0xFF, 0xFE, 0xFD, 0xFC),
            Wire.Var(VarId, Wire.Repeat(new byte[] { 0xAB }, 300)));

        static void AssertSame(List<(ushort Id, byte[] Payload)> expected, List<(ushort Id, byte[] Payload)> actual, string what)
        {
            Assert.AreEqual(expected.Count, actual.Count, what + ": packet count");
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.AreEqual(expected[i].Id, actual[i].Id, $"{what}: id of packet {i}");
                Assert.AreEqual(expected[i].Payload, actual[i].Payload, $"{what}: payload of packet {i}");
            }
        }

        [Test]
        public void FixedPacket_IsRouted_WithPayloadExcludingId()
        {
            var packets = FramerHarness.Run(Table(), Wire.Fixed(FixedId, 1, 2, 3, 4), 0);
            Assert.AreEqual(1, packets.Count);
            Assert.AreEqual(FixedId, packets[0].Id);
            Assert.AreEqual(new byte[] { 1, 2, 3, 4 }, packets[0].Payload);
        }

        [Test]
        public void VariablePacket_PayloadExcludesIdAndLength()
        {
            var packets = FramerHarness.Run(Table(), Wire.Var(VarId, 9, 8, 7), 0);
            Assert.AreEqual(1, packets.Count);
            Assert.AreEqual(new byte[] { 9, 8, 7 }, packets[0].Payload);
        }

        [Test]
        public void PacketWithoutPayload_IsRouted_WithEmptyPayload()
        {
            var packets = FramerHarness.Run(Table(), Wire.Fixed(EmptyId), 0);
            Assert.AreEqual(1, packets.Count);
            Assert.AreEqual(0, packets[0].Payload.Length);
        }

        [Test]
        public void Coalesced_ManyPacketsInOneAppend_AreRoutedInOrder()
        {
            var stream = MixedStream();
            var packets = FramerHarness.Run(Table(), stream, 0);
            Assert.AreEqual(6, packets.Count);
            CollectionAssert.AreEqual(new ushort[] { FixedId, VarId, EmptyId, VarId, FixedId, VarId }, packets.ConvertAll(p => p.Id));
            Assert.AreEqual(300, packets[5].Payload.Length);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(7)]
        [TestCase(64)]
        public void SameStream_InChunks_YieldsIdenticalPackets(int chunk)
        {
            var stream = MixedStream();
            var whole = FramerHarness.Run(Table(), stream, 0);
            var chunked = FramerHarness.Run(Table(), stream, chunk);
            AssertSame(whole, chunked, $"chunk={chunk}");
        }

        [Test]
        public void SameStream_SplitAtEveryByteOffset_YieldsIdenticalPackets()
        {
            var stream = MixedStream();
            var whole = FramerHarness.Run(Table(), stream, 0);
            for (int split = 0; split <= stream.Length; split++)
            {
                var router = new RecordingRouter();
                var framer = new PacketFramer(Table(), router);
                Assert.IsTrue(framer.Append(new ReadOnlySpan<byte>(stream, 0, split)));
                framer.Process(int.MaxValue);
                Assert.IsTrue(framer.Append(new ReadOnlySpan<byte>(stream, split, stream.Length - split)));
                framer.Process(int.MaxValue);
                AssertSame(whole, router.Packets, $"split at {split}");
            }
        }

        [Test]
        public void PartialHeader_WaitsForMoreData()
        {
            var router = new RecordingRouter();
            var framer = new PacketFramer(Table(), router);
            var pkt = Wire.Var(VarId, 1, 2, 3);

            framer.Append(new ReadOnlySpan<byte>(pkt, 0, 1));          // half an ID
            Assert.AreEqual(0, framer.Process(10));
            framer.Append(new ReadOnlySpan<byte>(pkt, 1, 2));          // ID + half of the length field
            Assert.AreEqual(0, framer.Process(10));
            framer.Append(new ReadOnlySpan<byte>(pkt, 3, 2));          // length known, payload incomplete
            Assert.AreEqual(0, framer.Process(10));
            framer.Append(new ReadOnlySpan<byte>(pkt, 5, 2));
            Assert.AreEqual(1, framer.Process(10));
            Assert.AreEqual(1, router.Packets.Count);
            Assert.IsFalse(framer.IsFaulted);
        }

        [Test]
        public void Process_ReturnsNumberOfPacketsFramed_AndZeroWhenIdle()
        {
            var router = new RecordingRouter();
            var framer = new PacketFramer(Table(), router);
            Assert.AreEqual(0, framer.Process(10));
            framer.Append(MixedStream());
            Assert.AreEqual(6, framer.Process(100));
            Assert.AreEqual(0, framer.Process(100));
        }

        [Test]
        public void BackToBackPackets_AcrossManyProcessCalls_KeepAlignment()
        {
            var stream = Wire.Repeat(Wire.Fixed(FixedId, 1, 2, 3, 4), 50);
            var packets = FramerHarness.Run(Table(), stream, 5, maxPerProcess: 3);
            Assert.AreEqual(50, packets.Count);
            foreach (var p in packets) Assert.AreEqual(new byte[] { 1, 2, 3, 4 }, p.Payload);
        }
    }
}
