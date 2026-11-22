#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Unity.Collections;
using UnityEngine;
using Uninarok.Networking.Client;
using Uninarok.Networking.Data;
using Uninarok.Networking.Flow;

namespace Uninarok.Networking.Dev
{
    /// <summary>
    /// Development auto-login (SPEC-0001 FR-15; plan 7.1): on start it logs in, picks a server and creates or picks a character,
    /// so the flow can be exercised against a local Hercules without a UI. The whole type is
    /// compiled only in the editor and in development builds. Credentials are NOT serialized here: they come from
    /// <see cref="DevCredentialsSource"/> (EditorPrefs, or a dev-login.json outside the repository).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DevAutoLogin : MonoBehaviour
    {
        [Header("Flow")]
        [Tooltip("Index in the server list the login server returns.")]
        [SerializeField, Min(0)] private int serverIndex;

        [Tooltip("Slot of the character to enter the map with (used when the account already has characters).")]
        [SerializeField, Range(0, 255)] private int characterSlot;

        [Tooltip("Logins to try in total. 1 means no retry after a failure.")]
        [SerializeField, Min(1)] private int maxLoginAttempts = 1;

        [Tooltip("Finish the map load at once, without a world (PLAN-0010 section 7). Also on when the player runs the network probe.")]
        [SerializeField] private bool completeMapLoadWithoutWorld;

        [Header("New character (used only when the account has none)")]
        [SerializeField] private string characterName = "DevHero";
        [SerializeField, Range(0, 255)] private int createSlot;
        [SerializeField] private ushort hairColor = 1;
        [SerializeField] private ushort hairStyle = 1;
        [Tooltip("Starting job class ID (0 = Novice).")]
        [SerializeField] private uint jobClass;
        [SerializeField] private AccountSex sex = AccountSex.SEX_MALE;

        private DevAutoLoginDriver _driver;

        private void Start()
        {
            NetworkClient client = NetworkClient.Instance != null ? NetworkClient.Instance : FindFirstObjectByType<NetworkClient>();
            if (client == null || client.Flow == null)
            {
                Debug.LogWarning("[Net] DevAutoLogin: no NetworkClient in the scene.");
                return;
            }

            var name = new FixedString32Bytes();
            name.CopyFromTruncated(characterName ?? string.Empty);
            var settings = new DevAutoLoginSettings
            {
                ServerIndex = serverIndex,
                CharacterSlot = (byte)characterSlot,
                MaxLoginAttempts = maxLoginAttempts,
                CompleteMapLoadWithoutWorld = completeMapLoadWithoutWorld || NetworkProbe.IsRequested,
                Draft = new CharacterDraft(in name, (byte)createSlot, hairColor, hairStyle, jobClass, sex),
            };

            _driver = new DevAutoLoginDriver(client.Flow, settings, DevCredentialsSource.Load);
            _driver.Start();
        }

        private void OnDestroy()
        {
            _driver?.Dispose();
            _driver = null;
        }
    }
}
#endif
