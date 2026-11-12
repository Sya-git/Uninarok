using System;
using System.Threading;
using Uninarok.Networking;
using Uninarok.Networking.Packets;
using Uninarok.Networking.Packets.In;
using Uninarok.Networking.Packets.Out;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace Uninarok.Login
{
    [NoAutoStaticsCleanup]
    public class LoginManager : MonoBehaviour
    {
        public static int PacketVersion = 20220406;
        // Versoes a considerar: 20200401, 20220406, 20190530
        public enum LoginState
        {
            None,
            LoggingIn,
            OnServerSelection,
            LoggingToCharacterServer,
            OnEmptyCharacterSelection,
            CreatingCharacter,
            OnCharacterSelection,
            LoggingToZone,
            Playing
        }

        public enum PincodeState : short
        {
            None = -1,
            PinCorrect = 0,
            AskForPin, // client sends 0x8b8
            CreateNewPin, // client sends 0x8ba
            PinMustBeChanged, // client 0x8be
            CreateNewPin2, // client sends 0x8ba
            Unknown, // client shows msgstr(1896)
            Unknown2, // client shows msgstr(1897) Unable to use your KSSN number
            CharSelectionShowsButton, // char select window shows a button - client sends 0x8c5
            PinIncorrect
        }

        //TODO: In the future login host and login port should be probably gathered from a dns
        [SerializeField] private string loginHost = "127.0.0.1";
        [SerializeField] private int loginPort = 6900;
        [SerializeField] private int loginTimeout = 15;
        public static LoginManager Instance { get; private set; }

        private TcpConnection _connection;
        public uint AccountId { get; private set; }
        public int AuthCode { get; private set; }
        public uint UserLevel { get; private set; }
        public uint LastLoginIP { get; private set; }
        public string LastLoginTime { get; private set; }
        public byte Sex { get; private set; }
        public uint CurrentCharacterId { get; private set; }
        private Networking.Packets.In.LoginServer.ACCEPT_LOGIN.UninarokServerEndpoint[] _serverList;
        public LoginState CurrentState
        {
            get => _currentState;
            private set { _currentState = value; Debug.LogWarning(_currentState); }
        }
        private LoginState _currentState;
        public PincodeState PinState 
        { 
            get => _currentPincodeState;
            private set { _currentPincodeState = value; Debug.LogWarning(_currentPincodeState); }
        }
        private PincodeState _currentPincodeState;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            UninarokPackageDispatcher.Subscribe<Networking.Packets.In.LoginServer.ACCEPT_LOGIN>(OnAcceptLogin);
            UninarokPackageDispatcher.Subscribe<Networking.Packets.In.LoginServer.REFUSE_LOGIN>(OnLoginRefused);
            UninarokPackageDispatcher.Subscribe<Networking.Packets.In.LoginServer.REFUSE_LOGIN_3>(OnLoginRefused);
            UninarokPackageDispatcher.Subscribe<Networking.Packets.CharacterServer.MAP_ENTER_REQUEST_REFUSED>(OnCharacterRefuse);

            UninarokPackageDispatcher.Subscribe<Networking.Packets.CharacterServer.CHARACTER_DATA>(OnCharData);
            UninarokPackageDispatcher.Subscribe<Networking.Packets.CharacterServer.CHARACTER_CREATION_SUCCESS>(OnCharacterCreationSuccess);
            UninarokPackageDispatcher.Subscribe<Networking.Packets.CharacterServer.CHARACTER_CREATION_FAILED>(OnCharacterCreationFailed);
            UninarokPackageDispatcher.Subscribe<Networking.Packets.CharacterServer.PINCODE_LOGINSTATE>(OnPincodeState);
            UninarokPackageDispatcher.Subscribe<Networking.Packets.CharacterServer.MAP_ENTER_REQUEST_ACCEPTED>(OnZoneServerInfoReceived);
            //UninarokPackageDispatcher.Subscribe<HC.enter>
        }

        private void OnDisable()
        {
            UninarokPackageDispatcher.Unsubscribe<Networking.Packets.In.LoginServer.ACCEPT_LOGIN>(OnAcceptLogin);
            UninarokPackageDispatcher.Unsubscribe<Networking.Packets.In.LoginServer.REFUSE_LOGIN>(OnLoginRefused);
            UninarokPackageDispatcher.Unsubscribe<Networking.Packets.In.LoginServer.REFUSE_LOGIN_3>(OnLoginRefused);
            UninarokPackageDispatcher.Unsubscribe<Networking.Packets.CharacterServer.MAP_ENTER_REQUEST_REFUSED>(OnCharacterRefuse);

            UninarokPackageDispatcher.Unsubscribe<Networking.Packets.CharacterServer.CHARACTER_DATA>(OnCharData);
            UninarokPackageDispatcher.Unsubscribe<Networking.Packets.CharacterServer.CHARACTER_CREATION_SUCCESS>(OnCharacterCreationSuccess);
            UninarokPackageDispatcher.Unsubscribe<Networking.Packets.CharacterServer.CHARACTER_CREATION_FAILED>(OnCharacterCreationFailed);
            UninarokPackageDispatcher.Unsubscribe<Networking.Packets.CharacterServer.PINCODE_LOGINSTATE>(OnPincodeState);
            UninarokPackageDispatcher.Unsubscribe<Networking.Packets.CharacterServer.MAP_ENTER_REQUEST_ACCEPTED>(OnZoneServerInfoReceived);
        }

        private void OnDestroy()
        {
            if (_connection != null)
            {
                _connection.Dispose();
            }
        }

        #region PACKET CALLBACKS
        private void OnAcceptLogin(in Networking.Packets.In.LoginServer.ACCEPT_LOGIN packet)
        {
            Debug.Log($"Login accepted! AuthCode: {packet.AuthCode}, AccountId: {packet.AccountId}, UserLevel: {packet.UserLevel}, LastLoginIP: {packet.LastLoginIP}, LastLoginTime: {packet.LastLoginTimeString}, Sex: {packet.Sex}");
            Debug.Log($"First server info: {packet.ServersEndpointList[0].Address}:{packet.ServersEndpointList[0].Port} ({packet.ServersEndpointList[0].ServerName}))");
            // Handle the login acceptance logic here, e.g., transition to the next scene or update UI
            AccountId = packet.AccountId;
            AuthCode = packet.AuthCode;
            UserLevel = packet.UserLevel;
            LastLoginIP = packet.LastLoginIP;
            LastLoginTime = packet.LastLoginTimeString.ToString();
            Sex = packet.Sex;
            //TODO: Needs to be a list
            _serverList = new Networking.Packets.In.LoginServer.ACCEPT_LOGIN.UninarokServerEndpoint[packet.ServersEndpointList.Length];
            packet.ServersEndpointList.CopyTo(_serverList, 0);

            CurrentState = LoginState.OnServerSelection;

            _connection.Dispose();
        }

        private void OnCharData(in Networking.Packets.CharacterServer.CHARACTER_DATA packet)
        {
            if (packet.CharsInfo.Length == 0)
            {
                CurrentState = LoginState.OnEmptyCharacterSelection;
            }
            else
            {
                CurrentState = LoginState.OnCharacterSelection;
            }
        }

        private void OnPincodeState(in Networking.Packets.CharacterServer.PINCODE_LOGINSTATE structData)
        {
            PinState = (PincodeState)structData.State;
        }

        private void OnCharacterRefuse(in Networking.Packets.CharacterServer.MAP_ENTER_REQUEST_REFUSED refuseData)
        {
            Debug.LogError($"Login to character map refused with reason: {refuseData.ErrorCode}");
        }

        private void OnCharacterCreationFailed(in Networking.Packets.CharacterServer.CHARACTER_CREATION_FAILED packet)
        {
            Debug.LogError($"Character could not be created: {(Networking.Packets.CharacterServer.CHARACTER_CREATION_FAILED.CharCreationFailedReason)packet.ErrorCode}");
        }

        private void OnCharacterCreationSuccess(in Networking.Packets.CharacterServer.CHARACTER_CREATION_SUCCESS packet)
        {
            Debug.LogError($"Character {packet.CharInfo.Name} created successfully");
        }

        private void OnZoneServerInfoReceived(in Networking.Packets.CharacterServer.MAP_ENTER_REQUEST_ACCEPTED packet)
        {
            Debug.LogError($"Received zone server info: {packet.IpAddress}:{packet.Port}, {packet.MapName}, {packet.DnsHost}");
            CurrentState = LoginState.Playing;
        }

        private void OnLoginRefused(in Networking.Packets.In.LoginServer.REFUSE_LOGIN_3 packet)
        {
            Debug.LogWarning($"Login denied! Error code: {packet.ErrorCode}");
            // Handle the login denial logic here, e.g., show an error message to the user


            CurrentState = LoginState.None;
            _connection.Dispose();
            _connection = null;
        }

        private void OnLoginRefused(in Networking.Packets.In.LoginServer.REFUSE_LOGIN packet)
        {
            Debug.LogWarning($"Login denied! Error code");
            // Handle the login denial logic here, e.g., show an error message to the user


            CurrentState = LoginState.None;
            _connection.Dispose();
            _connection = null;
        }
        #endregion

        #region PUBLIC METHODS
        public void StartLogin(string username, string password)
        {
            if (CurrentState != LoginState.None)
            {
                return;
            }

            CurrentState = LoginState.LoggingIn;
            LoginAsync(loginHost, loginPort, username, password);
        }

        public void SelectServerAtIndex(int index)
        {
            if (CurrentState != LoginState.OnServerSelection)
            {
                Debug.LogWarning("Cannot select server. Not in server selection state.");
                return;
            }

            if (index < 0 || index >= _serverList.Length)
            {
                Debug.LogWarning("Invalid server index.");
                return;
            }

            var selectedServer = _serverList[index];
            var host = selectedServer.Address.ToString();
            var port = selectedServer.Port;

            CurrentState = LoginState.LoggingToCharacterServer;
            ConnectToCharacterAsync(host, port);
        }

        //TODO: PINCODE
        public void SetupFirstPin()
        {

        }

        public void CreateCharacter(string charName, byte slot, ushort hairColor, ushort hairStyle, ushort job, Networking.Packets.In.LoginServer.AccountSex sex)
        {
            // TODO: GC
            CurrentState = LoginState.CreatingCharacter;
            SendCreateCharacterAsync(charName, slot, hairColor, hairStyle, job, (byte)sex);
        }

        public void SelectCharacterAtIndex(byte index)
        {
            CurrentState = LoginState.LoggingToZone;
            SendSelectCharacterAsync(index);
        }
        #endregion

        private async void LoginAsync(string host, int port, string username, string password)
        {
            var _timeOut = new CancellationTokenSource();
            _timeOut.CancelAfter(loginTimeout * 1000); // Convert seconds to milliseconds

            _connection = await TcpConnection.ConnectAsync(host, port, _timeOut.Token);
            // Send login request
            var loginRequest = new Networking.Packets.Out.LoginServer.REQUEST_LOGIN(username, password, '0');

            // TODO: Add a cancellation token to the SendAsync method to handle timeouts or cancellations if needed.
            await _connection.SendAsync(loginRequest.Serialize(), CancellationToken.None);
        }

        private async void ConnectToCharacterAsync(string host, int port)
        {
            var timeout = new CancellationTokenSource();
            timeout.CancelAfter(loginTimeout * 1000); // Convert seconds to milliseconds
            _connection = await TcpConnection.ConnectAsync(host, port, timeout.Token);

            // Send character server enter request
            var characterServerEnterRequest = new Networking.Packets.Out.CharacterServer.CHARACTER_SERVER_REQUEST_ENTRY(AccountId, AuthCode, UserLevel, Sex);
            _connection.IgnoreBytes(4);
            await _connection.SendAsync(characterServerEnterRequest.Serialize(), CancellationToken.None);
        }

        private async void SendCreateCharacterAsync(string charName, byte slot, ushort hairColor, ushort hairStyle, ushort job, byte sex)
        {
            var packet = new Networking.Packets.Out.CharacterServer.CREATE_CHARACTER(charName, slot, hairColor, hairStyle, job, sex);
            var bytes = new Memory<byte>(new byte[Networking.Packets.Out.CharacterServer.CREATE_CHARACTER.Size]);
            packet.Serialize(bytes);

            await _connection.SendAsync(bytes, CancellationToken.None);
        }

        private async void SendSelectCharacterAsync(byte slotIndex)
        {
            var packet = new Networking.Packets.Out.CharacterServer.SELECT_CHARACTER(slotIndex);
            var bytes = new Memory<byte>(new byte[Networking.Packets.Out.CharacterServer.SELECT_CHARACTER.Size]);
            packet.Serialize(bytes.Span);

            await _connection.SendAsync(bytes, CancellationToken.None);
        }

        private void Update()
        {
            if (_connection != null && _connection.IsConnected)
            {
                _connection.SerializePackages();
            }
        }
    }
}
