using System;
using Unity.Collections;
using Uninarok.Networking.Client;
using Uninarok.Networking.Core.Dispatch;
using Uninarok.Networking.Flow;
using Uninarok.Networking.Packets;
using Uninarok.Networking.Packets.In.Login;
using Uninarok.Networking.Packets.Out.Login;

namespace Uninarok.Networking.Managers
{
    /// <summary>
    /// Protocol logic of the login server (SPEC-0001 FR-9, FR-11, FR-12, FR-20; plan 4.9). Sends the opening
    /// <c>REQUEST_LOGIN</c> once, fills the session from <c>ACCEPT_LOGIN</c>, and reports the three kinds of refusal.
    /// It never changes flow state, never retries, and never touches a socket.
    /// </summary>
    public sealed class LoginManager : IServerManager
    {
        private readonly INetworkClient _client;
        private readonly SessionContext _context;
        private readonly Subscriptions _subscriptions;

        // Created once, so Enter/Exit cycles do not allocate delegates.
        private readonly PacketHandler<ACCEPT_LOGIN> _onAccept;
        private readonly PacketHandler<LOGIN_REFUSAL> _onRefusal;
        private readonly PacketHandler<REFUSE_LOGIN_3> _onRefuse3;
        private readonly PacketHandler<LOGIN_SERVER_NOTICE> _onNotice;

        private string _user;
        private string _password;

        public LoginManager(INetworkClient client, SessionContext context)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _subscriptions = new Subscriptions(client);
            _onAccept = OnAccept;
            _onRefusal = OnRefusal;
            _onRefuse3 = OnRefuse3;
            _onNotice = OnNotice;
        }

        /// <summary>The login was accepted: the session holds the account values and the server list.</summary>
        public event Action Accepted;

        /// <summary>The login was refused by LOGIN_REFUSAL, REFUSE_LOGIN_3 or LOGIN_SERVER_NOTICE.</summary>
        public event Action<LoginRefusal> Refused;

        /// <summary>Held until <see cref="OnConnected"/> sends <c>REQUEST_LOGIN</c>, then cleared. Cleared again by <see cref="Exit"/>.</summary>
        public void SetCredentials(string user, string password)
        {
            _user = user;
            _password = password;
        }

        public void Enter()
        {
            _subscriptions.Add(_onAccept);
            _subscriptions.Add(_onRefusal);
            _subscriptions.Add(_onRefuse3);
            _subscriptions.Add(_onNotice);
        }

        public void Exit()
        {
            _subscriptions.Clear();
            _user = null;
            _password = null;
        }

        /// <summary>
        /// Sends one <c>REQUEST_LOGIN</c> with the held credentials, then forgets them: a second call, or a refusal, can never
        /// produce a second request (FR-11). A failed send is not retried either; the closed connection reaches the flow.
        /// </summary>
        public void OnConnected()
        {
            if (_user == null) return;

            var name = new FixedString32Bytes();
            var password = new FixedString32Bytes();
            name.CopyFromTruncated(_user);
            password.CopyFromTruncated(_password ?? string.Empty);
            _user = null;
            _password = null;

            _client.Send(new REQUEST_LOGIN(ProtocolInfo.PacketVersion, in name, in password));
        }

        /// <summary>The login server needs no keepalive after <c>ACCEPT_LOGIN</c> (spec section 6).</summary>
        public void Tick(long nowMs) { }

        private void OnAccept(in ACCEPT_LOGIN p)
        {
            _context.AccountId = p.AccountId;
            _context.AuthCode = p.AuthCode;
            _context.UserLevel = p.UserLevel;
            _context.Sex = p.Sex;
            _context.Servers.Clear();
            _context.Servers.AddRange(p.Servers);
            Accepted?.Invoke();
        }

        private void OnRefusal(in LOGIN_REFUSAL p) =>
            Refused?.Invoke(new LoginRefusal(LoginRefusalSource.Refusal, p.ErrorCode, in p.BlockDate));

        private void OnRefuse3(in REFUSE_LOGIN_3 p) =>
            Refused?.Invoke(new LoginRefusal(LoginRefusalSource.Refuse3, p.ErrorCode, in p.BlockDate));

        private void OnNotice(in LOGIN_SERVER_NOTICE p) =>
            Refused?.Invoke(new LoginRefusal(LoginRefusalSource.ServerNotice, p.ErrorCode, default));
    }
}
