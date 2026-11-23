using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Uninarok.Units.Editor
{
    /// <summary>Outcome of <see cref="UnitComposerPresetStore.Save"/>.</summary>
    public enum SaveResult
    {
        Saved,
        NeedsConfirm,
        Refused,
    }

    /// <summary>Composer presets: read-only starters plus per-user presets (PLAN-0009 4.12, FR-18).</summary>
    public sealed class UnitComposerPresetStore
    {
        const string UnreadableName = "UnitComposerPresets.unreadable.json";

        [Serializable]
        class FileModel
        {
            public int Version = 1;
            public List<UnitComposerPreset> Presets = new List<UnitComposerPreset>();
        }

        readonly string _userFile;
        readonly string _starterFile;
        readonly List<UnitComposerPreset> _starters = new List<UnitComposerPreset>();
        readonly List<UnitComposerPreset> _users = new List<UnitComposerPreset>();
        List<(UnitComposerPreset Preset, bool Starter)> _all = new List<(UnitComposerPreset, bool)>();
        bool _userFileUnreadable;

        public UnitComposerPresetStore(string userFile, string starterFile)
        {
            _userFile = userFile;
            _starterFile = starterFile;
            Reload();
        }

        public static UnitComposerPresetStore ForProject()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string user = Path.Combine(projectRoot, "UserSettings", "Uninarok", "UnitComposerPresets.json");
            string starters = Path.Combine(Application.dataPath, "Scripts", "Units", "Editor", "Presets", "UnitComposerStarterPresets.json");
            return new UnitComposerPresetStore(user, starters);
        }

        public IReadOnlyList<(UnitComposerPreset Preset, bool Starter)> All => _all;

        public string Problem { get; private set; }

        public bool IsStarter(string name) => Find(_starters, name) != null;

        public bool IsTaken(string name) => Find(_starters, name) != null || Find(_users, name) != null;

        public SaveResult Save(UnitComposerPreset preset, bool overwrite)
        {
            string name = preset?.Name?.Trim();
            if (string.IsNullOrEmpty(name) || IsStarter(name)) return SaveResult.Refused;
            var existing = Find(_users, name);
            if (existing != null && !overwrite) return SaveResult.NeedsConfirm;

            var copy = JsonUtility.FromJson<UnitComposerPreset>(JsonUtility.ToJson(preset));
            copy.Name = name;
            var next = new List<UnitComposerPreset>(_users);
            if (existing != null) next[next.IndexOf(existing)] = copy; else next.Add(copy);
            if (!Write(next)) return SaveResult.Refused;
            Commit(next);
            return SaveResult.Saved;
        }

        public bool Rename(string from, string to)
        {
            string newName = to?.Trim();
            if (string.IsNullOrEmpty(newName)) return false;
            var item = Find(_users, from);
            if (item == null) return false;
            // Changing only the case of its own name is allowed; any other taken name is not.
            bool sameItem = string.Equals(item.Name, newName, StringComparison.OrdinalIgnoreCase);
            if (!sameItem && IsTaken(newName)) return false;
            var next = new List<UnitComposerPreset>(_users);
            var copy = JsonUtility.FromJson<UnitComposerPreset>(JsonUtility.ToJson(item));
            copy.Name = newName;
            next[next.IndexOf(item)] = copy;
            if (!Write(next)) return false;
            Commit(next);
            return true;
        }

        public bool Delete(string name)
        {
            var item = Find(_users, name);
            if (item == null) return false;
            var next = new List<UnitComposerPreset>(_users);
            next.Remove(item);
            if (!Write(next)) return false;
            Commit(next);
            return true;
        }

        public void Reload()
        {
            _starters.Clear();
            _users.Clear();
            Problem = null;
            _userFileUnreadable = false;

            // A missing starter file lists no starters, silently (T11.3 creates it).
            if (File.Exists(_starterFile))
            {
                var model = TryRead(_starterFile, out string error);
                if (model != null) _starters.AddRange(model.Presets);
                else Debug.LogWarning($"[Unit Composer] starter presets file '{_starterFile}' cannot be read: {error}");
            }

            if (File.Exists(_userFile))
            {
                var model = TryRead(_userFile, out string error);
                if (model != null) _users.AddRange(model.Presets);
                else
                {
                    _userFileUnreadable = true;
                    Problem = $"The user presets file cannot be read ({error}). It is kept aside as {UnreadableName} on the next save.";
                    Debug.LogWarning($"[Unit Composer] {Problem} File: {_userFile}");
                }
            }
            Rebuild();
        }

        static FileModel TryRead(string path, out string error)
        {
            error = null;
            try
            {
                var model = JsonUtility.FromJson<FileModel>(File.ReadAllText(path));
                if (model == null) { error = "the file is empty"; return null; }
                model.Presets = (model.Presets ?? new List<UnitComposerPreset>()).Where(p => p != null && !string.IsNullOrWhiteSpace(p.Name)).ToList();
                foreach (var p in model.Presets) p.Name = p.Name.Trim();
                return model;
            }
            catch (Exception e)
            {
                error = e.Message;
                return null;
            }
        }

        static UnitComposerPreset Find(List<UnitComposerPreset> list, string name)
        {
            string n = name?.Trim();
            if (string.IsNullOrEmpty(n)) return null;
            return list.FirstOrDefault(p => string.Equals(p.Name, n, StringComparison.OrdinalIgnoreCase));
        }

        void Commit(List<UnitComposerPreset> users)
        {
            _users.Clear();
            _users.AddRange(users);
            _userFileUnreadable = false;
            Problem = null;
            Rebuild();
        }

        void Rebuild()
        {
            var all = new List<(UnitComposerPreset, bool)>();
            foreach (var s in _starters) all.Add((s, true));
            foreach (var u in _users.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)) all.Add((u, false));
            _all = all;
        }

        bool Write(List<UnitComposerPreset> users)
        {
            string tmp = null;
            try
            {
                string dir = Path.GetDirectoryName(_userFile);
                Directory.CreateDirectory(dir);
                if (_userFileUnreadable && File.Exists(_userFile))
                {
                    string aside = Path.Combine(dir, UnreadableName);
                    if (File.Exists(aside)) File.Delete(aside);
                    File.Move(_userFile, aside);
                }
                var model = new FileModel { Presets = users };
                tmp = _userFile + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(model, true));
                if (File.Exists(_userFile))
                {
                    // A backup name makes the replace a rename pair, which is more reliable on Windows than a null backup.
                    string bak = _userFile + ".bak";
                    File.Replace(tmp, _userFile, bak);
                    try { File.Delete(bak); } catch (Exception) { }
                }
                else File.Move(tmp, _userFile);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Unit Composer] could not save the presets: {e.Message}");
                try { if (tmp != null && File.Exists(tmp)) File.Delete(tmp); } catch (Exception) { }
                return false;
            }
        }
    }
}
