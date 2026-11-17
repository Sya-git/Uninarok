using UnityEditor;
using UnityEngine;
using Uninarok.Networking.Dev;

namespace Uninarok.Networking.Editor
{
    /// <summary>
    /// Sets the development login credentials in <c>EditorPrefs</c> (SPEC-0001 FR-15, D16). They are stored per user on this
    /// machine, outside the repository and outside every scene.
    /// </summary>
    public sealed class DevLoginCredentialsWindow : EditorWindow
    {
        private string _user;
        private string _password;

        [MenuItem("Uninarok/Networking/Dev Login Credentials...")]
        public static void Open()
        {
            var window = GetWindow<DevLoginCredentialsWindow>(true, "Dev Login Credentials");
            window.minSize = new Vector2(340, 120);
            window.Show();
        }

        private void OnEnable()
        {
            _user = EditorPrefs.GetString(DevCredentialsSource.UserPrefKey, "");
            _password = EditorPrefs.GetString(DevCredentialsSource.PasswordPrefKey, "");
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Used by DevAutoLogin. Stored in EditorPrefs on this machine, never in the scene or the repository.", MessageType.Info);
            _user = EditorGUILayout.TextField("Account", _user);
            _password = EditorGUILayout.PasswordField("Password", _password);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Save"))
                {
                    EditorPrefs.SetString(DevCredentialsSource.UserPrefKey, _user ?? "");
                    EditorPrefs.SetString(DevCredentialsSource.PasswordPrefKey, _password ?? "");
                    Close();
                }
                if (GUILayout.Button("Clear"))
                {
                    EditorPrefs.DeleteKey(DevCredentialsSource.UserPrefKey);
                    EditorPrefs.DeleteKey(DevCredentialsSource.PasswordPrefKey);
                    _user = _password = "";
                }
            }
        }
    }
}
