using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Uninarok.Networking.Client;
using Uninarok.Networking.Core.Transport;
using Uninarok.Networking.Data;
using Uninarok.Networking.Flow;
using Uninarok.Networking.Managers;
using Uninarok.Networking.Packets;
using Uninarok.Networking.Packets.In.Map;

namespace Uninarok.Networking.Tests
{
    // SPEC-0001 FR-6, FR-9, FR-14, FR-20; plan 4.9. ZoneManager speaks only the map server's protocol.
    public class ZoneManagerTests
    {
        private FakeNetworkClient _client;
        private SessionContext _ctx;
        private FakeClock _clock;
        private NetworkSettings _settings;
        private ZoneManager _zone;
        private int _authenticated;
        private readonly List<byte> _refusals = new List<byte>();

        [SetUp]
        public void SetUp()
        {
            _client = new FakeNetworkClient();
            _ctx = new SessionContext { AccountId = 2000001, CharacterId = 140593, AuthCode = 0x11223344, Sex = AccountSex.SEX_MALE };
            _clock = new FakeClock { NowMs = 12_345_678 };
            _settings = new NetworkSettings();
            _zone = new ZoneManager(_client, _ctx, _clock, _settings);
            _authenticated = 0;
            _refusals.Clear();
            _zone.Authenticated += () => _authenticated++;
            _zone.EnterRefused += c => _refusals.Add(c);
        }

        private void EnterAndConnect()
        {
            _zone.Enter();
            _client.Connect("10.0.0.2", 6122, ConnectionRole.Map);
            _client.RaiseConnected();
            _zone.OnConnected();
        }

        static AUTHENTICATION_ACCEPTED Authenticated()
            => new AUTHENTICATION_ACCEPTED(new byte[] { 0x04, 0x03, 0x02, 0x01, 0x25, 0x8C, 0x84, 0x05, 0x05, 0x00, 0x00 });

        // ---- entering the map ----

        [Test]
        public void OnConnected_SendsRequestEnterMap_WithTheContextValues_AndTheClockTimestamp()
        {
            EnterAndConnect();

            Assert.AreEqual(1, _client.Sent.Count);
            Assert.AreEqual(PacketIds.REQUEST_ENTER_MAP, _client.Sent[0].Id);
            Assert.AreEqual(new byte[]
            {
                0x36, 0x04,                 // id
                0x81, 0x84, 0x1E, 0x00,     // AccountId
                0x31, 0x25, 0x02, 0x00,     // CharId
                0x44, 0x33, 0x22, 0x11,     // AuthCode
                0x00, 0x00, 0x00, 0x00,     // reserved
                0x4E, 0x61, 0xBC, 0x00,     // Timestamp = clock 12345678
                0x01,                       // Sex
            }, _client.Sent[0].Bytes);
        }

        [Test]
        public void AuthenticationAccepted_FillsTheEntry_AndRaisesAuthenticated()
        {
            _zone.Enter();
            _client.Deliver(Authenticated());

            Assert.AreEqual(1, _authenticated);
            Assert.AreEqual(0x01020304u, _ctx.Entry.StartTick);
            Assert.AreEqual(150, _ctx.Entry.Position.X);
            Assert.AreEqual(200, _ctx.Entry.Position.Y);
            Assert.AreEqual(UninarokDirection.South, _ctx.Entry.Position.Direction);
            Assert.AreEqual((5, 5), ((int)_ctx.Entry.XSize, (int)_ctx.Entry.YSize));
        }

        [Test]
        public void MapEntryRefusal_RaisesEnterRefused_WithTheCode()
        {
            _zone.Enter();
            _client.Deliver(new MAP_ENTRY_REFUSAL(new byte[] { 3 }));
            CollectionAssert.AreEqual(new byte[] { 3 }, _refusals);
            Assert.AreEqual(0, _authenticated);
        }

        [Test]
        public void HeartbeatResponse_WithNoOutstandingRequest_RaisesNothing()
        {
            int answered = 0;
            _zone.HeartbeatAnswered += (tick, sent, received) => answered++;
            _zone.Enter();
            Assert.AreEqual(1, _client.Dispatcher.HandlerCount<SERVER_HEARTBEAT_RESPONSE>());
            _client.Deliver(new SERVER_HEARTBEAT_RESPONSE(new byte[] { 1, 0, 0, 0 }));
            Assert.AreEqual(0, answered);
            Assert.AreEqual(0, _client.HandlerErrors.Count);
            Assert.AreEqual(0, _client.Sent.Count);
        }

        // ---- the server clock's samples (SPEC-0010 FR-2, PLAN-0010 4.2) ----

        [Test]
        public void ZoneManager_HeartbeatAnswered_RttFromSendTime()
        {
            var answers = new List<(uint Tick, long Sent, long Received)>();
            _zone.HeartbeatAnswered += (tick, sent, received) => answers.Add((tick, sent, received));
            _clock.NowMs = 5_000;
            EnterAndConnect();
            _zone.SendMapLoaded();
            _clock.NowMs = 5_080;
            _client.Deliver(new SERVER_HEARTBEAT_RESPONSE(new byte[] { 0x10, 0x27, 0, 0 }));
            _client.Deliver(new SERVER_HEARTBEAT_RESPONSE(new byte[] { 0x11, 0x27, 0, 0 })); // a second answer to the same request

            _clock.NowMs = 15_001;
            _zone.Tick(15_001);
            _clock.NowMs = 15_151;
            _client.Deliver(new SERVER_HEARTBEAT_RESPONSE(new byte[] { 0x20, 0x4E, 0, 0 }));

            CollectionAssert.AreEqual(new[] { (10_000u, 5_000L, 5_080L), (20_000u, 15_001L, 15_151L) }, answers);
        }

        [Test]
        public void HeartbeatAnswered_AfterAnUnansweredRequest_CountsFromTheOlderOne()
        {
            long sentAt = 0;
            _zone.HeartbeatAnswered += (tick, sent, received) => sentAt = sent;
            _clock.NowMs = 5_000;
            EnterAndConnect();
            _zone.SendMapLoaded();
            _clock.NowMs = 15_000;
            _zone.Tick(15_000);
            _clock.NowMs = 15_010;
            _client.Deliver(new SERVER_HEARTBEAT_RESPONSE(new byte[] { 1, 0, 0, 0 }));
            Assert.AreEqual(5_000L, sentAt);
        }

        [Test]
        public void HeartbeatAnswered_IsForgottenOnExit_AndAFailedSendIsNotOutstanding()
        {
            int answered = 0;
            _zone.HeartbeatAnswered += (tick, sent, received) => answered++;
            EnterAndConnect();
            _zone.SendMapLoaded();
            _zone.Exit();
            _zone.Enter();
            _client.Deliver(new SERVER_HEARTBEAT_RESPONSE(new byte[] { 1, 0, 0, 0 }));
            _client.AcceptSends = false;
            _zone.SendMapLoaded();
            _client.Deliver(new SERVER_HEARTBEAT_RESPONSE(new byte[] { 1, 0, 0, 0 }));
            Assert.AreEqual(0, answered);
        }

        // ---- MAP_LOADED and the keepalive (FR-14) ----

        [Test]
        public void NoHeartbeat_BeforeMapLoaded()
        {
            EnterAndConnect();
            _client.ClearRecords();
            for (long t = 0; t <= 100_000; t += 5_000) _zone.Tick(t);
            Assert.AreEqual(0, _client.SentCount(PacketIds.HEARTBEAT_REQUEST));
        }

        [Test]
        public void SendMapLoaded_SendsTheHeaderOnlyPacket_ThenOneHeartbeatAtOnce()
        {
            EnterAndConnect();
            _client.ClearRecords();
            _zone.SendMapLoaded();

            Assert.AreEqual(2, _client.Sent.Count);
            Assert.AreEqual(new byte[] { 0x7D, 0x00 }, _client.Sent[0].Bytes);
            Assert.AreEqual(new byte[] { 0x60, 0x03, 0x4E, 0x61, 0xBC, 0x00 }, _client.Sent[1].Bytes, "PLAN-0010 D6: the first heartbeat, with the clock");
        }

        [Test]
        public void Heartbeat_IsSentEvery10Seconds_WithIncreasingTimestamps()
        {
            _clock.NowMs = 5_000;
            EnterAndConnect();
            _client.ClearRecords();
            _zone.SendMapLoaded();                       // the first heartbeat goes now, the next is due at 15 000
            _client.ClearRecords();

            _zone.Tick(14_999);
            Assert.AreEqual(0, _client.Sent.Count);
            _zone.Tick(15_000);
            _zone.Tick(24_999);
            _zone.Tick(25_000);
            _zone.Tick(35_000);

            Assert.AreEqual(3, _client.SentCount(PacketIds.HEARTBEAT_REQUEST));
            uint ts(int i) => BitConverter.ToUInt32(_client.Sent[i].Bytes, 2);
            Assert.AreEqual(15_000u, ts(0));
            Assert.AreEqual(25_000u, ts(1));
            Assert.AreEqual(35_000u, ts(2));
            Assert.Less(ts(0), ts(1));
            Assert.Less(ts(1), ts(2));
            Assert.AreEqual(new byte[] { 0x60, 0x03 }, new[] { _client.Sent[0].Bytes[0], _client.Sent[0].Bytes[1] });
        }

        [Test]
        public void Heartbeat_FollowsTheSettings()
        {
            _settings.MapKeepaliveSeconds = 4f;
            _clock.NowMs = 0;
            EnterAndConnect();
            _zone.SendMapLoaded();
            _client.ClearRecords();
            _zone.Tick(3_999);
            Assert.AreEqual(0, _client.Sent.Count);
            _zone.Tick(4_000);
            Assert.AreEqual(1, _client.Sent.Count);
        }

        [Test]
        public void ALongGap_SendsOneHeartbeat_NotABurst()
        {
            _clock.NowMs = 0;
            EnterAndConnect();
            _zone.SendMapLoaded();
            _client.ClearRecords();
            _zone.Tick(1_000_000);
            Assert.AreEqual(1, _client.SentCount(PacketIds.HEARTBEAT_REQUEST));
        }

        [Test]
        public void NoHeartbeat_AfterExit()
        {
            _clock.NowMs = 0;
            EnterAndConnect();
            _zone.SendMapLoaded();
            _zone.Exit();
            _client.ClearRecords();

            _zone.Tick(10_000);
            _zone.Tick(500_000);
            Assert.AreEqual(0, _client.Sent.Count);
        }

        [Test]
        public void HeartbeatTick_AllocatesNothing()
        {
            _clock.NowMs = 0;
            EnterAndConnect();
            _zone.SendMapLoaded();
            _client.RecordSends = false;
            _zone.Tick(10_000);                          // warm-up

            long now = 10_000;
            Assert.That(() => { now += 10_000; _zone.Tick(now); }, UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory());
            Assert.Greater(_client.UncountedSends, 1);
        }

        [Test]
        public void ReEntering_RequiresAFreshMapLoaded_BeforeHeartbeatsResume()
        {
            _clock.NowMs = 0;
            EnterAndConnect();
            _zone.SendMapLoaded();
            _zone.Exit();

            EnterAndConnect();                           // a new visit to the map server
            _client.ClearRecords();
            _zone.Tick(10_000);
            _zone.Tick(50_000);
            Assert.AreEqual(0, _client.SentCount(PacketIds.HEARTBEAT_REQUEST));
        }

        // ---- subscriptions (FR-9) ----

        [Test]
        public void Enter_Subscribes_AndExit_ReleasesEveryHandler()
        {
            _zone.Enter();
            Assert.AreEqual(1, _client.Dispatcher.HandlerCount<AUTHENTICATION_ACCEPTED>());
            Assert.AreEqual(1, _client.Dispatcher.HandlerCount<MAP_ENTRY_REFUSAL>());
            Assert.AreEqual(1, _client.Dispatcher.HandlerCount<SERVER_HEARTBEAT_RESPONSE>());

            _zone.Exit();
            Assert.AreEqual(0, _client.Dispatcher.HandlerCount<AUTHENTICATION_ACCEPTED>());
            Assert.AreEqual(0, _client.Dispatcher.HandlerCount<MAP_ENTRY_REFUSAL>());
            Assert.AreEqual(0, _client.Dispatcher.HandlerCount<SERVER_HEARTBEAT_RESPONSE>());
        }

        [Test]
        public void AfterExit_PacketsAreNotHandled()
        {
            _zone.Enter();
            _zone.Exit();
            _client.Deliver(Authenticated());
            _client.Deliver(new MAP_ENTRY_REFUSAL(new byte[] { 1 }));
            Assert.AreEqual(0, _authenticated);
            Assert.AreEqual(0, _refusals.Count);
        }

        [Test]
        public void HundredEnterExitCycles_LeaveTheSubscriberCountUnchanged()
        {
            for (int i = 0; i < 100; i++) { _zone.Enter(); _zone.Exit(); }
            Assert.AreEqual(0, _client.Dispatcher.HandlerCount<AUTHENTICATION_ACCEPTED>());
            _zone.Enter();
            Assert.AreEqual(1, _client.Dispatcher.HandlerCount<AUTHENTICATION_ACCEPTED>());
            _zone.Exit();
        }

        [Test]
        public void Constructor_RejectsNullArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new ZoneManager(null, _ctx, _clock, _settings));
            Assert.Throws<ArgumentNullException>(() => new ZoneManager(_client, null, _clock, _settings));
            Assert.Throws<ArgumentNullException>(() => new ZoneManager(_client, _ctx, null, _settings));
            Assert.Throws<ArgumentNullException>(() => new ZoneManager(_client, _ctx, _clock, null));
        }
    }
}
