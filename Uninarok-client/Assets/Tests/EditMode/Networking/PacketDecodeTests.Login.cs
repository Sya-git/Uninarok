using System;
using NUnit.Framework;
using Unity.Collections;
using Uninarok.Networking.Core.Wire;
using Uninarok.Networking.Data;
using Uninarok.Networking.Flow;
using Uninarok.Networking.Packets;
using Uninarok.Networking.Packets.In.Login;
using Uninarok.Networking.Packets.Out.Login;

namespace Uninarok.Networking.Tests
{
    // SPEC-0001 FR-18: login-server packets decode their sample with nothing left over, and outgoing ones write golden bytes.
    public partial class PacketDecodeTests
    {
        static byte[] Text(string s, int wireBytes)
        {
            var b = new byte[wireBytes];
            for (int i = 0; i < s.Length; i++) b[i] = (byte)s[i];
            return b;
        }

        // ---- LOGIN_SERVER_NOTICE (0x0081, 3) ----

        [Test]
        public void LOGIN_SERVER_NOTICE_Decodes()
        {
            var p = new LOGIN_SERVER_NOTICE(new byte[] { 8 });
            Assert.AreEqual(8, p.ErrorCode);
        }

        [Test]
        public void LOGIN_SERVER_NOTICE_RejectsShortAndLongPayloads()
        {
            Assert.Throws<PacketFormatException>(() => new LOGIN_SERVER_NOTICE(ReadOnlySpan<byte>.Empty));
            Assert.Throws<PacketFormatException>(() => new LOGIN_SERVER_NOTICE(new byte[] { 1, 2 }));
        }

        [Test]
        public void LOGIN_SERVER_NOTICE_SizeAndId()
        {
            Assert.AreEqual(3, LOGIN_SERVER_NOTICE.Size);
            Assert.AreEqual(0x0081, PacketIds.LOGIN_SERVER_NOTICE);
        }

        // ---- LOGIN_REFUSAL (0x006A, 23) ----

        [Test]
        public void LOGIN_REFUSAL_Decodes_ErrorCodeAndBlockDate()
        {
            var payload = Wire.Concat(new byte[] { 6 }, Text("2026-12-31 23:59:59", 20));
            Assert.AreEqual(LOGIN_REFUSAL.Size - 2, payload.Length);
            var p = new LOGIN_REFUSAL(payload);
            Assert.AreEqual(6, p.ErrorCode);
            Assert.AreEqual("2026-12-31 23:59:59", p.BlockDate.ToString());
        }

        [Test]
        public void LOGIN_REFUSAL_WithoutBan_HasAnEmptyDate()
        {
            var p = new LOGIN_REFUSAL(Wire.Concat(new byte[] { 3 }, new byte[20]));
            Assert.AreEqual(3, p.ErrorCode);
            Assert.AreEqual(0, p.BlockDate.Length);
        }

        [Test]
        public void LOGIN_REFUSAL_RejectsWrongLengths()
        {
            Assert.Throws<PacketFormatException>(() => new LOGIN_REFUSAL(new byte[20]));
            Assert.Throws<PacketFormatException>(() => new LOGIN_REFUSAL(new byte[22]));
        }

        // ---- REFUSE_LOGIN_3 (0x0B02, 26) ----

        [Test]
        public void REFUSE_LOGIN_3_Decodes_ErrorCodeAndBlockDate()
        {
            var payload = Wire.Concat(new byte[] { 0x05, 0x01, 0x00, 0x00 }, Text("2027-01-01", 20));
            Assert.AreEqual(REFUSE_LOGIN_3.Size - 2, payload.Length);
            var p = new REFUSE_LOGIN_3(payload);
            Assert.AreEqual(0x0105, p.ErrorCode);
            Assert.AreEqual("2027-01-01", p.BlockDate.ToString());
        }

        [Test]
        public void REFUSE_LOGIN_3_RejectsWrongLengths()
        {
            Assert.Throws<PacketFormatException>(() => new REFUSE_LOGIN_3(new byte[23]));
            Assert.Throws<PacketFormatException>(() => new REFUSE_LOGIN_3(new byte[25]));
        }

        // ---- ACCEPT_LOGIN (0x0AC4, variable) ----

        static byte[] ServerEntry(byte[] ip, ushort port, string name, ushort users, ushort state, ushort property)
        {
            var e = new byte[ServerEndpoint.WireSize];
            Array.Copy(ip, 0, e, 0, 4);
            e[4] = (byte)port; e[5] = (byte)(port >> 8);
            Array.Copy(Text(name, 20), 0, e, 6, 20);
            e[26] = (byte)users; e[27] = (byte)(users >> 8);
            e[28] = (byte)state; e[29] = (byte)(state >> 8);
            e[30] = (byte)property; e[31] = (byte)(property >> 8);
            for (int i = 32; i < 160; i++) e[i] = 0xEE;            // the opaque 128-byte region
            return e;
        }

        static byte[] AcceptLoginPayload(params byte[][] servers)
        {
            var fixedPart = new byte[60];
            void Put(int at, uint v) { fixedPart[at] = (byte)v; fixedPart[at + 1] = (byte)(v >> 8); fixedPart[at + 2] = (byte)(v >> 16); fixedPart[at + 3] = (byte)(v >> 24); }
            Put(0, 0x11223344);                 // AuthCode
            Put(4, 2000001);                    // AccountId
            Put(8, 99);                         // UserLevel
            Put(12, 0xAABBCCDD);                // LastLoginIp
            for (int i = 16; i < 42; i++) fixedPart[i] = 0;          // LastLoginTime (26)
            fixedPart[42] = 1;                  // Sex: male
            for (int i = 0; i < 16; i++) fixedPart[43 + i] = (byte)(0xA0 + i);   // AuthToken
            fixedPart[59] = 1;                  // TwitterFlag
            return Wire.Concat(fixedPart, Wire.Concat(servers));
        }

        [Test]
        public void ACCEPT_LOGIN_Decodes_FixedFieldsAndServers()
        {
            var payload = AcceptLoginPayload(
                ServerEntry(new byte[] { 127, 0, 0, 1 }, 6121, "Uninarok", 12, 0, 1),
                ServerEntry(new byte[] { 192, 168, 1, 20 }, 40000, "Second", 3, 1, 2));
            var p = new ACCEPT_LOGIN(payload);

            Assert.AreEqual(0x11223344, p.AuthCode);
            Assert.AreEqual(2000001u, p.AccountId);
            Assert.AreEqual(99u, p.UserLevel);
            Assert.AreEqual(0xAABBCCDDu, p.LastLoginIp);
            Assert.AreEqual(AccountSex.SEX_MALE, p.Sex);
            var token = p.AuthToken;
            Assert.AreEqual(0xA0, token.byte0000);
            Assert.AreEqual(0xAF, token.byte0015);
            Assert.AreEqual(1, p.TwitterFlag);

            Assert.AreEqual(2, p.Servers.Length);
            Assert.AreEqual("127.0.0.1", p.Servers[0].Host);
            Assert.AreEqual(6121, p.Servers[0].Port);
            Assert.AreEqual("Uninarok", p.Servers[0].Name.ToString());
            Assert.AreEqual((12, 0, 1), ((int)p.Servers[0].UserCount, (int)p.Servers[0].State, (int)p.Servers[0].Property));
            Assert.AreEqual("192.168.1.20", p.Servers[1].Host);
            Assert.AreEqual(40000, p.Servers[1].Port, "a port above 32767 stays positive (FR-12)");
            Assert.AreEqual("Second", p.Servers[1].Name.ToString());
        }

        [Test]
        public void ACCEPT_LOGIN_WithoutServers_HasAnEmptyList()
        {
            var p = new ACCEPT_LOGIN(AcceptLoginPayload());
            Assert.AreEqual(0, p.Servers.Length);
        }

        [Test]
        public void ACCEPT_LOGIN_RejectsAServerListThatIsNotWholeEntries()
        {
            var payload = Wire.Concat(AcceptLoginPayload(ServerEntry(new byte[] { 1, 2, 3, 4 }, 1, "x", 0, 0, 0)), new byte[] { 0 });
            Assert.Throws<PacketFormatException>(() => new ACCEPT_LOGIN(payload));
        }

        [Test]
        public void ACCEPT_LOGIN_RejectsATruncatedFixedPart()
        {
            Assert.Throws<PacketFormatException>(() => new ACCEPT_LOGIN(new byte[59]));
        }

        [Test]
        public void ACCEPT_LOGIN_IsVariable()
        {
            Assert.AreEqual(-1, ACCEPT_LOGIN.Size);
            Assert.AreEqual(0x0AC4, PacketIds.ACCEPT_LOGIN);
        }

        // ---- REQUEST_LOGIN (0x0064, 55) ----

        static readonly byte[] GoldenRequestLogin =
        {
            0x64, 0x00,                                     // id
            0xF6, 0x89, 0x34, 0x01,                         // PacketVersion 20220406
            0x75, 0x73, 0x65, 0x72, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,   // "user" + zero fill (24)
            0x70, 0x61, 0x73, 0x73, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,   // "pass" + zero fill (24)
            0x30,                                           // client type '0'
        };

        [Test]
        public void REQUEST_LOGIN_WritesGoldenBytes()
        {
            var p = new REQUEST_LOGIN(ProtocolInfo.PacketVersion, new FixedString32Bytes("user"), new FixedString32Bytes("pass"));
            Assert.AreEqual(55, p.WireSize);
            var buf = new byte[p.WireSize];
            p.Serialize(buf);
            Assert.AreEqual(GoldenRequestLogin, buf);
        }

        [Test]
        public void REQUEST_LOGIN_TruncatesLongTextTo23Characters_AndKeepsTheTerminator()
        {
            var longName = new FixedString32Bytes("abcdefghijklmnopqrstuvwxyz");   // 26 characters
            var p = new REQUEST_LOGIN(1, longName, new FixedString32Bytes("x"));
            var buf = new byte[p.WireSize];
            p.Serialize(buf);
            Assert.AreEqual((byte)'w', buf[6 + 22]);        // 23rd character
            Assert.AreEqual(0, buf[6 + 23], "last name byte is the NUL terminator");
            Assert.AreEqual((byte)'x', buf[30], "the password starts exactly after the 24-byte name field");
        }

        [Test]
        public void REQUEST_LOGIN_Serialize_FillsExactlyWireSize()
        {
            var p = new REQUEST_LOGIN(1, new FixedString32Bytes("a"), new FixedString32Bytes("b"));
            Assert.Throws<PacketFormatException>(() => p.Serialize(new byte[54]), "one byte short must not be silently accepted");
        }

        [Test]
        public void REQUEST_LOGIN_SizeAndId()
        {
            Assert.AreEqual(55, REQUEST_LOGIN.Size);
            Assert.AreEqual(0x0064, PacketIds.REQUEST_LOGIN);
            Assert.AreEqual(20220406u, ProtocolInfo.PacketVersion);
        }
    }
}
