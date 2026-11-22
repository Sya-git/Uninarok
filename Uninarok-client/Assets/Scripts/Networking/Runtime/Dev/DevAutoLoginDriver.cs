#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Unity.Collections;
using Uninarok.Networking.Client;
using Uninarok.Networking.Data;
using Uninarok.Networking.Flow;
using Uninarok.Networking.Managers;

namespace Uninarok.Networking.Dev
{
    /// <summary>What <see cref="DevAutoLoginDriver"/> does at each step. Every field has an effect.</summary>
    public struct DevAutoLoginSettings
    {
        public int ServerIndex;
        public byte CharacterSlot;
        public CharacterDraft Draft;
        public int MaxLoginAttempts;
        /// <summary>Finish the map load at once, for runs with no world to load it (PLAN-0010 §7): the network probe.</summary>
        public bool CompleteMapLoadWithoutWorld;
    }

    /// <summary>
    /// Drives the flow for local testing (SPEC-0001 FR-15; plan 7.1): login, pick a server, create or pick a character,
    /// and, only where there is no world, finish the map load. Each action is issued once per state entry; a failed creation or a refused map entry is not
    /// retried in a loop; a failed login is retried only while attempts remain. Plain C# so it runs in tests with the real flow.
    /// </summary>
    public sealed class DevAutoLoginDriver : IDisposable
    {
        private readonly NetworkFlow _flow;
        private readonly DevAutoLoginSettings _settings;
        private readonly Func<DevCredentials?> _credentials;

        private FlowState? _actedFor;       // the state entry already handled
        private int _attempt;               // logins started so far
        private bool _createTried;          // one creation attempt per login
        private bool _selectTried;          // one character selection per login
        private bool _started;
        private bool _disposed;

        public DevAutoLoginDriver(NetworkFlow flow, DevAutoLoginSettings settings, Func<DevCredentials?> credentials)
        {
            _flow = flow ?? throw new ArgumentNullException(nameof(flow));
            _settings = settings;
            _credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));

            _flow.StateChanged += OnStateChanged;
            _flow.Character.PinStateChanged += OnPinStateChanged;
            _flow.MapLoadRequested += OnMapLoadRequested;
        }

        /// <summary>Starts the first login. Does nothing (with a warning) when no credentials are set.</summary>
        public void Start()
        {
            if (_started || _disposed) return;
            _started = true;
            BeginLogin();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _flow.StateChanged -= OnStateChanged;
            _flow.Character.PinStateChanged -= OnPinStateChanged;
            _flow.MapLoadRequested -= OnMapLoadRequested;
        }

        private void BeginLogin()
        {
            DevCredentials? credentials = _credentials();
            if (credentials == null || !credentials.Value.IsValid)
            {
                NetLog.Warn("DevAutoLogin: no credentials set (EditorPrefs or " + DevCredentialsSource.FileName + "); doing nothing.");
                return;
            }

            _attempt++;
            _createTried = false;
            _selectTried = false;
            _actedFor = null;
            _flow.StartLogin(credentials.Value.User, credentials.Value.Password);
        }

        private void OnStateChanged(FlowState state)
        {
            if (_disposed || !_started) return;
            _actedFor = null;                                           // a new state entry: one action is allowed again

            switch (state)
            {
                case FlowState.ServerSelection:
                    Act(state, () => _flow.SelectServer(_settings.ServerIndex));
                    break;

                case FlowState.CharSelection:
                    DecideInCharSelection();
                    break;

                case FlowState.Failed:
                    NetLog.Warn("DevAutoLogin: flow failed: " + _flow.Failure);
                    if (_attempt < _settings.MaxLoginAttempts)
                    {
                        Act(state, () =>
                        {
                            _flow.Reset();
                            BeginLogin();
                        });
                    }
                    break;
            }
        }

        private void OnPinStateChanged(PinState pin)
        {
            if (_disposed || !_started) return;
            if (_flow.State == FlowState.CharSelection) DecideInCharSelection();   // the PIN may arrive after the character list
        }

        private void OnMapLoadRequested()
        {
            if (_disposed || !_started || !_settings.CompleteMapLoadWithoutWorld) return;
            Act(FlowState.LoadingMap, () => _flow.MapLoaded());
        }

        private void DecideInCharSelection()
        {
            if (_actedFor == FlowState.CharSelection) return;

            if (_flow.Context.Characters.Count > 0)
            {
                if (_selectTried)
                {
                    NetLog.Warn("DevAutoLogin: the character selection was refused; not trying again.");
                    _actedFor = FlowState.CharSelection;
                    return;
                }
                _selectTried = true;
                Act(FlowState.CharSelection, () => _flow.SelectCharacter(_settings.CharacterSlot));
                return;
            }

            // No character yet: create one, but only once the PIN state is known and allows it.
            PinState pin = _flow.Character.PinState;
            if (pin == PinState.Unknown) return;                         // wait for PINCODE_LOGINSTATE

            if (_createTried)
            {
                NetLog.Warn("DevAutoLogin: character creation failed (code " + _flow.Character.LastCreationFailure + "); not trying again.");
                _actedFor = FlowState.CharSelection;
                return;
            }
            if (pin != PinState.Ok && pin != PinState.Unused)
            {
                NetLog.Warn("DevAutoLogin: PIN state " + pin + " does not allow creating a character; stopping.");
                _actedFor = FlowState.CharSelection;
                return;
            }

            _createTried = true;
            Act(FlowState.CharSelection, () =>
            {
                CharacterDraft draft = _settings.Draft;
                _flow.CreateCharacter(in draft);
            });
        }

        // Marks the state entry as handled BEFORE acting: the action itself changes the state and may re-enter this class.
        private void Act(FlowState state, Action action)
        {
            if (_actedFor == state) return;
            _actedFor = state;
            action();
        }
    }
}
#endif
