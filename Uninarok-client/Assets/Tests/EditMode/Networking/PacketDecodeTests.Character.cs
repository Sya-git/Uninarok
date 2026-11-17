using System;
using NUnit.Framework;
using Unity.Collections;
using Uninarok.Networking.Core.Wire;
using Uninarok.Networking.Data;
using Uninarok.Networking.Packets;
using Uninarok.Networking.Packets.In.Character;
using Uninarok.Networking.Packets.Out.Character;

namespace Uninarok.Networking.Tests
{
    // SPEC-0001 FR-18: character-server packets decode their sample with nothing left over; outgoing ones write golden bytes.
    public partial class PacketDecodeTests
    {
        static byte[] CharInfoBytes(int charId, string name, short slot)
        {
            var c = new CharInfo { CharId = charId, Name = new FixedString32Bytes(name), MapName = new FixedString32Bytes("prontera"), Slot = slot, Sex = AccountSex.SEX_MALE, BaseLevel = 42 };
            var buf = new byte[CharInfo.WireSize];
            var w = new PacketWriter(buf);
            c.Write(ref w);
            Assert.AreEqual(CharInfo.WireSize, w.Position);
            return buf;
        }

        static byte[] SizedBuffer(int totalPacketSize, Action<byte[]> fill = null)
        {
            var b = new byte[totalPacketSize - 2];
            fill?.Invoke(b);
            return b;
        }

        // ---- CHARACTER_SERVER_REQUEST_ENTRY (0x0065, 17), out ----

        [Test]
        public void CHARACTER_SERVER_REQUEST_ENTRY_WritesGoldenBytes()
        {
            var p = new CHARACTER_SERVER_REQUEST_ENTRY(2000001, 0x11223344, 99, AccountSex.SEX_MALE);
            var buf = new byte[p.WireSize];
            p.Serialize(buf);
            Assert.AreEqual(17, p.WireSize);
            Assert.AreEqual(new byte[]
            {
                0x65, 0x00,                 // id
                0x81, 0x84, 0x1E, 0x00,     // AccountId 2000001
                0x44, 0x33, 0x22, 0x11,     // AuthCode
                0x63, 0x00, 0x00, 0x00,     // UserLevel 99
                0x00, 0x00,                 // reserved
                0x01,                       // Sex
            }, buf);
        }

        // ---- CHARACTER_SERVER_PING (0x0187, 6), out ----

        [Test]
        public void CHARACTER_SERVER_PING_WritesGoldenBytes()
        {
            var p = new CHARACTER_SERVER_PING(2000001);
            var buf = new byte[p.WireSize];
            p.Serialize(buf);
            Assert.AreEqual(6, p.WireSize);
            Assert.AreEqual(new byte[] { 0x87, 0x01, 0x81, 0x84, 0x1E, 0x00 }, buf);
        }

        // ---- SELECT_CHARACTER (0x0066, 3), out ----

        [Test]
        public void SELECT_CHARACTER_WritesGoldenBytes()
        {
            var p = new SELECT_CHARACTER(2);
            var buf = new byte[p.WireSize];
            p.Serialize(buf);
            Assert.AreEqual(3, p.WireSize);
            Assert.AreEqual(new byte[] { 0x66, 0x00, 0x02 }, buf);
        }

        // ---- CREATE_CHARACTER (0x0A39, 36), out ----

        [Test]
        public void CREATE_CHARACTER_WritesGoldenBytes()
        {
            var p = new CREATE_CHARACTER(new FixedString32Bytes("Hero"), 1, 3, 7, 4001, AccountSex.SEX_MALE);
            var buf = new byte[p.WireSize];
            p.Serialize(buf);
            Assert.AreEqual(36, p.WireSize);

            var expected = new byte[36];
            expected[0] = 0x39; expected[1] = 0x0A;
            expected[2] = (byte)'H'; expected[3] = (byte)'e'; expected[4] = (byte)'r'; expected[5] = (byte)'o';   // name, 24 bytes from offset 2
            expected[26] = 1;                                       // Slot
            expected[27] = 3; expected[28] = 0;                     // HairColor
            expected[29] = 7; expected[30] = 0;                     // HairStyle
            expected[31] = 0xA1; expected[32] = 0x0F; expected[33] = 0; expected[34] = 0;   // JobClass 4001, four bytes
            expected[35] = 1;                                       // Sex
            Assert.AreEqual(expected, buf);
        }

        [Test]
        public void CREATE_CHARACTER_ReplacesNonAsciiNameCharactersWithQuestionMarks()
        {
            var p = new CREATE_CHARACTER(new FixedString32Bytes("Héro"), 0, 0, 0, 0, AccountSex.SEX_FEMALE);
            var buf = new byte[p.WireSize];
            p.Serialize(buf);
            Assert.AreEqual((byte)'H', buf[2]);
            Assert.AreEqual((byte)'?', buf[3]);
            Assert.AreEqual((byte)'r', buf[4]);
        }

        [Test]
        public void CREATE_CHARACTER_TruncatesALongNameTo23Characters()
        {
            var p = new CREATE_CHARACTER(new FixedString32Bytes("abcdefghijklmnopqrstuvwxyz"), 5, 0, 0, 0, AccountSex.SEX_MALE);
            var buf = new byte[p.WireSize];
            p.Serialize(buf);
            Assert.AreEqual((byte)'w', buf[2 + 22]);
            Assert.AreEqual(0, buf[2 + 23], "NUL terminator");
            Assert.AreEqual(5, buf[26], "the slot starts exactly after the 24-byte name");
        }

        // ---- MAP_ENTER_REQUEST_REFUSED (0x006C, 3) ----

        [Test]
        public void MAP_ENTER_REQUEST_REFUSED_Decodes_AndRejectsWrongLengths()
        {
            Assert.AreEqual(0, new MAP_ENTER_REQUEST_REFUSED(new byte[] { 0 }).ErrorCode);
            Assert.Throws<PacketFormatException>(() => new MAP_ENTER_REQUEST_REFUSED(ReadOnlySpan<byte>.Empty));
            Assert.Throws<PacketFormatException>(() => new MAP_ENTER_REQUEST_REFUSED(new byte[2]));
        }

        // ---- CHARACTER_CREATION_FAILED (0x006E, 3) ----

        [TestCase((byte)0x00)]
        [TestCase((byte)0x02)]
        [TestCase((byte)0xFF)]
        public void CHARACTER_CREATION_FAILED_Decodes_ErrorCode(byte code)
        {
            Assert.AreEqual(code, new CHARACTER_CREATION_FAILED(new byte[] { code }).ErrorCode);
        }

        [Test]
        public void CHARACTER_CREATION_FAILED_RejectsWrongLengths()
        {
            Assert.Throws<PacketFormatException>(() => new CHARACTER_CREATION_FAILED(ReadOnlySpan<byte>.Empty));
            Assert.Throws<PacketFormatException>(() => new CHARACTER_CREATION_FAILED(new byte[2]));
        }

        // ---- CHARACTER_CREATION_SUCCESS (0x0B6F, 177 = 2 + the 175-byte CharInfo; 0x006D is the pre-20201007 ID) ----

        [Test]
        public void CHARACTER_CREATION_SUCCESS_Decodes_TheCharInfo()
        {
            var payload = CharInfoBytes(150001, "Poring", 2);
            Assert.AreEqual(CHARACTER_CREATION_SUCCESS.Size - 2, payload.Length);
            var p = new CHARACTER_CREATION_SUCCESS(payload);
            Assert.AreEqual(150001, p.Character.CharId);
            Assert.AreEqual("Poring", p.Character.Name.ToString());
            Assert.AreEqual(2, p.Character.Slot);
        }

        [Test]
        public void CHARACTER_CREATION_SUCCESS_RejectsWrongLengths()
        {
            Assert.Throws<PacketFormatException>(() => new CHARACTER_CREATION_SUCCESS(new byte[174]));
            Assert.Throws<PacketFormatException>(() => new CHARACTER_CREATION_SUCCESS(new byte[176]));
        }

        // ---- PINCODE_LOGINSTATE (0x08B9, 12) ----

        [Test]
        public void PINCODE_LOGINSTATE_Decodes()
        {
            var payload = new byte[] { 0x78, 0x56, 0x34, 0x12, 0x81, 0x84, 0x1E, 0x00, 0x02, 0x00 };
            Assert.AreEqual(PINCODE_LOGINSTATE.Size - 2, payload.Length);
            var p = new PINCODE_LOGINSTATE(payload);
            Assert.AreEqual(0x12345678, p.PincodeSeed);
            Assert.AreEqual(2000001, p.AccountId);
            Assert.AreEqual(2, p.State);
        }

        [Test]
        public void PINCODE_LOGINSTATE_RejectsWrongLengths()
        {
            Assert.Throws<PacketFormatException>(() => new PINCODE_LOGINSTATE(new byte[9]));
            Assert.Throws<PacketFormatException>(() => new PINCODE_LOGINSTATE(new byte[11]));
        }

        // ---- CHARACTER_SLOTS_INFO (0x082D, variable, 25 payload bytes) ----

        [Test]
        public void CHARACTER_SLOTS_INFO_Decodes()
        {
            var payload = new byte[25];
            payload[0] = 9; payload[1] = 6; payload[2] = 1; payload[3] = 2; payload[4] = 3;
            for (int i = 5; i < 25; i++) payload[i] = 0xCC;
            var p = new CHARACTER_SLOTS_INFO(payload);
            Assert.AreEqual((9, 6, 1, 2, 3), ((int)p.CharSlots, (int)p.AvailableCharSlots, (int)p.Unknown, (int)p.CharSlots2, (int)p.CharSlots3));
        }

        [Test]
        public void CHARACTER_SLOTS_INFO_RejectsWrongLengths()
        {
            Assert.Throws<PacketFormatException>(() => new CHARACTER_SLOTS_INFO(new byte[24]));
            Assert.Throws<PacketFormatException>(() => new CHARACTER_SLOTS_INFO(new byte[26]));
        }

        // ---- CHARACTER_DATA (0x006B, variable) ----

        static byte[] CharacterDataPayload(params byte[][] records)
        {
            var meta = new byte[23];
            meta[0] = 12; meta[1] = 9; meta[2] = 3;
            return Wire.Concat(meta, Wire.Concat(records));
        }

        [Test]
        public void CHARACTER_DATA_Decodes_MetadataAndRecords()
        {
            var p = new CHARACTER_DATA(CharacterDataPayload(CharInfoBytes(1, "One", 0), CharInfoBytes(2, "Two", 1)));
            Assert.AreEqual((12, 9, 3), ((int)p.MaxChars, (int)p.AvailableSlots, (int)p.PremiumSlots));
            Assert.AreEqual(2, p.Characters.Length);
            Assert.AreEqual("One", p.Characters[0].Name.ToString());
            Assert.AreEqual(2, p.Characters[1].CharId);
            Assert.AreEqual(1, p.Characters[1].Slot);
        }

        [Test]
        public void CHARACTER_DATA_WithoutRecords_HasAnEmptyList()
        {
            Assert.AreEqual(0, new CHARACTER_DATA(CharacterDataPayload()).Characters.Length);
        }

        [Test]
        public void CHARACTER_DATA_RejectsARecordListThatIsNotWholeRecords()
        {
            var payload = Wire.Concat(CharacterDataPayload(CharInfoBytes(1, "One", 0)), new byte[] { 0 });
            Assert.Throws<PacketFormatException>(() => new CHARACTER_DATA(payload));
            Assert.Throws<PacketFormatException>(() => new CHARACTER_DATA(new byte[22]));
        }

        // ---- MAP_ENTER_REQUEST_ACCEPTED (0x0AC5, 156) ----

        [Test]
        public void MAP_ENTER_REQUEST_ACCEPTED_Decodes_AndKeepsHighPortsPositive()
        {
            var payload = new byte[MAP_ENTER_REQUEST_ACCEPTED.Size - 2];
            payload[0] = 0x31; payload[1] = 0x25; payload[2] = 0x02; payload[3] = 0x00;            // CharId 140593
            var map = Text("prontera", 16); Array.Copy(map, 0, payload, 4, 16);
            payload[20] = 127; payload[21] = 0; payload[22] = 0; payload[23] = 1;                   // 127.0.0.1
            payload[24] = 0x40; payload[25] = 0x9C;                                                  // port 40000 (> 32767)
            var dns = Text("localhost", 128); Array.Copy(dns, 0, payload, 26, 128);

            var p = new MAP_ENTER_REQUEST_ACCEPTED(payload);
            Assert.AreEqual(140593, p.CharId);
            Assert.AreEqual("prontera", p.MapName.ToString());
            Assert.AreEqual("127.0.0.1", p.FormatIpAddress());
            Assert.AreEqual(40000, p.Port);
            Assert.AreEqual("localhost", p.DnsHost.ToString());
        }

        [Test]
        public void MAP_ENTER_REQUEST_ACCEPTED_HoldsA127CharacterDnsHost()
        {
            var payload = new byte[MAP_ENTER_REQUEST_ACCEPTED.Size - 2];
            for (int i = 26; i < 26 + 127; i++) payload[i] = (byte)'a';                              // 127 characters + NUL
            var p = new MAP_ENTER_REQUEST_ACCEPTED(payload);
            Assert.AreEqual(127, p.DnsHost.Length);
        }

        [Test]
        public void MAP_ENTER_REQUEST_ACCEPTED_RejectsWrongLengths()
        {
            Assert.Throws<PacketFormatException>(() => new MAP_ENTER_REQUEST_ACCEPTED(new byte[153]));
            Assert.Throws<PacketFormatException>(() => new MAP_ENTER_REQUEST_ACCEPTED(new byte[155]));
        }

        // ---- ids and sizes ----

        [Test]
        public void CharacterServerPackets_SizesAndIds()
        {
            Assert.AreEqual((0x0065, 17), ((int)PacketIds.CHARACTER_SERVER_REQUEST_ENTRY, CHARACTER_SERVER_REQUEST_ENTRY.Size));
            Assert.AreEqual((0x0066, 3), ((int)PacketIds.SELECT_CHARACTER, SELECT_CHARACTER.Size));
            Assert.AreEqual((0x006B, -1), ((int)PacketIds.CHARACTER_DATA, CHARACTER_DATA.Size));
            Assert.AreEqual((0x006C, 3), ((int)PacketIds.MAP_ENTER_REQUEST_REFUSED, MAP_ENTER_REQUEST_REFUSED.Size));
            Assert.AreEqual((0x0B6F, 177), ((int)PacketIds.CHARACTER_CREATION_SUCCESS, CHARACTER_CREATION_SUCCESS.Size));
            Assert.AreEqual((0x006E, 3), ((int)PacketIds.CHARACTER_CREATION_FAILED, CHARACTER_CREATION_FAILED.Size));
            Assert.AreEqual((0x0187, 6), ((int)PacketIds.CHARACTER_SERVER_PING, CHARACTER_SERVER_PING.Size));
            Assert.AreEqual((0x082D, -1), ((int)PacketIds.CHARACTER_SLOTS_INFO, CHARACTER_SLOTS_INFO.Size));
            Assert.AreEqual((0x08B9, 12), ((int)PacketIds.PINCODE_LOGINSTATE, PINCODE_LOGINSTATE.Size));
            Assert.AreEqual((0x0A39, 36), ((int)PacketIds.CREATE_CHARACTER, CREATE_CHARACTER.Size));
            Assert.AreEqual((0x0AC5, 156), ((int)PacketIds.MAP_ENTER_REQUEST_ACCEPTED, MAP_ENTER_REQUEST_ACCEPTED.Size));
        }
    }
}
