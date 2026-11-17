#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using UnityEngine;

namespace Uninarok.Networking.Dev
{
    /// <summary>Account name and password for the development auto-login. Never logged, never serialized in a scene.</summary>
    public readonly struct DevCredentials
    {
        public readonly string User;
        public readonly string Password;

        public DevCredentials(string user, string password)
        {
            User = user;
            Password = password;
        }

        public bool IsValid => !string.IsNullOrEmpty(User);
    }

    /// <summary>
    /// Where the development credentials come from (SPEC-0001 FR-15, D16): <c>EditorPrefs</c> in the editor, or a
    /// <c>dev-login.json</c> under <c>Application.persistentDataPath</c> in a development player. Both live outside the
    /// repository, so nothing is committed. Absent from release builds.
    /// </summary>
    public static class DevCredentialsSource
    {
        public const string UserPrefKey = "Uninarok.DevLogin.User";
        public const string PasswordPrefKey = "Uninarok.DevLogin.Password";
        public const string FileName = "dev-login.json";

        [Serializable]
        private sealed class FileFormat
        {
            public string user;
            public string password;
        }

        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>The credentials, or null when none are set.</summary>
        public static DevCredentials? Load()
        {
#if UNITY_EDITOR
            string user = UnityEditor.EditorPrefs.GetString(UserPrefKey, "");
            if (!string.IsNullOrEmpty(user))
                return new DevCredentials(user, UnityEditor.EditorPrefs.GetString(PasswordPrefKey, ""));
            return null;
#else
            return LoadFromFile(FilePath);
#endif
        }

        /// <summary>Reads a <c>dev-login.json</c> file: <c>{"user": "...", "password": "..."}</c>. Null when missing or malformed.</summary>
        public static DevCredentials? LoadFromFile(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                return Parse(File.ReadAllText(path));
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static DevCredentials? Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                var parsed = JsonUtility.FromJson<FileFormat>(json);
                if (parsed == null || string.IsNullOrEmpty(parsed.user)) return null;
                return new DevCredentials(parsed.user, parsed.password ?? "");
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
#endif
