using System;
using Unity.Collections;
using Uninarok.Networking.Core.Wire;
using Uninarok.Networking.Data;
using Uninarok.Networking.Flow;
using Uninarok.Networking.Packets.In.Character;
using Uninarok.Networking.Packets.In.Login;
using Uninarok.Networking.Packets.In.Map;

namespace Uninarok.Networking.Tests
{
    /// <summary>Builds incoming server packets for flow and manager tests, from the wire layouts in the packet docs.</summary>
    public static class TestPackets
    {
        public static byte[] Text(string s, int wireBytes)
        {
            var b = new byte[wireBytes];
            for (int i = 0; i < s.Length; i++) b[i] = (byte)s[i];
            return b;
        }

        public static byte[] ServerEntry(byte[] ip, ushort port, string name)
        {
            var e = new byte[ServerEndpoint.WireSize];
            Array.Copy(ip, 0, e, 0, 4);
            e[4] = (byte)port; e[5] = (byte)(port >> 8);
            Array.Copy(Text(name, 20), 0, e, 6, 20);
            return e;
        }

        /// <summary>ACCEPT_LOGIN: account 2000001, auth code 0x11223344, user level 99, male, one entry per given server.</summary>
        public static ACCEPT_LOGIN AcceptLogin(params byte[][] servers)
        {
            var fixedPart = new byte[60];
            void Put(int at, uint v) { fixedPart[at] = (byte)v; fixedPart[at + 1] = (byte)(v >> 8); fixedPart[at + 2] = (byte)(v >> 16); fixedPart[at + 3] = (byte)(v >> 24); }
            Put(0, 0x11223344);
            Put(4, 2000001);
            Put(8, 99);
            fixedPart[42] = 1;
            return new ACCEPT_LOGIN(Wire.Concat(fixedPart, Wire.Concat(servers)));
        }

        public static ACCEPT_LOGIN AcceptLoginWithOneServer(string host = "127.0.0.1", ushort port = 6121)
        {
            var octets = Array.ConvertAll(host.Split('.'), byte.Parse);
            return AcceptLogin(ServerEntry(octets, port, "Uninarok"));
        }

        public static byte[] CharBytes(int id, string name, short slot)
        {
            var c = new CharInfo { CharId = id, Name = new FixedString32Bytes(name), MapName = new FixedString32Bytes("prontera"), Slot = slot, Sex = AccountSex.SEX_MALE };
            var buf = new byte[CharInfo.WireSize];
            var w = new PacketWriter(buf);
            c.Write(ref w);
            return buf;
        }

        public static CHARACTER_DATA CharacterData(params byte[][] records)
            => new CHARACTER_DATA(Wire.Concat(new byte[23], Wire.Concat(records)));

        public static MAP_ENTER_REQUEST_ACCEPTED MapAccepted(int charId, string map, byte[] ip, ushort port)
        {
            var payload = new byte[MAP_ENTER_REQUEST_ACCEPTED.Size - 2];
            payload[0] = (byte)charId; payload[1] = (byte)(charId >> 8); payload[2] = (byte)(charId >> 16); payload[3] = (byte)(charId >> 24);
            for (int i = 0; i < map.Length; i++) payload[4 + i] = (byte)map[i];
            Array.Copy(ip, 0, payload, 20, 4);
            payload[24] = (byte)port; payload[25] = (byte)(port >> 8);
            return new MAP_ENTER_REQUEST_ACCEPTED(payload);
        }

        public static AUTHENTICATION_ACCEPTED AuthenticationAccepted()
            => new AUTHENTICATION_ACCEPTED(new byte[] { 0x04, 0x03, 0x02, 0x01, 0x25, 0x8C, 0x84, 0x05, 0x05, 0x00, 0x00 });

        public static LOGIN_REFUSAL Refusal(byte code) => new LOGIN_REFUSAL(Wire.Concat(new byte[] { code }, new byte[20]));
        public static REFUSE_LOGIN_3 RefuseLogin3(int code) => new REFUSE_LOGIN_3(Wire.Concat(BitConverter.GetBytes(code), new byte[20]));
        public static LOGIN_SERVER_NOTICE ServerNotice(byte code) => new LOGIN_SERVER_NOTICE(new[] { code });
        public static CHARACTER_CREATION_SUCCESS CreationSuccess(int id, string name, short slot) => new CHARACTER_CREATION_SUCCESS(CharBytes(id, name, slot));
        public static CHARACTER_CREATION_FAILED CreationFailed(byte code) => new CHARACTER_CREATION_FAILED(new[] { code });
        public static MAP_ENTER_REQUEST_REFUSED MapEnterRefused(byte code) => new MAP_ENTER_REQUEST_REFUSED(new[] { code });
        public static MAP_ENTRY_REFUSAL MapEntryRefusal(byte code) => new MAP_ENTRY_REFUSAL(new[] { code });
    }
}
