using System;
using System.Threading;
using Uninarok.Login;
using Uninarok.Networking;
using Uninarok.Networking.Packets;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace Uninarok.Managers
{
    [AutoStaticsCleanup]
    public partial class GameConnectionManager : MonoBehaviour
    {
        public enum GameState
        {
            None,
            LoadingMap,
            Playing
        }
        [SerializeField] private float pingDelay = 1f;

        public static GameState GameConnectionState => _inst != null ? _inst.CurrentGameState : GameState.None;
        public GameState CurrentGameState
        {
            get => _currentGameState;
            set
            {
                _currentGameState = value;
                Debug.LogWarning(CurrentGameState);
            }
        }
        private GameState _currentGameState;
        private static GameConnectionManager _inst;

        private TcpConnection _connection;
        private CancellationTokenSource _cancellationToken = new CancellationTokenSource();

        private string _currentMapIp;
        private short _currentMapPort;
        private int _currentCharId;

        #region UNITY EVENTS
        private void Awake()
        {
            _inst = this;
        }

        private void OnEnable()
        {
            UninarokPackageDispatcher.Subscribe<CharacterServer.MAP_ENTER_REQUEST_ACCEPTED>(OnZoneServerInfoReceived);

            UninarokPackageDispatcher.Subscribe<MapServer.HEARTBEAT_RESPONSE>(OnZonePing);
        }  

        private void OnDisable()
        {
            UninarokPackageDispatcher.Subscribe<CharacterServer.MAP_ENTER_REQUEST_ACCEPTED>(OnZoneServerInfoReceived);

            UninarokPackageDispatcher.Unsubscribe<MapServer.HEARTBEAT_RESPONSE>(OnZonePing);
        }

        private void OnDestroy()
        {
            if (_connection != null && _connection.IsConnected)
            {
                _cancellationToken.Cancel();
                _connection.Dispose();
                _connection = null;
            }
        }

        private void Update()
        {
            ProcessPing();
            if (_connection != null && _connection.IsConnected)
            {
                _connection.SerializePackages();
            }
        }
        #endregion

        #region Callbacks
        private void OnZonePing(in MapServer.HEARTBEAT_RESPONSE structData)
        {
            Debug.Log($"Ping received with timestamp {structData.Timestamp}");
        }

        private void OnZoneServerInfoReceived(in CharacterServer.MAP_ENTER_REQUEST_ACCEPTED structData)
        {
            _currentMapIp = $"{structData.IpAddress[0]}.{structData.IpAddress[1]}.{structData.IpAddress[2]}.{structData.IpAddress[3]}";
            _currentMapPort = structData.Port;
            _currentCharId = structData.CharId;
        }
        #endregion

        public static void ConnectToCurrentMap()
        {
            if (!_inst) return;

            _inst.CurrentGameState = GameState.LoadingMap;
            _inst.ConnectToMapAsync(_inst._currentMapIp, _inst._currentMapPort);
        }
        public void ConnectToMap(byte[] ip, short port)
        {
            var loginHost = $"{ip[0]}.{ip[1]}.{ip[2]}.{ip[3]}";
            ConnectToMapAsync(loginHost, port);
        }

        private async void ConnectToMapAsync(string host, int port)
        {
            _connection = await TcpConnection.ConnectAsync(host, port, _cancellationToken.Token);

            //TODO: wait for loading
            //var packetVersion = new CZ.CZ_PACKET_DB
            //{
            //    PacketVer = (uint)LoginManager.PacketVersion
            //};
            //await _connection.SendAsync(packetVersion.Serialize(), CancellationToken.None);
            // _sendPing = true;
            // return;
            var enterMap = new MapServer.REQUEST_ENTER_MAP()
            {
                AccountId = LoginManager.Instance.AccountId,
                AuthCode = LoginManager.Instance.AuthCode,
                CharId = _currentCharId,
                Sex = LoginManager.Instance.Sex,
                Timestamp = (uint)Time.fixedUnscaledDeltaTime
            };
            await _connection.SendAsync(enterMap.Serialize(), CancellationToken.None);
        }

        // TODO: Consider trying async
        private float _pingDelayTimer;
        private bool _sendPing = false;
        private void ProcessPing()
        {
            if (_connection == null || !_connection.IsConnected || !_sendPing)
                return;

            _pingDelayTimer -= Time.deltaTime;
            if (_pingDelayTimer <= 0)
            {
                SendTickAsync();

                _pingDelayTimer = pingDelay;
            }
        }

        private async void SendTickAsync()
        {
            var ping = new MapServer.MAP_HEARTBEAT()
            {
                Timestamp = (uint)Time.unscaledTime
            };
            var bytes = new byte[MapServer.MAP_HEARTBEAT.Size];

            await _connection.SendAsync(new Memory<byte>(bytes), CancellationToken.None);
        }
    }
}
