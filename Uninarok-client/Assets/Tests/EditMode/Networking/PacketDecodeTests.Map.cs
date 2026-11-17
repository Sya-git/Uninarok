using System;
using NUnit.Framework;
using Uninarok.Networking.Core.Wire;
using Uninarok.Networking.Data;
using Uninarok.Networking.Packets;
using Uninarok.Networking.Packets.In.Map;
using Uninarok.Networking.Packets.Out.Map;

namespace Uninarok.Networking.Tests
{
    // SPEC-0001 FR-18, FR-14: map-server packets decode their sample with nothing left over; outgoing ones write golden bytes.
    public partial class PacketDecodeTests
    {
        // ---- REQUEST_ENTER_MAP (0x0436, 23), out ----

        [Test]
        public void REQUEST_ENTER_MAP_WritesGoldenBytes()
        {
            var p = new REQUEST_ENTER_MAP(2000001, 140593, 0x11223344, 12345678, AccountSex.SEX_MALE);
            var buf = new byte[p.WireSize];
            p.Serialize(buf);
            Assert.AreEqual(23, p.WireSize);
            Assert.AreEqual(new byte[]
            {
                0x36, 0x04,                 // id 0x0436
                0x81, 0x84, 0x1E, 0x00,     // AccountId (offset 2)
                0x31, 0x25, 0x02, 0x00,     // CharId (offset 6)
                0x44, 0x33, 0x22, 0x11,     // AuthCode (offset 10)
                0x00, 0x00, 0x00, 0x00,     // reserved (offsets 14 to 17)
                0x4E, 0x61, 0xBC, 0x00,     // Timestamp 12345678 (offset 18)
                0x01,                       // Sex (offset 22)
            }, buf);
        }

        // ---- HEARTBEAT_REQUEST (0x0360, 6), out ----

        [Test]
        public void HEARTBEAT_REQUEST_WritesGoldenBytes()
        {
            var p = new HEARTBEAT_REQUEST(0x01020304);
            var buf = new byte[p.WireSize];
            p.Serialize(buf);
            Assert.AreEqual(6, p.WireSize);
            Assert.AreEqual(new byte[] { 0x60, 0x03, 0x04, 0x03, 0x02, 0x01 }, buf);
        }

        // ---- MAP_LOADED (0x007D, 2), out ----

        [Test]
        public void MAP_LOADED_WritesTheHeaderOnly()
        {
            var p = new MAP_LOADED();
            var buf = new byte[p.WireSize];
            p.Serialize(buf);
            Assert.AreEqual(2, p.WireSize);
            Assert.AreEqual(new byte[] { 0x7D, 0x00 }, buf);
        }

        // ---- MAP_ENTRY_ACCEPTANCE (0x0283, 6) ----

        [Test]
        public void MAP_ENTRY_ACCEPTANCE_Decodes_AndRejectsWrongLengths()
        {
            Assert.AreEqual(0x0102A381u, new MAP_ENTRY_ACCEPTANCE(new byte[] { 0x81, 0xA3, 0x02, 0x01 }).CharacterObjectId);
            Assert.Throws<PacketFormatException>(() => new MAP_ENTRY_ACCEPTANCE(new byte[3]));
            Assert.Throws<PacketFormatException>(() => new MAP_ENTRY_ACCEPTANCE(new byte[5]));
        }

        // ---- MAP_ENTRY_REFUSAL (0x0074, 3) ----

        [Test]
        public void MAP_ENTRY_REFUSAL_Decodes_AndRejectsWrongLengths()
        {
            Assert.AreEqual(3, new MAP_ENTRY_REFUSAL(new byte[] { 3 }).ErrorCode);
            Assert.Throws<PacketFormatException>(() => new MAP_ENTRY_REFUSAL(ReadOnlySpan<byte>.Empty));
            Assert.Throws<PacketFormatException>(() => new MAP_ENTRY_REFUSAL(new byte[2]));
        }

        // ---- SERVER_HEARTBEAT_RESPONSE (0x007F, 6) ----

        [Test]
        public void SERVER_HEARTBEAT_RESPONSE_Decodes_AndRejectsWrongLengths()
        {
            Assert.AreEqual(0xDEADBEEFu, new SERVER_HEARTBEAT_RESPONSE(new byte[] { 0xEF, 0xBE, 0xAD, 0xDE }).ServerTimestamp);
            Assert.Throws<PacketFormatException>(() => new SERVER_HEARTBEAT_RESPONSE(new byte[3]));
            Assert.Throws<PacketFormatException>(() => new SERVER_HEARTBEAT_RESPONSE(new byte[5]));
        }

        // ---- AUTHENTICATION_ACCEPTED (0x02EB, 13) ----

        [Test]
        public void AUTHENTICATION_ACCEPTED_DecodesTheDocSample()
        {
            // doc sample, full packet: EB 02 04 03 02 01 25 8C 84 05 05 00 00 (the payload excludes the 2-byte id)
            var payload = new byte[] { 0x04, 0x03, 0x02, 0x01, 0x25, 0x8C, 0x84, 0x05, 0x05, 0x00, 0x00 };
            Assert.AreEqual(AUTHENTICATION_ACCEPTED.Size - 2, payload.Length);
            var p = new AUTHENTICATION_ACCEPTED(payload);
            Assert.AreEqual(0x01020304u, p.StartTimestamp);
            Assert.AreEqual(150, p.Position.X);
            Assert.AreEqual(200, p.Position.Y);
            Assert.AreEqual(UninarokDirection.South, p.Position.Direction);
            Assert.AreEqual(5, p.XSize);
            Assert.AreEqual(5, p.YSize);
            Assert.AreEqual(0, p.Font);
        }

        [Test]
        public void AUTHENTICATION_ACCEPTED_RejectsWrongLengths()
        {
            Assert.Throws<PacketFormatException>(() => new AUTHENTICATION_ACCEPTED(new byte[10]));
            Assert.Throws<PacketFormatException>(() => new AUTHENTICATION_ACCEPTED(new byte[12]));
        }

        // ---- ids and sizes ----

        [Test]
        public void MapServerPackets_SizesAndIds()
        {
            Assert.AreEqual((0x0074, 3), ((int)PacketIds.MAP_ENTRY_REFUSAL, MAP_ENTRY_REFUSAL.Size));
            Assert.AreEqual((0x007D, 2), ((int)PacketIds.MAP_LOADED, MAP_LOADED.Size));
            Assert.AreEqual((0x007F, 6), ((int)PacketIds.SERVER_HEARTBEAT_RESPONSE, SERVER_HEARTBEAT_RESPONSE.Size));
            Assert.AreEqual((0x0283, 6), ((int)PacketIds.MAP_ENTRY_ACCEPTANCE, MAP_ENTRY_ACCEPTANCE.Size));
            Assert.AreEqual((0x02EB, 13), ((int)PacketIds.AUTHENTICATION_ACCEPTED, AUTHENTICATION_ACCEPTED.Size));
            Assert.AreEqual((0x0360, 6), ((int)PacketIds.HEARTBEAT_REQUEST, HEARTBEAT_REQUEST.Size));
            Assert.AreEqual((0x0436, 23), ((int)PacketIds.REQUEST_ENTER_MAP, REQUEST_ENTER_MAP.Size));
        }
    }
}
