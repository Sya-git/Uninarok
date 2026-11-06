using UnityEngine;
using Uninarok.Networking.Packets.In;
using Uninarok.Networking.Packets.Out;
using Uninarok.Networking;
using System.Threading;

namespace Uninarok.Login
{
    public class LoginManager : MonoBehaviour
    {
        private enum LoginState
        {
            None,
            LoggingIn,
            OnServerSelection,
        }

        //TODO: In the future login host and login port should be probably gathered from a dns
        [SerializeField] private string loginHost = "127.0.0.1";
        [SerializeField] private int loginPort = 6900;
        [SerializeField] private int loginTimeout = 15;
        public static LoginManager Instance { get; private set; }

        private UninarokTcpConnection _connection;
        private uint _accountId;
        private int _authCode;
        private uint _userLevel;
        private uint _lastLoginIP;
        private string _lastLoginTime;
        private char _sex;
        private UninarokServerEndpoint[] _serverList;
        private LoginState _currentState = LoginState.None;


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
            AC.ACCEPT_LOGIN.Register(OnAcceptLogin);
        }

        private void OnDisable()
        {
            AC.ACCEPT_LOGIN.Unregister(OnAcceptLogin);
        }

        private void OnAcceptLogin(in AC.ACCEPT_LOGIN packet)
        {
            Debug.Log($"Login accepted! AuthCode: {packet.AuthCode}, AccountId: {packet.AccountId}, UserLevel: {packet.UserLevel}, LastLoginIP: {packet.LastLoginIP}, LastLoginTime: {packet.LastLoginTimeString}, Sex: {packet.Sex}");
            // Handle the login acceptance logic here, e.g., transition to the next scene or update UI
            _accountId = packet.AccountId;
            _authCode = packet.AuthCode;
            _userLevel = packet.UserLevel;
            _lastLoginIP = packet.LastLoginIP;
            _lastLoginTime = packet.LastLoginTimeString.ToString();
            _sex = (char)packet.Sex;
            //TODO: Needs to be a list
            _serverList = new UninarokServerEndpoint[] { packet.ServersEndpointList };

            _currentState = LoginState.OnServerSelection;

            _connection.Dispose();
        }

        private void OnDenyLogin(in AC.REFUSE_LOGIN packet)
        {
            Debug.Log($"Login denied!");
            // Handle the login denial logic here, e.g., show an error message to the user

            _connection.Dispose();
        }

        public void StartLogin(string username, string password)
        {
            LoginAsync(loginHost, loginPort, username, password);
        }

        public void SelectServerAtIndex(int index)
        {
            if (_currentState != LoginState.OnServerSelection)
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
        }

        private async void LoginAsync(string host, int port, string username, string password)
        {
            var _timeOut = new CancellationTokenSource();
            _timeOut.CancelAfter(loginTimeout * 1000); // Convert seconds to milliseconds
            
            _connection = await UninarokTcpConnection.ConnectAsync(host, port, _timeOut.Token);
            // Send login request
            var loginRequest = new CA.LOGIN(username, password, '0');

            // TODO: Add a cancellation token to the SendAsync method to handle timeouts or cancellations if needed.
            await _connection.SendAsync(loginRequest.Serialize(), CancellationToken.None);
        }

        private async void ConnectToCharacterAsync(string host, int port)
        {
            var _timeOut = new CancellationTokenSource();
            _timeOut.CancelAfter(loginTimeout * 1000); // Convert seconds to milliseconds
            _connection = await UninarokTcpConnection.ConnectAsync(host, port, _timeOut.Token);
            
            // Send character server enter request
            var characterServerEnterRequest = new CH.ENTER(_accountId, _authCode, _userLevel, (byte)_sex);

            await _connection.SendAsync(characterServerEnterRequest.Serialize(), CancellationToken.None);
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
