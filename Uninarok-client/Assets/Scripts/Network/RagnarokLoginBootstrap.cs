using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Uninarok.Networking.Packets.In;
using UnityEngine;

namespace Uninarok.Networking
{
    public sealed class RagnarokLoginBootstrap : MonoBehaviour
    {
        public enum LoginState
        {
            Idle,
            ConnectingToLogin,
            Authenticating,
            ConnectingToCharacter,
            SelectingCharacter,
            CreatingCharacter,
            ConnectingToMap,
            EnteringMap,
            MapReady,
            Failed
        }

        [Header("Login server")]
        [SerializeField] private string loginHost = "127.0.0.1";
        [SerializeField] private int loginPort = 6900;
        [SerializeField] private string accountName;
        [SerializeField, Tooltip("Development only: Unity scene data is not a secure place to store account passwords.")]
        private string accountPassword;

        [Header("Character server")]
        [SerializeField] private int characterServerIndex;
        [SerializeField] private string characterServerHostOverride;

        [Header("Character creation")]
        [SerializeField] private byte characterSlot;
        [SerializeField] private string characterName = "NewCharacter";
        [SerializeField] private ushort jobClass;
        [SerializeField] private ushort hairColor;
        [SerializeField] private ushort hairStyle;
        [SerializeField] private byte sex = 1;

        [Header("Map server")]
        [SerializeField] private string mapServerHostOverride;
        [SerializeField] private float timeoutSeconds = 20f;

        private readonly byte[] _sendBuffer = new byte[64];
        private CancellationTokenSource _shutdown;
        private RagnarokTcpConnection _mapConnection;

        public LoginState State { get; private set; }
        public bool HasEnteredMap => State == LoginState.MapReady;

        private UninarokTcpConnection _currentConnection;

        private async void Start()
        {
            if (string.IsNullOrWhiteSpace(loginHost) || loginPort < 1 || loginPort > ushort.MaxValue)
            {
                Fail("Login server host or port is invalid.");
                return;
            }

            _shutdown = new CancellationTokenSource();
            _shutdown.CancelAfter(TimeSpan.FromSeconds(Math.Max(1f, timeoutSeconds)));
            try
            {
                //await LoginAsync(_shutdown.Token);
            }
            catch (OperationCanceledException)
            {
                Fail("Ragnarok login timed out or was cancelled.");
            }
            catch (Exception exception)
            {
                Fail(exception.Message);
            }
        }

        private void OnEnable()
        {
            AC.ACCEPT_LOGIN.Register(OnLoginAccepted);
        }

        private void OnDisable()
        {
            AC.ACCEPT_LOGIN.Unregister(OnLoginAccepted);
        }

        private void OnLoginAccepted(in AC.ACCEPT_LOGIN packet)
        {
            
        }

        private async void StartLogin()
        {
            //_currentConnection = UninarokTcpConnection.ConnectAsync(loginHost, loginPort);
        }

        private async Task LoginAsync(CancellationToken cancellationToken)
        {
            State = LoginState.ConnectingToLogin;
            uint accountId;
            int authCode;
            uint userLevel;
            byte accountSex;
            UninarokServerEndpoint characterEndpoint;

            using (RagnarokTcpConnection login = await RagnarokTcpConnection.ConnectAsync(loginHost, loginPort, cancellationToken))
            {
                State = LoginState.Authenticating;
                RagnarokPacketCodec.WriteLogin(_sendBuffer, accountName, accountPassword);
                await login.SendAsync(_sendBuffer, 55, cancellationToken);

                RagnarokPacket response = await login.ReadPacketAsync(cancellationToken);
                var loginAccepted = new AC.ACCEPT_LOGIN(response.Buffer);
                RequirePacket(response, RagnarokPacketId.LoginAccepted, "Login server rejected or returned an unsupported response");
                if (!RagnarokPacketCodec.TryGetCharacterServer(response.Buffer, response.Length, characterServerIndex, out characterEndpoint))
                    throw new InvalidDataException("The login response contains no usable character server entry.");

                accountId = RagnarokPacketCodec.ReadAccountId(response.Buffer);
                authCode = RagnarokPacketCodec.ReadAuthCode(response.Buffer);
                userLevel = RagnarokPacketCodec.ReadUserLevel(response.Buffer);
                accountSex = RagnarokPacketCodec.ReadSex(response.Buffer);
            }

            string characterHost = string.IsNullOrWhiteSpace(characterServerHostOverride)
                ? characterEndpoint.Address.ToString()
                : characterServerHostOverride;

            State = LoginState.ConnectingToCharacter;
            using (RagnarokTcpConnection character = await RagnarokTcpConnection.ConnectAsync(characterHost, characterEndpoint.Port, cancellationToken))
            {
                RagnarokPacketCodec.WriteCharacterServerEnter(_sendBuffer, accountId, authCode, userLevel, accountSex);
                await character.SendAsync(_sendBuffer, 17, cancellationToken);

                State = LoginState.SelectingCharacter;
                RagnarokPacket characters = await character.ReadPacketAsync(cancellationToken);
                RequirePacket(characters, RagnarokPacketId.CharacterList, "Character server rejected the account or sent an unsupported response");
                int characterCount = RagnarokPacketCodec.GetCharacterCount(characters.Buffer, characters.Length);
                byte slot;

                if (characterCount == 0)
                {
                    if (characterSlot > 8)
                        throw new InvalidDataException("Character slot must be in the range 0-8 for this initial bootstrap.");
                    if (string.IsNullOrWhiteSpace(characterName))
                        throw new InvalidDataException("A character name is required when the account has no characters.");

                    State = LoginState.CreatingCharacter;
                    RagnarokPacketCodec.WriteCreateCharacter(_sendBuffer, characterName, characterSlot, hairColor, hairStyle, jobClass, sex);
                    await character.SendAsync(_sendBuffer, 36, cancellationToken);
                    RagnarokPacket created = await character.ReadPacketAsync(cancellationToken);
                    RequirePacket(created, RagnarokPacketId.CharacterCreated, "Character creation failed or was rejected by the server");
                    slot = RagnarokPacketCodec.ReadCreatedCharacterSlot(created.Buffer);
                }
                else
                {
                    slot = RagnarokPacketCodec.FindCharacterSlot(characters.Buffer, characters.Length, characterSlot);
                }

                State = LoginState.SelectingCharacter;
                RagnarokPacketCodec.WriteSelectCharacter(_sendBuffer, slot);
                await character.SendAsync(_sendBuffer, 3, cancellationToken);

                RagnarokPacket redirect = await character.ReadPacketAsync(cancellationToken);
                RequirePacket(redirect, RagnarokPacketId.MapServerRedirect, "Character selection failed or the character server sent an unsupported response");
                if (!RagnarokPacketCodec.TryGetMapEndpoint(redirect.Buffer, redirect.Length, out UninarokServerEndpoint mapEndpoint))
                    throw new InvalidDataException("The character server returned an invalid map server address.");

                string mapHost = string.IsNullOrWhiteSpace(mapServerHostOverride)
                    ? mapEndpoint.Address.ToString()
                    : mapServerHostOverride;
                uint characterId = RagnarokPacketCodec.ReadCharacterId(redirect.Buffer);

                State = LoginState.ConnectingToMap;
                RagnarokTcpConnection map = await RagnarokTcpConnection.ConnectAsync(mapHost, mapEndpoint.Port, cancellationToken);
                bool keepConnection = false;
                try
                {
                    State = LoginState.EnteringMap;
                    RagnarokPacketCodec.WriteMapEnter(_sendBuffer, accountId, characterId, authCode, unchecked((uint)Environment.TickCount), accountSex);
                    await map.SendAsync(_sendBuffer, 19, cancellationToken);

                    RagnarokPacket mapResponse = await map.ReadPacketAsync(cancellationToken);
                    RequirePacket(mapResponse, RagnarokPacketId.MapEnterAccepted, "Map server rejected entry or sent an unsupported response");
                    _mapConnection = map;
                    keepConnection = true;
                    State = LoginState.MapReady;
                    Debug.Log("Ragnarok login completed; map server accepted the character.", this);
                }
                finally
                {
                    if (!keepConnection)
                        map.Dispose();
                }
            }
        }

        private static void RequirePacket(RagnarokPacket packet, RagnarokPacketId expected, string message)
        {
            if (packet.Id != (ushort)expected)
                throw new InvalidDataException($"{message} (packet 0x{packet.Id:X4}).");
        }

        private void Fail(string message)
        {
            State = LoginState.Failed;
            Debug.LogError(message, this);
        }

        private void OnDestroy()
        {
            _shutdown?.Cancel();
            _shutdown?.Dispose();
            _mapConnection?.Dispose();
        }
    }
}
