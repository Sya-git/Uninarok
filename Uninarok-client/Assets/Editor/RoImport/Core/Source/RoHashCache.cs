using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Uninarok.RoImport.Core.Source
{
    /// <summary>
    /// Remembers SHA-256 hashes of source files by (disk path, length, last-write ticks), so a full batch does not rehash
    /// gigabytes of unchanged files (plan 4.2). Stored in <c>Library/RoImport/hashcache.json</c>: local, never committed.
    /// The committed ledger holds the hashes that matter; this file is only a cache and can be deleted at any time.
    /// </summary>
    public sealed class RoHashCache
    {
        public const string DefaultRelativePath = "Library/RoImport/hashcache.json";

        [Serializable]
        sealed class Entry
        {
            public string path;
            public long length;
            public long ticks;
            public string hash;
        }

        [Serializable]
        sealed class FileModel
        {
            public List<Entry> entries = new List<Entry>();
        }

        readonly string _file;
        readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        bool _dirty;

        /// <summary>Opens a file for hashing. Replaced by tests to count reads.</summary>
        internal Func<string, Stream> OpenRead = path => File.OpenRead(path);

        /// <param name="cacheFile">Where the cache is kept; null for an in-memory cache that is never saved.</param>
        public RoHashCache(string cacheFile)
        {
            _file = cacheFile;
        }

        /// <summary>The cache file of the current project: <c>&lt;project&gt;/Library/RoImport/hashcache.json</c>.</summary>
        public static string DefaultFile =>
            Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", DefaultRelativePath);

        public int Count => _entries.Count;

        /// <summary>Loads the cache file if there is one. A missing or unreadable file gives an empty cache.</summary>
        public void Load()
        {
            _entries.Clear();
            _dirty = false;
            if (_file == null || !File.Exists(_file)) return;
            try
            {
                var model = JsonUtility.FromJson<FileModel>(File.ReadAllText(_file, Encoding.UTF8));
                if (model?.entries == null) return;
                foreach (var e in model.entries)
                    if (!string.IsNullOrEmpty(e.path) && !string.IsNullOrEmpty(e.hash)) _entries[e.path] = e;
            }
            catch (Exception e) when (e is IOException || e is ArgumentException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[RoImport] Hash cache '{_file}' could not be read and is rebuilt: {e.Message}");
                _entries.Clear();
            }
        }

        /// <summary>Writes the cache if it changed. Entries are sorted, so the file is stable.</summary>
        public void Save()
        {
            if (_file == null || !_dirty) return;
            var model = new FileModel();
            var keys = new List<string>(_entries.Keys);
            keys.Sort(StringComparer.Ordinal);
            foreach (var key in keys) model.entries.Add(_entries[key]);

            Directory.CreateDirectory(Path.GetDirectoryName(_file) ?? ".");
            File.WriteAllText(_file, JsonUtility.ToJson(model, true).Replace("\r\n", "\n"), new UTF8Encoding(false));
            _dirty = false;
        }

        /// <summary>The SHA-256 (lowercase hex) of a file; read from disk only when its length or write time changed.</summary>
        public string Get(string diskPath, long length, long lastWriteTicks)
        {
            if (_entries.TryGetValue(diskPath, out var e) && e.length == length && e.ticks == lastWriteTicks)
                return e.hash;

            string hash = Compute(diskPath);
            _entries[diskPath] = new Entry { path = diskPath, length = length, ticks = lastWriteTicks, hash = hash };
            _dirty = true;
            return hash;
        }

        string Compute(string diskPath)
        {
            using (var stream = OpenRead(diskPath))
                return Hex(SHA256Of(stream));
        }

        internal static string HashOf(string diskPath)
        {
            using (var stream = File.OpenRead(diskPath))
                return Hex(SHA256Of(stream));
        }

        static byte[] SHA256Of(Stream stream)
        {
            using (var sha = SHA256.Create())
                return sha.ComputeHash(stream);
        }

        static string Hex(byte[] hash)
        {
            var sb = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
