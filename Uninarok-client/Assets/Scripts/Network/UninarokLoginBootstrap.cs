using System.Threading;
using Uninarok.Login;
using Uninarok.Managers;
using UnityEngine;
using AccountSex = Uninarok.Networking.Packets.In.LoginServer.AccountSex;

namespace Uninarok.Networking
{
    public sealed class UninarokLoginBootstrap : MonoBehaviour
    {
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
        [SerializeField] private AccountSex sex = AccountSex.SEX_MALE;

        [Header("Map server")]
        [SerializeField] private string mapServerHostOverride;
        [SerializeField] private float timeoutSeconds = 20f;

        private async void Start()
        {
            
        }

        private void Update()
        {
            if (!LoginManager.Instance)
            {
                return;
            }

            switch (LoginManager.Instance.CurrentState)
            {
                case LoginManager.LoginState.None:
                    LoginManager.Instance.StartLogin(accountName, accountPassword);
                    break;
                case LoginManager.LoginState.OnServerSelection:
                    LoginManager.Instance.SelectServerAtIndex(0);
                    break;
                case LoginManager.LoginState.OnEmptyCharacterSelection:
                    if (LoginManager.Instance.PinState == LoginManager.PincodeState.PinCorrect)
                    {
                        LoginManager.Instance.CreateCharacter(characterName, 0, 0, 0, 0, sex);
                    }
                    break;
                case LoginManager.LoginState.OnCharacterSelection:
                    LoginManager.Instance.SelectCharacterAtIndex(0);
                    break;
                case LoginManager.LoginState.Playing:
                    if (GameConnectionManager.GameConnectionState == GameConnectionManager.GameState.None)
                    {
                        GameConnectionManager.ConnectToCurrentMap();
                    }
                    break;
            }
        }
    }
}
