using System;
using Uninarok.Networking.Client;
using Uninarok.Networking.Core.Transport;
using Uninarok.Networking.Managers;

namespace Uninarok.Networking.Flow
{
    /// <summary>
    /// The login, character and map flow as an explicit state machine (SPEC-0001 FR-10, FR-11, FR-13, FR-20; plan 4.9).
    /// It owns the state, every deadline, the hand-off between servers (which connection to open, which manager is active)
    /// and the <see cref="SessionContext"/>. The managers only speak their server's protocol; the game talks only to the flow.
    /// Every connection attempt and every wait for a server reply has a deadline, so no state can be stuck.
    /// Main thread only.
    /// </summary>
    public sealed class NetworkFlow : IDisposable
    {
        private readonly INetworkClient _client;
        private readonly IClock _clock;
        private readonly NetworkSettings _settings;
        private readonly ServerEndpoint _loginServer;

        // Exactly one manager is active at a time: entered before its connection opens, exited when leaving its server.
        private IServerManager _active;
        private ConnectionRole _activeRole;

        private bool _hasDeadline;
        private long _deadlineAt;
        private bool _connectionUp;       // false: waiting for the connection (ConnectTimeout); true: waiting for a reply (ReplyTimeout)
        private bool _authenticated;      // AUTHENTICATION_ACCEPTED arrived in LoadingMap
        private bool _disposed;

        public NetworkFlow(INetworkClient client, IClock clock, NetworkSettings settings, ServerEndpoint loginServer)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _loginServer = loginServer;

            Context = new SessionContext();
            Login = new LoginManager(client, Context);
            Character = new CharacterManager(client, Context, settings);
            Zone = new ZoneManager(client, Context, clock, settings);

            _client.Connected += OnConnected;
            _client.Disconnected += OnDisconnected;
            Login.Accepted += OnLoginAccepted;
            Login.Refused += OnLoginRefused;
            Character.CharactersReceived += OnCharactersReceived;
            Character.CreationSucceeded += OnCreationFinished;
            Character.CreationFailed += OnCreationFailed;
            Character.MapServerAssigned += OnMapServerAssigned;
            Character.MapEnterRefused += OnMapEnterRefused;
            Zone.Authenticated += OnAuthenticated;
            Zone.EnterRefused += OnEnterRefused;
            Zone.MapChangeOrdered += OnMapChangeOrdered;
        }

        public FlowState State { get; private set; } = FlowState.Idle;
        public FlowFailure? Failure { get; private set; }
        public SessionContext Context { get; }
        public LoginManager Login { get; }
        public CharacterManager Character { get; }
        public ZoneManager Zone { get; }

        public event Action<FlowState> StateChanged;

        /// <summary>
        /// The game must load <c>Context.Map</c> and call <see cref="MapLoaded"/>: raised in <c>LoadingMap</c> once the map server
        /// accepted the entry, and again for every map change, also one that arrives while a map is still loading (PLAN-0010 D15).
        /// </summary>
        public event Action MapLoadRequested;

        // ---- commands (main thread). Each validates the current state; otherwise it warns and is ignored. ----

        public void StartLogin(string user, string password)
        {
            if (State != FlowState.Idle) { Reject("StartLogin"); return; }

            Failure = null;
            Activate(Login, ConnectionRole.Login);
            Login.SetCredentials(user, password);
            BeginConnecting(FlowState.LoggingIn);
            _client.Connect(_loginServer.Host, _loginServer.Port, ConnectionRole.Login);
        }

        public void SelectServer(int index)
        {
            if (State != FlowState.ServerSelection) { Reject("SelectServer"); return; }
            if (index < 0 || index >= Context.Servers.Count) { Reject("SelectServer(" + index + "): no such server;"); return; }

            ServerEndpoint server = Context.Servers[index];
            Activate(Character, ConnectionRole.Character);
            BeginConnecting(FlowState.ConnectingToChar);
            _client.Connect(server.Host, server.Port, ConnectionRole.Character);
        }

        public void CreateCharacter(in CharacterDraft draft)
        {
            if (State != FlowState.CharSelection) { Reject("CreateCharacter"); return; }
            BeginWaitingForReply(FlowState.CreatingCharacter);
            Character.RequestCreate(in draft);
        }

        public void SelectCharacter(byte slot)
        {
            if (State != FlowState.CharSelection) { Reject("SelectCharacter"); return; }
            BeginWaitingForReply(FlowState.SelectingCharacter);
            Character.RequestSelect(slot);
        }

        /// <summary>The game finished loading the map scene. Valid in LoadingMap once the server accepted the entry.</summary>
        public void MapLoaded()
        {
            if (State != FlowState.LoadingMap || !_authenticated) { Reject("MapLoaded"); return; }
            ClearDeadline();
            Zone.SendMapLoaded();
            SetState(FlowState.InGame);
        }

        /// <summary>Failed or Disconnected back to Idle: clears the session and the failure, ready for a new login.</summary>
        public void Reset()
        {
            if (State != FlowState.Failed && State != FlowState.Disconnected) { Reject("Reset"); return; }
            Context.Reset();
            Failure = null;
            SetState(FlowState.Idle);
        }

        /// <summary>Called by <c>NetworkClient</c> after the pump: checks the deadline, then ticks the active manager (keepalives).</summary>
        public void Tick()
        {
            if (_disposed) return;
            long now = _clock.NowMs;
            if (_hasDeadline && now >= _deadlineAt)
            {
                TimeOut();
                return;
            }
            _active?.Tick(now);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _client.Connected -= OnConnected;
            _client.Disconnected -= OnDisconnected;
            Login.Accepted -= OnLoginAccepted;
            Login.Refused -= OnLoginRefused;
            Character.CharactersReceived -= OnCharactersReceived;
            Character.CreationSucceeded -= OnCreationFinished;
            Character.CreationFailed -= OnCreationFailed;
            Character.MapServerAssigned -= OnMapServerAssigned;
            Character.MapEnterRefused -= OnMapEnterRefused;
            Zone.Authenticated -= OnAuthenticated;
            Zone.EnterRefused -= OnEnterRefused;
            Zone.MapChangeOrdered -= OnMapChangeOrdered;

            DeactivateManager();
        }

        // ---- connection events ----

        private void OnConnected(ConnectionRole role)
        {
            if (_active == null || role != _activeRole) return;     // an event of a replaced connection is stale

            switch (State)
            {
                case FlowState.LoggingIn:
                case FlowState.ConnectingToChar:
                    _connectionUp = true;
                    StartDeadline(_settings.ReplySeconds);
                    _active.OnConnected();
                    break;

                case FlowState.ConnectingToMap:
                    _connectionUp = true;
                    StartDeadline(_settings.ReplySeconds);
                    SetState(FlowState.LoadingMap);
                    _active.OnConnected();
                    break;
            }
        }

        private void OnDisconnected(ConnectionRole role, CloseInfo info)
        {
            if (_active == null || role != _activeRole) return;     // not the connection in use

            switch (State)
            {
                case FlowState.LoggingIn:
                case FlowState.ConnectingToChar:
                case FlowState.CharSelection:
                case FlowState.CreatingCharacter:
                case FlowState.SelectingCharacter:
                case FlowState.ConnectingToMap:
                case FlowState.LoadingMap:
                    DeactivateManager();
                    ClearDeadline();
                    Fail(CodeFor(info), info?.Message);
                    break;

                case FlowState.InGame:
                    DeactivateManager();
                    SetState(FlowState.Disconnected);
                    break;
            }
        }

        private static FailureCode CodeFor(CloseInfo info)
        {
            switch (info?.Reason)
            {
                case CloseReason.ConnectFailed: return FailureCode.ConnectFailed;
                case CloseReason.RemoteClosed: return FailureCode.RemoteClosed;
                case CloseReason.ProtocolFault: return FailureCode.ProtocolFault;
                default: return FailureCode.TransportError;
            }
        }

        // ---- manager events ----

        private void OnLoginAccepted()
        {
            if (State != FlowState.LoggingIn) return;
            _client.Disconnect();                                   // the login connection is done
            DeactivateManager();
            ClearDeadline();
            SetState(FlowState.ServerSelection);
        }

        private void OnLoginRefused(LoginRefusal refusal)
        {
            if (State != FlowState.LoggingIn) return;
            _client.Disconnect();
            DeactivateManager();
            ClearDeadline();
            Fail(FailureCode.LoginRefused, refusal.ToString());     // no retry: the caller decides (FR-11)
        }

        private void OnCharactersReceived()
        {
            if (State != FlowState.ConnectingToChar) return;
            ClearDeadline();
            SetState(FlowState.CharSelection);
        }

        private void OnCreationFinished()
        {
            if (State != FlowState.CreatingCharacter) return;
            ClearDeadline();
            SetState(FlowState.CharSelection);
        }

        private void OnCreationFailed(byte reason) => OnCreationFinished();     // the reason stays on Character (FR-13)

        private void OnMapEnterRefused(byte reason)
        {
            if (State != FlowState.SelectingCharacter) return;
            ClearDeadline();
            SetState(FlowState.CharSelection);                      // the reason stays on Character.LastMapEnterRefusal
        }

        private void OnMapServerAssigned()
        {
            if (State != FlowState.SelectingCharacter) return;

            Activate(Zone, ConnectionRole.Map);                     // leaves the character manager, enters the zone manager
            _authenticated = false;
            BeginConnecting(FlowState.ConnectingToMap);
            _client.Connect(Context.Map.Host, Context.Map.Port, ConnectionRole.Map);
        }

        private void OnAuthenticated()
        {
            if (State != FlowState.LoadingMap) return;
            _authenticated = true;
            StartDeadline(_settings.MapLoadSeconds);                // the client's own scene load
            RequestMapLoad();
        }

        // The server moved the player on this map server (SPEC-0010 FR-15): load the map, then MAP_LOADED again.
        private void OnMapChangeOrdered()
        {
            if (State != FlowState.InGame && !(State == FlowState.LoadingMap && _authenticated)) return;
            _authenticated = true;
            _connectionUp = true;
            StartDeadline(_settings.MapLoadSeconds);
            SetState(FlowState.LoadingMap);
            RequestMapLoad();
        }

        private void RequestMapLoad()
        {
            try { MapLoadRequested?.Invoke(); }
            catch (Exception e) { NetLog.HandlerError(e); }          // as StateChanged
        }

        private void OnEnterRefused(byte code)
        {
            if (State != FlowState.ConnectingToMap && State != FlowState.LoadingMap) return;
            _client.Disconnect();
            DeactivateManager();
            ClearDeadline();
            Fail(FailureCode.MapRefused, "Map server refused the entry (code " + code + ")");
        }

        // ---- state, managers, deadlines ----

        private void BeginConnecting(FlowState waitingState)
        {
            _connectionUp = false;
            StartDeadline(_settings.ConnectSeconds);
            SetState(waitingState);
        }

        private void BeginWaitingForReply(FlowState waitingState)
        {
            StartDeadline(_settings.ReplySeconds);
            SetState(waitingState);
        }

        // Makes 'manager' the active one: leaves the previous one first. Called BEFORE its connection opens, so no packet is missed.
        private void Activate(IServerManager manager, ConnectionRole role)
        {
            DeactivateManager();
            _active = manager;
            _activeRole = role;
            manager.Enter();
        }

        private void DeactivateManager()
        {
            IServerManager manager = _active;
            _active = null;
            manager?.Exit();
        }

        private void TimeOut()
        {
            FailureCode code = _connectionUp ? FailureCode.ReplyTimeout : FailureCode.ConnectTimeout;
            string message = _connectionUp ? "The server did not reply in time" : "The connection was not established in time";
            _client.Disconnect();
            DeactivateManager();
            ClearDeadline();
            Fail(code, message + " (" + State + ")");
        }

        private void Fail(FailureCode code, string message)
        {
            Failure = new FlowFailure(code, message);
            NetLog.Warn("Flow failed in " + State + ": " + Failure.Value);
            SetState(FlowState.Failed);
        }

        private void StartDeadline(float seconds)
        {
            _hasDeadline = true;
            _deadlineAt = _clock.NowMs + (long)(seconds * 1000f);
        }

        private void ClearDeadline() => _hasDeadline = false;

        private void SetState(FlowState next)
        {
            if (State == next) return;
            FlowState previous = State;
            State = next;
            NetLog.Info("Flow " + previous + " -> " + next);
            try { StateChanged?.Invoke(next); }
            catch (Exception e) { NetLog.HandlerError(e); }          // a UI handler must not break the flow
        }

        private void Reject(string command) => NetLog.Warn(command + " ignored in state " + State);
    }
}
