using System;
using Uninarok.Networking.Client;
using Uninarok.Networking.Core.Dispatch;
using Uninarok.Networking.Flow;
using Uninarok.Networking.Packets.In.Character;
using Uninarok.Networking.Packets.Out.Character;

namespace Uninarok.Networking.Managers
{
    /// <summary>
    /// Protocol logic of the character server (SPEC-0001 FR-3, FR-9, FR-12, FR-13, FR-14, FR-20; plan 4.9). Requests entry,
    /// keeps the character list, PIN state and map assignment in the session, and sends the keepalive.
    /// It never changes flow state and never touches a socket.
    /// </summary>
    public sealed class CharacterManager : IServerManager
    {
        private readonly INetworkClient _client;
        private readonly SessionContext _context;
        private readonly NetworkSettings _settings;
        private readonly Subscriptions _subscriptions;

        private readonly PacketHandler<CHARACTER_DATA> _onCharacterData;
        private readonly PacketHandler<CHARACTER_SLOTS_INFO> _onSlots;
        private readonly PacketHandler<PINCODE_LOGINSTATE> _onPin;
        private readonly PacketHandler<CHARACTER_CREATION_SUCCESS> _onCreated;
        private readonly PacketHandler<CHARACTER_CREATION_FAILED> _onCreationFailed;
        private readonly PacketHandler<MAP_ENTER_REQUEST_ACCEPTED> _onMapAccepted;
        private readonly PacketHandler<MAP_ENTER_REQUEST_REFUSED> _onMapRefused;

        // Keepalive: runs from OnConnected until Exit. The first Tick after OnConnected schedules the first ping.
        private bool _keepaliveActive;
        private bool _scheduleUnset;
        private long _nextPingAt;

        public CharacterManager(INetworkClient client, SessionContext context, NetworkSettings settings)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _subscriptions = new Subscriptions(client);
            _onCharacterData = OnCharacterData;
            _onSlots = OnSlots;
            _onPin = OnPin;
            _onCreated = OnCreated;
            _onCreationFailed = OnCreationFailed;
            _onMapAccepted = OnMapAccepted;
            _onMapRefused = OnMapRefused;
        }

        /// <summary><see cref="Managers.PinState.Unknown"/> on <see cref="Enter"/> and until the server reports it: never a default zero.</summary>
        public PinState PinState { get; private set; } = PinState.Unknown;

        /// <summary>Reason code of the last character-creation failure, or -1 when there was none since <see cref="Enter"/>.</summary>
        public int LastCreationFailure { get; private set; } = -1;

        /// <summary>Reason code of the last refused map-enter request, or -1 when there was none since <see cref="Enter"/>.</summary>
        public int LastMapEnterRefusal { get; private set; } = -1;

        public event Action<PinState> PinStateChanged;
        /// <summary><c>Context.Characters</c> was filled from the server's list.</summary>
        public event Action CharactersReceived;
        /// <summary>A character was created and added to <c>Context.Characters</c>.</summary>
        public event Action CreationSucceeded;
        public event Action<byte> CreationFailed;
        /// <summary><c>Context.Map</c> and <c>Context.CharacterId</c> are filled: connect to the map server.</summary>
        public event Action MapServerAssigned;
        /// <summary>The request to enter the map was refused; the flow returns to character selection.</summary>
        public event Action<byte> MapEnterRefused;

        public void Enter()
        {
            PinState = PinState.Unknown;
            LastCreationFailure = -1;
            LastMapEnterRefusal = -1;
            _keepaliveActive = false;

            _subscriptions.Add(_onCharacterData);
            _subscriptions.Add(_onSlots);
            _subscriptions.Add(_onPin);
            _subscriptions.Add(_onCreated);
            _subscriptions.Add(_onCreationFailed);
            _subscriptions.Add(_onMapAccepted);
            _subscriptions.Add(_onMapRefused);
        }

        public void Exit()
        {
            _subscriptions.Clear();
            _keepaliveActive = false;
        }

        /// <summary>
        /// The connection is up. The server first echoes the 4-byte account ID, which is not a packet, so the skip is
        /// registered BEFORE the entry request that provokes it (FR-3); then the keepalive starts.
        /// </summary>
        public void OnConnected()
        {
            _client.SkipNextBytes(4);
            _client.Send(new CHARACTER_SERVER_REQUEST_ENTRY(_context.AccountId, _context.AuthCode, _context.UserLevel, _context.Sex));
            _keepaliveActive = true;
            _scheduleUnset = true;
        }

        public void RequestCreate(in CharacterDraft draft)
        {
            _client.Send(new CREATE_CHARACTER(in draft.Name, draft.Slot, draft.HairColor, draft.HairStyle, draft.JobClass, draft.Sex));
        }

        public void RequestSelect(byte slot)
        {
            _context.SelectedSlot = slot;
            _client.Send(new SELECT_CHARACTER(slot));
        }

        /// <summary>
        /// <c>CHARACTER_SERVER_PING</c> with the account ID every <c>CharacterKeepaliveSeconds</c> while connected to this
        /// server. A long gap sends one ping, not a burst. Allocates nothing.
        /// </summary>
        public void Tick(long nowMs)
        {
            if (!_keepaliveActive) return;
            long interval = (long)(_settings.CharacterKeepaliveSeconds * 1000f);
            if (interval <= 0) return;

            if (_scheduleUnset)
            {
                _nextPingAt = nowMs + interval;
                _scheduleUnset = false;
                return;
            }
            if (nowMs < _nextPingAt) return;

            _client.Send(new CHARACTER_SERVER_PING(_context.AccountId));
            _nextPingAt += interval;
            if (_nextPingAt <= nowMs) _nextPingAt = nowMs + interval;
        }

        // ---- handlers ----

        private void OnCharacterData(in CHARACTER_DATA p)
        {
            _context.Characters.Clear();
            _context.Characters.AddRange(p.Characters);
            CharactersReceived?.Invoke();
        }

        private void OnSlots(in CHARACTER_SLOTS_INFO p)
        {
            _context.Slots = new CharacterSlots(p.CharSlots, p.AvailableCharSlots, p.CharSlots2, p.CharSlots3);
        }

        private void OnPin(in PINCODE_LOGINSTATE p)
        {
            PinState = (PinState)p.State;
            PinStateChanged?.Invoke(PinState);
        }

        private void OnCreated(in CHARACTER_CREATION_SUCCESS p)
        {
            _context.Characters.Add(p.Character);
            CreationSucceeded?.Invoke();
        }

        private void OnCreationFailed(in CHARACTER_CREATION_FAILED p)
        {
            LastCreationFailure = p.ErrorCode;
            CreationFailed?.Invoke(p.ErrorCode);
        }

        private void OnMapAccepted(in MAP_ENTER_REQUEST_ACCEPTED p)
        {
            _context.CharacterId = p.CharId;
            _context.Map = new MapEndpoint(p.FormatIpAddress(), p.Port, in p.MapName, in p.DnsHost);
            MapServerAssigned?.Invoke();
        }

        private void OnMapRefused(in MAP_ENTER_REQUEST_REFUSED p)
        {
            LastMapEnterRefusal = p.ErrorCode;
            MapEnterRefused?.Invoke(p.ErrorCode);
        }
    }
}
