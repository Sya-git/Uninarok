#if PENDING_T6_5 // T6.4: enabled (guard removed) by T6.5 when CharacterManager exists.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine.TestTools.Constraints;
using Uninarok.Networking.Client;
using Uninarok.Networking.Core.Transport;
using Uninarok.Networking.Data;
using Uninarok.Networking.Flow;
using Uninarok.Networking.Managers;
using Uninarok.Networking.Packets;
using Uninarok.Networking.Packets.In.Character;

namespace Uninarok.Networking.Tests
{
    // SPEC-0001 FR-3, FR-9, FR-12, FR-13, FR-14, FR-20; plan 4.9. CharacterManager speaks only the character server's protocol.
    public class CharacterManagerTests
    {
        private FakeNetworkClient _client;
        private SessionContext _ctx;
        private NetworkSettings _settings;
        private CharacterManager _char;

        private int _charactersReceived, _creationSucceeded, _mapAssigned;
        private readonly List<byte> _creationFailures = new List<byte>();
        private readonly List<byte> _mapRefusals = new List<byte>();
        private readonly List<PinState> _pinChanges = new List<PinState>();

        [SetUp]
        public void SetUp()
        {
            _client = new FakeNetworkClient();
            _ctx = new SessionContext { AccountId = 2000001, AuthCode = 0x11223344, UserLevel = 99, Sex = AccountSex.SEX_MALE };
            _settings = new NetworkSettings();
            _char = new CharacterManager(_client, _ctx, _settings);
            _charactersReceived = _creationSucceeded = _mapAssigned = 0;
            _creationFailures.Clear(); _mapRefusals.Clear(); _pinChanges.Clear();
            _char.CharactersReceived += () => _charactersReceived++;
            _char.CreationSucceeded += () => _creationSucceeded++;
            _char.CreationFailed += c => _creationFailures.Add(c);
            _char.MapServerAssigned += () => _mapAssigned++;
            _char.MapEnterRefused += c => _mapRefusals.Add(c);
            _char.PinStateChanged += s => _pinChanges.Add(s);
        }

        // ---- helpers ----

        static byte[] CharBytes(int id, string name, short slot)
        {
            var c = new CharInfo { CharId = id, Name = new FixedString32Bytes(name), MapName = new FixedString32Bytes("prontera"), Slot = slot, Sex = AccountSex.SEX_MALE };
            var buf = new byte[CharInfo.WireSize];
            var w = new Uninarok.Networking.Core.Wire.PacketWriter(buf);
            c.Write(ref w);
            return buf;
        }

        static CHARACTER_DATA CharacterData(params byte[][] records)
            => new CHARACTER_DATA(Wire.Concat(new byte[23], Wire.Concat(records)));

        static MAP_ENTER_REQUEST_ACCEPTED MapAccepted(int charId, string map, byte[] ip, ushort port)
        {
            var payload = new byte[MAP_ENTER_REQUEST_ACCEPTED.Size - 2];
            payload[0] = (byte)charId; payload[1] = (byte)(charId >> 8); payload[2] = (byte)(charId >> 16); payload[3] = (byte)(charId >> 24);
            for (int i = 0; i < map.Length; i++) payload[4 + i] = (byte)map[i];
            Array.Copy(ip, 0, payload, 20, 4);
            payload[24] = (byte)port; payload[25] = (byte)(port >> 8);
            return new MAP_ENTER_REQUEST_ACCEPTED(payload);
        }

        private void EnterAndConnect()
        {
            _char.Enter();
            _client.Connect("127.0.0.1", 6121, ConnectionRole.Character);
            _client.RaiseConnected();
            _char.OnConnected();
        }

        // ---- opening request and the 4-byte echo (FR-3) ----

        [Test]
        public void OnConnected_SkipsFourBytes_BeforeSendingTheEntryRequest()
        {
            EnterAndConnect();
            CollectionAssert.AreEqual(new[] { "Connect Character 127.0.0.1:6121", "Skip 4", "Send 0x0065" }, _client.Actions);
        }

        [Test]
        public void EntryRequest_CarriesTheSessionValues()
        {
            EnterAndConnect();
            Assert.AreEqual(new byte[]
            {
                0x65, 0x00, 0x81, 0x84, 0x1E, 0x00, 0x44, 0x33, 0x22, 0x11, 0x63, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01,
            }, _client.Sent[0].Bytes);
        }

        // ---- PIN state (FR-13) ----

        [Test]
        public void PinState_IsUnknown_AfterEnter()
        {
            _char.Enter();
            Assert.AreEqual(PinState.Unknown, _char.PinState);
        }

        [Test]
        public void PinState_IsUnknown_EvenBeforeTheFirstEnter_AndAfterReEntering()
        {
            Assert.AreEqual(PinState.Unknown, _char.PinState);
            _char.Enter();
            _client.Deliver(new PINCODE_LOGINSTATE(new byte[] { 1, 0, 0, 0, 2, 0, 0, 0, 7, 0 }));
            Assert.AreEqual(PinState.Unused, _char.PinState);
            _char.Exit();
            _char.Enter();
            Assert.AreEqual(PinState.Unknown, _char.PinState, "a new visit to the server starts unknown again");
        }

        [Test]
        public void PincodeLoginState_UpdatesThePinState_AndRaisesTheEvent()
        {
            _char.Enter();
            _client.Deliver(new PINCODE_LOGINSTATE(new byte[] { 0x78, 0x56, 0x34, 0x12, 0x81, 0x84, 0x1E, 0x00, 1, 0 }));
            Assert.AreEqual(PinState.Ask, _char.PinState);
            CollectionAssert.AreEqual(new[] { PinState.Ask }, _pinChanges);
        }

        // ---- character list and slots ----

        [Test]
        public void CharacterData_FillsTheContext_AndRaisesCharactersReceived()
        {
            _char.Enter();
            _client.Deliver(CharacterData(CharBytes(1, "One", 0), CharBytes(2, "Two", 1)));

            Assert.AreEqual(1, _charactersReceived);
            Assert.AreEqual(2, _ctx.Characters.Count);
            Assert.AreEqual("Two", _ctx.Characters[1].Name.ToString());
        }

        [Test]
        public void CharacterData_ReplacesTheList_NotAppendsToIt()
        {
            _char.Enter();
            _client.Deliver(CharacterData(CharBytes(1, "One", 0)));
            _client.Deliver(CharacterData(CharBytes(2, "Two", 1), CharBytes(3, "Three", 2)));
            Assert.AreEqual(2, _ctx.Characters.Count);
            Assert.AreEqual(2, _ctx.Characters[0].CharId);
        }

        [Test]
        public void EmptyCharacterList_StillRaisesCharactersReceived()
        {
            _char.Enter();
            _client.Deliver(CharacterData());
            Assert.AreEqual(1, _charactersReceived);
            Assert.AreEqual(0, _ctx.Characters.Count);
        }

        [Test]
        public void CharacterSlotsInfo_FillsTheContext()
        {
            _char.Enter();
            var payload = new byte[25];
            payload[0] = 9; payload[1] = 6; payload[3] = 2; payload[4] = 3;
            _client.Deliver(new CHARACTER_SLOTS_INFO(payload));
            Assert.AreEqual((9, 6, 2, 3), ((int)_ctx.Slots.CharSlots, (int)_ctx.Slots.AvailableCharSlots, (int)_ctx.Slots.CharSlots2, (int)_ctx.Slots.CharSlots3));
        }

        // ---- creating a character (FR-13) ----

        [Test]
        public void RequestCreate_SendsCreateCharacter()
        {
            EnterAndConnect();
            _client.ClearRecords();
            _client.Status = ConnectionStatus.Connected;
            _char.RequestCreate(new CharacterDraft(new FixedString32Bytes("Hero"), 1, 3, 7, 4001, AccountSex.SEX_MALE));

            Assert.AreEqual(1, _client.SentCount(PacketIds.CREATE_CHARACTER));
            Assert.AreEqual(36, _client.Sent[0].Bytes.Length);
            Assert.AreEqual((byte)'H', _client.Sent[0].Bytes[2]);
            Assert.AreEqual(1, _client.Sent[0].Bytes[26], "slot");
        }

        [Test]
        public void CreationSuccess_AddsTheCharacter_AndRaisesTheEvent()
        {
            _char.Enter();
            _client.Deliver(CharacterData(CharBytes(1, "One", 0)));
            _client.Deliver(new CHARACTER_CREATION_SUCCESS(CharBytes(9, "Newbie", 1)));

            Assert.AreEqual(1, _creationSucceeded);
            Assert.AreEqual(2, _ctx.Characters.Count);
            Assert.AreEqual("Newbie", _ctx.Characters[1].Name.ToString());
        }

        [Test]
        public void CreationFailure_ExposesTheReason_AndRaisesTheEvent()
        {
            _char.Enter();
            Assert.AreEqual(-1, _char.LastCreationFailure, "no failure yet");
            _client.Deliver(new CHARACTER_CREATION_FAILED(new byte[] { CHARACTER_CREATION_FAILED.NameAlreadyExists }));

            CollectionAssert.AreEqual(new byte[] { 0x00 }, _creationFailures);
            Assert.AreEqual(0x00, _char.LastCreationFailure);
            Assert.AreEqual(0, _creationSucceeded);

            _client.Deliver(new CHARACTER_CREATION_FAILED(new byte[] { CHARACTER_CREATION_FAILED.Denied }));
            Assert.AreEqual(0xFF, _char.LastCreationFailure);
        }

        // ---- selecting a character and the map assignment ----

        [Test]
        public void RequestSelect_StoresTheSlot_AndSendsSelectCharacter()
        {
            EnterAndConnect();
            _client.ClearRecords();
            _client.Status = ConnectionStatus.Connected;
            _char.RequestSelect(2);

            Assert.AreEqual(2, _ctx.SelectedSlot);
            Assert.AreEqual(new byte[] { 0x66, 0x00, 0x02 }, _client.Sent[0].Bytes);
        }

        [Test]
        public void MapEnterRequestAccepted_FillsTheMapEndpoint_AndRaisesMapServerAssigned()
        {
            _char.Enter();
            _client.Deliver(MapAccepted(140593, "prontera", new byte[] { 10, 0, 0, 2 }, 6122));

            Assert.AreEqual(1, _mapAssigned);
            Assert.AreEqual(140593, _ctx.CharacterId);
            Assert.AreEqual("10.0.0.2", _ctx.Map.Host);
            Assert.AreEqual(6122, _ctx.Map.Port);
            Assert.AreEqual("prontera", _ctx.Map.MapName.ToString());
        }

        [Test]
        public void MapEnterRequestAccepted_WithAPortAbove32767_StoresItUnchanged()
        {
            _char.Enter();
            _client.Deliver(MapAccepted(1, "prontera", new byte[] { 127, 0, 0, 1 }, 40000));
            Assert.AreEqual(40000, _ctx.Map.Port, "FR-12: ports cover 0 to 65535");
            _client.Deliver(MapAccepted(1, "prontera", new byte[] { 127, 0, 0, 1 }, 65535));
            Assert.AreEqual(65535, _ctx.Map.Port);
        }

        [Test]
        public void MapEnterRequestRefused_ExposesTheReason_AndRaisesTheEvent()
        {
            _char.Enter();
            Assert.AreEqual(-1, _char.LastMapEnterRefusal);
            _client.Deliver(new MAP_ENTER_REQUEST_REFUSED(new byte[] { 0 }));

            CollectionAssert.AreEqual(new byte[] { 0 }, _mapRefusals);
            Assert.AreEqual(0, _char.LastMapEnterRefusal);
            Assert.AreEqual(0, _mapAssigned);
        }

        // ---- keepalive (FR-14) ----

        [Test]
        public void Ping_IsSentEvery12Seconds_WithTheAccountId()
        {
            EnterAndConnect();
            _client.ClearRecords();
            _client.Status = ConnectionStatus.Connected;

            _char.Tick(0);                                         // schedules the first ping
            _char.Tick(11_999);
            Assert.AreEqual(0, _client.SentCount(PacketIds.CHARACTER_SERVER_PING));

            _char.Tick(12_000);
            Assert.AreEqual(1, _client.SentCount(PacketIds.CHARACTER_SERVER_PING));
            Assert.AreEqual(new byte[] { 0x87, 0x01, 0x81, 0x84, 0x1E, 0x00 }, _client.Sent[0].Bytes);

            _char.Tick(23_999);
            Assert.AreEqual(1, _client.SentCount(PacketIds.CHARACTER_SERVER_PING));
            _char.Tick(24_000);
            Assert.AreEqual(2, _client.SentCount(PacketIds.CHARACTER_SERVER_PING));
        }

        [Test]
        public void Ping_FollowsTheSettings()
        {
            _settings.CharacterKeepaliveSeconds = 5f;
            EnterAndConnect();
            _client.ClearRecords();
            _client.Status = ConnectionStatus.Connected;
            _char.Tick(1_000);
            _char.Tick(5_999);
            Assert.AreEqual(0, _client.Sent.Count);
            _char.Tick(6_000);
            Assert.AreEqual(1, _client.Sent.Count);
        }

        [Test]
        public void ALongGap_SendsOnePing_NotABurst()
        {
            EnterAndConnect();
            _client.ClearRecords();
            _client.Status = ConnectionStatus.Connected;
            _char.Tick(0);
            _char.Tick(1_000_000);
            Assert.AreEqual(1, _client.SentCount(PacketIds.CHARACTER_SERVER_PING));
        }

        [Test]
        public void NoPing_BeforeConnected_OrAfterExit()
        {
            _char.Enter();
            _char.Tick(0);
            _char.Tick(100_000);
            Assert.AreEqual(0, _client.Sent.Count, "not connected yet");

            EnterAndConnect();
            _client.ClearRecords();
            _client.Status = ConnectionStatus.Connected;
            _char.Exit();
            _char.Tick(0);
            _char.Tick(500_000);
            Assert.AreEqual(0, _client.Sent.Count, "none after leaving the server");
        }

        [Test]
        public void KeepaliveTick_AllocatesNothing()
        {
            EnterAndConnect();
            _client.Status = ConnectionStatus.Connected;
            _client.RecordSends = false;
            _char.Tick(0);
            _char.Tick(12_000);                                    // warm-up: first ping

            long now = 12_000;
            Assert.That(() => { now += 12_000; _char.Tick(now); }, UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory());
            Assert.Greater(_client.UncountedSends, 1);
        }

        // ---- subscriptions (FR-9) ----

        [Test]
        public void Enter_Subscribes_AndExit_ReleasesEveryHandler()
        {
            _char.Enter();
            Assert.AreEqual(1, _client.Dispatcher.HandlerCount<CHARACTER_DATA>());
            Assert.AreEqual(1, _client.Dispatcher.HandlerCount<CHARACTER_SLOTS_INFO>());
            Assert.AreEqual(1, _client.Dispatcher.HandlerCount<PINCODE_LOGINSTATE>());
            Assert.AreEqual(1, _client.Dispatcher.HandlerCount<CHARACTER_CREATION_SUCCESS>());
            Assert.AreEqual(1, _client.Dispatcher.HandlerCount<CHARACTER_CREATION_FAILED>());
            Assert.AreEqual(1, _client.Dispatcher.HandlerCount<MAP_ENTER_REQUEST_ACCEPTED>());
            Assert.AreEqual(1, _client.Dispatcher.HandlerCount<MAP_ENTER_REQUEST_REFUSED>());

            _char.Exit();
            Assert.AreEqual(0, _client.Dispatcher.HandlerCount<CHARACTER_DATA>());
            Assert.AreEqual(0, _client.Dispatcher.HandlerCount<CHARACTER_SLOTS_INFO>());
            Assert.AreEqual(0, _client.Dispatcher.HandlerCount<PINCODE_LOGINSTATE>());
            Assert.AreEqual(0, _client.Dispatcher.HandlerCount<CHARACTER_CREATION_SUCCESS>());
            Assert.AreEqual(0, _client.Dispatcher.HandlerCount<CHARACTER_CREATION_FAILED>());
            Assert.AreEqual(0, _client.Dispatcher.HandlerCount<MAP_ENTER_REQUEST_ACCEPTED>());
            Assert.AreEqual(0, _client.Dispatcher.HandlerCount<MAP_ENTER_REQUEST_REFUSED>());
        }

        [Test]
        public void AfterExit_PacketsAreNotHandled()
        {
            _char.Enter();
            _char.Exit();
            _client.Deliver(CharacterData(CharBytes(1, "One", 0)));
            _client.Deliver(MapAccepted(1, "prontera", new byte[] { 1, 2, 3, 4 }, 6122));
            Assert.AreEqual(0, _charactersReceived);
            Assert.AreEqual(0, _mapAssigned);
        }

        [Test]
        public void HundredEnterExitCycles_LeaveTheSubscriberCountUnchanged()
        {
            for (int i = 0; i < 100; i++) { _char.Enter(); _char.Exit(); }
            Assert.AreEqual(0, _client.Dispatcher.HandlerCount<CHARACTER_DATA>());
            _char.Enter();
            Assert.AreEqual(1, _client.Dispatcher.HandlerCount<CHARACTER_DATA>());
            _char.Exit();
        }

        [Test]
        public void Constructor_RejectsNullArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new CharacterManager(null, _ctx, _settings));
            Assert.Throws<ArgumentNullException>(() => new CharacterManager(_client, null, _settings));
            Assert.Throws<ArgumentNullException>(() => new CharacterManager(_client, _ctx, null));
        }
    }
}
#endif
