using System;
using Uninarok.Networking.Client;
using Uninarok.Networking.Core.Dispatch;
using Uninarok.Networking.Data;
using Uninarok.Networking.Flow;
using Uninarok.Networking.Packets.In.Map;
using Uninarok.Networking.Packets.Out.Map;

namespace Uninarok.Networking.Managers
{
    /// <summary>
    /// Protocol logic of the map server (SPEC-0001 FR-6, FR-9, FR-14, FR-20; plan 4.9). Requests entry, stores what the
    /// server reports, announces that the map finished loading, and sends the keepalive once the session is in game.
    /// It never changes flow state and never touches a socket.
    /// </summary>
    public sealed class ZoneManager : IServerManager
    {
        private readonly INetworkClient _client;
        private readonly SessionContext _context;
        private readonly IClock _clock;
        private readonly NetworkSettings _settings;
        private readonly Subscriptions _subscriptions;

        private readonly PacketHandler<AUTHENTICATION_ACCEPTED> _onAuthenticated;
        private readonly PacketHandler<MAP_ENTRY_REFUSAL> _onRefusal;
        private readonly PacketHandler<SERVER_HEARTBEAT_RESPONSE> _onHeartbeatResponse;
        private readonly PacketHandler<MAP_CHANGE> _onMapChange;
        private readonly PacketHandler<MAP_SERVER_CHANGE> _onMapServerChange;

        // The keepalive starts at SendMapLoaded (the session is InGame, spec section 6) and stops at Exit.
        private bool _heartbeatActive;
        private long _nextHeartbeatAt;

        // The one outstanding heartbeat: its local send time gives the round trip (SPEC-0010 FR-2).
        private bool _heartbeatOutstanding;
        private long _heartbeatSentAt;

        public ZoneManager(INetworkClient client, SessionContext context, IClock clock, NetworkSettings settings)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _subscriptions = new Subscriptions(client);
            _onAuthenticated = OnAuthenticated;
            _onRefusal = OnRefusal;
            _onHeartbeatResponse = OnHeartbeatResponse;
            _onMapChange = OnMapChange;
            _onMapServerChange = OnMapServerChange;
        }

        /// <summary>AUTHENTICATION_ACCEPTED was stored in <c>Context.Entry</c>.</summary>
        public event Action Authenticated;

        /// <summary>The map server refused the entry (MAP_ENTRY_REFUSAL), with its reason code.</summary>
        public event Action<byte> EnterRefused;

        /// <summary>
        /// The server answered the outstanding heartbeat: its tick, and the local times the request was sent and the answer was
        /// handled (SPEC-0010 FR-2). An answer to nothing raises nothing.
        /// </summary>
        public event Action<uint, long, long> HeartbeatAnswered;

        /// <summary>
        /// The server moved the player to a map (or cell) of this map server: <c>Context.Map</c> has the new map and
        /// <c>Context.Entry</c> the new cell, facing north (SPEC-0010 FR-15, PLAN-0010 D14). The heartbeat stopped until the next
        /// <see cref="SendMapLoaded"/>.
        /// </summary>
        public event Action MapChangeOrdered;

        /// <summary>
        /// The server moved the player to another map server: <c>Context.Map</c> has its address and the map, <c>Context.Entry</c>
        /// the cell (SPEC-0010 FR-15). The flow reconnects.
        /// </summary>
        public event Action MapServerChangeOrdered;

        public void Enter()
        {
            _heartbeatActive = false;
            _heartbeatOutstanding = false;
            _subscriptions.Add(_onAuthenticated);
            _subscriptions.Add(_onRefusal);
            _subscriptions.Add(_onHeartbeatResponse);
            _subscriptions.Add(_onMapChange);
            _subscriptions.Add(_onMapServerChange);
        }

        public void Exit()
        {
            _subscriptions.Clear();
            _heartbeatActive = false;
            _heartbeatOutstanding = false;
        }

        /// <summary>The connection is up: request entry with the session values and the client tick.</summary>
        public void OnConnected()
        {
            _client.Send(new REQUEST_ENTER_MAP(_context.AccountId, _context.CharacterId, _context.AuthCode,
                                               (uint)_clock.NowMs, _context.Sex));
        }

        /// <summary>
        /// The game finished loading the map: send <c>MAP_LOADED</c> and start the heartbeat, with a first one at once so the
        /// server clock is measured before the first walks arrive (PLAN-0010 D6).
        /// </summary>
        public void SendMapLoaded()
        {
            _client.Send(new MAP_LOADED());
            _heartbeatActive = true;
            long now = _clock.NowMs;
            SendHeartbeat(now);
            _nextHeartbeatAt = now + IntervalMs();
        }

        /// <summary>
        /// <c>HEARTBEAT_REQUEST</c> with the monotonic millisecond timestamp every <c>MapKeepaliveSeconds</c>, only after
        /// <see cref="SendMapLoaded"/>. A long gap sends one heartbeat, not a burst. Allocates nothing.
        /// </summary>
        public void Tick(long nowMs)
        {
            if (!_heartbeatActive || nowMs < _nextHeartbeatAt) return;

            SendHeartbeat(nowMs);
            long interval = IntervalMs();
            _nextHeartbeatAt += interval;
            if (_nextHeartbeatAt <= nowMs) _nextHeartbeatAt = nowMs + interval;
        }

        // The answer carries no id. A request while one is unanswered (an answer later than the interval) is counted from the
        // older one, so a round trip can only look longer than it was, and the clock prefers the shortest (PLAN-0010 4.2).
        private void SendHeartbeat(long nowMs)
        {
            if (!_client.Send(new HEARTBEAT_REQUEST((uint)nowMs))) return;
            if (_heartbeatOutstanding) return;
            _heartbeatOutstanding = true;
            _heartbeatSentAt = nowMs;
        }

        private long IntervalMs()
        {
            long ms = (long)(_settings.MapKeepaliveSeconds * 1000f);
            return ms > 0 ? ms : 10_000;
        }

        // ---- handlers ----

        private void OnAuthenticated(in AUTHENTICATION_ACCEPTED p)
        {
            _context.Entry = new MapEntry(p.StartTimestamp, in p.Position, p.XSize, p.YSize);
            Authenticated?.Invoke();
        }

        private void OnRefusal(in MAP_ENTRY_REFUSAL p) => EnterRefused?.Invoke(p.ErrorCode);

        private void OnMapChange(in MAP_CHANGE p)
        {
            _heartbeatActive = false;
            _context.Map = new MapEndpoint(_context.Map.Host, _context.Map.Port, in p.MapName, in _context.Map.DnsHost);
            MoveEntry(p.X, p.Y);
            MapChangeOrdered?.Invoke();
        }

        private void OnMapServerChange(in MAP_SERVER_CHANGE p)
        {
            _heartbeatActive = false;
            _context.Map = new MapEndpoint(p.FormatIpAddress(), p.Port, in p.MapName, in p.DnsHost);
            MoveEntry(p.X, p.Y);
            MapServerChangeOrdered?.Invoke();
        }

        // The move packets carry no tick and no facing.
        private void MoveEntry(ushort x, ushort y)
        {
            var position = new UninarokPosition(x, y, UninarokDirection.North);
            _context.Entry = new MapEntry(0, in position, _context.Entry.XSize, _context.Entry.YSize);
        }

        // The reply to HEARTBEAT_REQUEST carries the server tick.
        private void OnHeartbeatResponse(in SERVER_HEARTBEAT_RESPONSE p)
        {
            if (!_heartbeatOutstanding) return;
            _heartbeatOutstanding = false;
            HeartbeatAnswered?.Invoke(p.ServerTimestamp, _heartbeatSentAt, _clock.NowMs);
        }
    }
}
