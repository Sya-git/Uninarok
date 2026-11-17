using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Uninarok.Networking.Core.Transport;
using Uninarok.Networking.Data;
using Uninarok.Networking.Flow;
using Uninarok.Networking.Managers;
using Uninarok.Networking.Packets;
using Uninarok.Networking.Packets.In.Login;

namespace Uninarok.Networking.Tests
{
    // SPEC-0001 FR-9, FR-11, FR-12, FR-20; plan 4.9. LoginManager speaks only the login server's protocol.
    public class LoginManagerTests
    {
        private FakeNetworkClient _client;
        private SessionContext _ctx;
        private LoginManager _login;
        private int _accepted;
        private readonly List<LoginRefusal> _refusals = new List<LoginRefusal>();

        [SetUp]
        public void SetUp()
        {
            _client = new FakeNetworkClient();
            _ctx = new SessionContext();
            _login = new LoginManager(_client, _ctx);
            _accepted = 0;
            _refusals.Clear();
            _login.Accepted += () => _accepted++;
            _login.Refused += r => _refusals.Add(r);
        }

        // ---- helpers ----

        static byte[] Text(string s, int wireBytes)
        {
            var b = new byte[wireBytes];
            for (int i = 0; i < s.Length; i++) b[i] = (byte)s[i];
            return b;
        }

        static byte[] ServerEntry(byte[] ip, ushort port, string name)
        {
            var e = new byte[ServerEndpoint.WireSize];
            Array.Copy(ip, 0, e, 0, 4);
            e[4] = (byte)port; e[5] = (byte)(port >> 8);
            Array.Copy(Text(name, 20), 0, e, 6, 20);
            return e;
        }

        static ACCEPT_LOGIN AcceptLogin(params byte[][] servers)
        {
            var fixedPart = new byte[60];
            void Put(int at, uint v) { fixedPart[at] = (byte)v; fixedPart[at + 1] = (byte)(v >> 8); fixedPart[at + 2] = (byte)(v >> 16); fixedPart[at + 3] = (byte)(v >> 24); }
            Put(0, 0x11223344);      // AuthCode
            Put(4, 2000001);         // AccountId
            Put(8, 99);              // UserLevel
            fixedPart[42] = 1;       // Sex male
            return new ACCEPT_LOGIN(Wire.Concat(fixedPart, Wire.Concat(servers)));
        }

        private void EnterAndConnect()
        {
            _login.Enter();
            _client.Connect("127.0.0.1", 6900, ConnectionRole.Login);
            _client.RaiseConnected();
        }

        // ---- opening request ----

        [Test]
        public void OnConnected_SendsRequestLogin_WithTheCredentialsAndProtocolVersion()
        {
            _login.Enter();
            _login.SetCredentials("user", "pass");
            _client.Connect("127.0.0.1", 6900, ConnectionRole.Login);
            _client.RaiseConnected();
            _login.OnConnected();

            Assert.AreEqual(1, _client.Sent.Count);
            var bytes = _client.Sent[0].Bytes;
            Assert.AreEqual(55, bytes.Length);
            Assert.AreEqual(PacketIds.REQUEST_LOGIN, _client.Sent[0].Id);
            Assert.AreEqual(new byte[] { 0xF6, 0x89, 0x34, 0x01 }, new[] { bytes[2], bytes[3], bytes[4], bytes[5] }, "PACKETVER 20220406");
            Assert.AreEqual((byte)'u', bytes[6]);
            Assert.AreEqual((byte)'p', bytes[30]);
            Assert.AreEqual(ProtocolInfo.DefaultClientType, bytes[54]);
        }

        [Test]
        public void Credentials_AreClearedAfterTheRequest_SoTheyCannotBeSentTwice()
        {
            _login.Enter();
            _login.SetCredentials("user", "pass");
            _client.RaiseConnected(ConnectionRole.Login);
            _login.OnConnected();
            _login.OnConnected();                                   // a second call must not send a second request (FR-11)

            Assert.AreEqual(1, _client.SentCount(PacketIds.REQUEST_LOGIN));
        }

        [Test]
        public void OnConnected_WithoutCredentials_SendsNothing()
        {
            _login.Enter();
            _client.RaiseConnected(ConnectionRole.Login);
            _login.OnConnected();
            Assert.AreEqual(0, _client.Sent.Count);
        }

        [Test]
        public void ARefusedRequest_IsNotRetriedByTheManager()
        {
            _login.Enter();
            _login.SetCredentials("user", "wrong");
            _client.RaiseConnected(ConnectionRole.Login);
            _login.OnConnected();
            _client.Deliver(new LOGIN_REFUSAL(Wire.Concat(new byte[] { 1 }, new byte[20])));

            Assert.AreEqual(1, _refusals.Count);
            Assert.AreEqual(1, _client.SentCount(PacketIds.REQUEST_LOGIN), "exactly one login request, no automatic retry (FR-11)");
        }

        [Test]
        public void FailedSend_DoesNotThrow_AndIsNotRetried()
        {
            _login.Enter();
            _login.SetCredentials("user", "pass");
            _client.RaiseConnected(ConnectionRole.Login);
            _client.AcceptSends = false;
            Assert.DoesNotThrow(() => _login.OnConnected());
            _client.AcceptSends = true;
            _login.OnConnected();
            Assert.AreEqual(0, _client.Sent.Count);
        }

        // ---- ACCEPT_LOGIN ----

        [Test]
        public void AcceptLogin_FillsTheContext_AndRaisesAcceptedOnce()
        {
            _login.Enter();
            _client.Deliver(AcceptLogin(
                ServerEntry(new byte[] { 127, 0, 0, 1 }, 6121, "Uninarok"),
                ServerEntry(new byte[] { 10, 0, 0, 5 }, 40000, "High")));

            Assert.AreEqual(1, _accepted);
            Assert.AreEqual(2000001u, _ctx.AccountId);
            Assert.AreEqual(0x11223344, _ctx.AuthCode);
            Assert.AreEqual(99u, _ctx.UserLevel);
            Assert.AreEqual(AccountSex.SEX_MALE, _ctx.Sex);
            Assert.AreEqual(2, _ctx.Servers.Count);
            Assert.AreEqual("127.0.0.1", _ctx.Servers[0].Host);
            Assert.AreEqual(6121, _ctx.Servers[0].Port);
            Assert.AreEqual("Uninarok", _ctx.Servers[0].Name.ToString());
            Assert.AreEqual(40000, _ctx.Servers[1].Port, "a port above 32767 is kept (FR-12)");
        }

        [Test]
        public void AcceptLogin_ReplacesTheServerList_NotAppendsToIt()
        {
            _login.Enter();
            _client.Deliver(AcceptLogin(ServerEntry(new byte[] { 1, 1, 1, 1 }, 1, "a")));
            _client.Deliver(AcceptLogin(ServerEntry(new byte[] { 2, 2, 2, 2 }, 2, "b"), ServerEntry(new byte[] { 3, 3, 3, 3 }, 3, "c")));
            Assert.AreEqual(2, _ctx.Servers.Count);
            Assert.AreEqual("2.2.2.2", _ctx.Servers[0].Host);
        }

        // ---- refusals ----

        [Test]
        public void LoginRefusal_RaisesRefusedOnce_WithCodeAndBlockDate()
        {
            _login.Enter();
            _client.Deliver(new LOGIN_REFUSAL(Wire.Concat(new byte[] { 6 }, Text("2026-12-31 23:59:59", 20))));

            Assert.AreEqual(1, _refusals.Count);
            Assert.AreEqual(LoginRefusalSource.Refusal, _refusals[0].Source);
            Assert.AreEqual(6, _refusals[0].ErrorCode);
            Assert.AreEqual("2026-12-31 23:59:59", _refusals[0].BlockDate.ToString());
            Assert.AreEqual(0, _accepted);
        }

        [Test]
        public void RefuseLogin3_RaisesRefusedOnce()
        {
            _login.Enter();
            _client.Deliver(new REFUSE_LOGIN_3(Wire.Concat(new byte[] { 0x05, 0x01, 0, 0 }, Text("2027-01-01", 20))));

            Assert.AreEqual(1, _refusals.Count);
            Assert.AreEqual(LoginRefusalSource.Refuse3, _refusals[0].Source);
            Assert.AreEqual(0x0105, _refusals[0].ErrorCode);
            Assert.AreEqual("2027-01-01", _refusals[0].BlockDate.ToString());
        }

        [Test]
        public void LoginServerNotice_RaisesRefusedOnce()
        {
            _login.Enter();
            _client.Deliver(new LOGIN_SERVER_NOTICE(new byte[] { 8 }));

            Assert.AreEqual(1, _refusals.Count);
            Assert.AreEqual(LoginRefusalSource.ServerNotice, _refusals[0].Source);
            Assert.AreEqual(8, _refusals[0].ErrorCode);
            Assert.AreEqual(0, _refusals[0].BlockDate.Length);
        }

        // ---- subscriptions (FR-9) ----

        [Test]
        public void Enter_Subscribes_AndExit_ReleasesEveryHandler()
        {
            _login.Enter();
            Assert.AreEqual(1, _client.Dispatcher.HandlerCount<ACCEPT_LOGIN>());
            Assert.AreEqual(1, _client.Dispatcher.HandlerCount<LOGIN_REFUSAL>());
            Assert.AreEqual(1, _client.Dispatcher.HandlerCount<REFUSE_LOGIN_3>());
            Assert.AreEqual(1, _client.Dispatcher.HandlerCount<LOGIN_SERVER_NOTICE>());

            _login.Exit();
            Assert.AreEqual(0, _client.Dispatcher.HandlerCount<ACCEPT_LOGIN>());
            Assert.AreEqual(0, _client.Dispatcher.HandlerCount<LOGIN_REFUSAL>());
            Assert.AreEqual(0, _client.Dispatcher.HandlerCount<REFUSE_LOGIN_3>());
            Assert.AreEqual(0, _client.Dispatcher.HandlerCount<LOGIN_SERVER_NOTICE>());
        }

        [Test]
        public void AfterExit_PacketsAreNotHandled()
        {
            _login.Enter();
            _login.Exit();
            _client.Deliver(new LOGIN_SERVER_NOTICE(new byte[] { 8 }));
            _client.Deliver(AcceptLogin(ServerEntry(new byte[] { 1, 1, 1, 1 }, 1, "a")));

            Assert.AreEqual(0, _refusals.Count);
            Assert.AreEqual(0, _accepted);
            Assert.AreEqual(0, _ctx.Servers.Count);
        }

        [Test]
        public void HundredEnterExitCycles_LeaveTheSubscriberCountUnchanged()
        {
            for (int i = 0; i < 100; i++) { _login.Enter(); _login.Exit(); }
            Assert.AreEqual(0, _client.Dispatcher.HandlerCount<ACCEPT_LOGIN>());
            Assert.AreEqual(0, _client.Dispatcher.HandlerCount<LOGIN_REFUSAL>());

            _login.Enter();
            Assert.AreEqual(1, _client.Dispatcher.HandlerCount<ACCEPT_LOGIN>(), "exactly one subscription after many cycles");
            _login.Exit();
        }

        [Test]
        public void Exit_ClearsAnyUnsentCredentials()
        {
            _login.Enter();
            _login.SetCredentials("user", "pass");
            _login.Exit();
            _login.Enter();
            _client.RaiseConnected(ConnectionRole.Login);
            _login.OnConnected();
            Assert.AreEqual(0, _client.Sent.Count, "credentials do not survive a leave");
        }

        [Test]
        public void Tick_DoesNothing_OnTheLoginServer()
        {
            _login.Enter();
            _client.RaiseConnected(ConnectionRole.Login);
            _login.Tick(1_000_000);                                 // no keepalive on the login server after ACCEPT_LOGIN (spec section 6)
            Assert.AreEqual(0, _client.Sent.Count);
        }

        [Test]
        public void Constructor_RejectsNullArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new LoginManager(null, _ctx));
            Assert.Throws<ArgumentNullException>(() => new LoginManager(_client, null));
        }
    }
}
