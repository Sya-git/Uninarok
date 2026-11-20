using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using Uninarok.RoImport.Editor.Ledger;

namespace Uninarok.RoImport.Editor.Sprites
{
    /// <summary>One sheet: the SPR content it was made from and where it is (PLAN-0007 4.2).</summary>
    [Serializable]
    public sealed class RoSheetEntry
    {
        /// <summary>SHA-256 (hex, lowercase) of the SPR file.</summary>
        public string Hash;
        /// <summary>Project path of the sheet PNG.</summary>
        public string Sheet;
        /// <summary>The SPR that first produced it (canonical), for people reading the file.</summary>
        public string Source;
        /// <summary>Whether the sheet has its index texture (palette support, FR-7).</summary>
        public bool HasIndex;
        /// <summary>The sheet rules version it was written with; a different version writes the sheet again.</summary>
        public int Rules;
        /// <summary>Indexed frames of the SPR: true-colour slots start here (plan 4.2).</summary>
        public int IndexedCount;
    }

    /// <summary>
    /// SPR content → sheet (PLAN-0007 4.2, D2; SPEC-0007 FR-2, FR-9). Sheets are owned by this index, not by a ledger entry,
    /// because one sheet serves every SPR with the same bytes and the ledger allows one owner per output. One committed JSON
    /// file next to the ledgers, deterministic like them (sorted by hash, LF, no timestamps), written only when it changed.
    /// </summary>
    public sealed class RoSpriteSheetIndex
    {
        public const string FileName = "spritesheets.json";

        [Serializable]
        sealed class FileModel
        {
            public List<RoSheetEntry> sheets = new List<RoSheetEntry>();
        }

        readonly Dictionary<string, RoSheetEntry> _entries = new Dictionary<string, RoSheetEntry>(StringComparer.Ordinal);

        RoSpriteSheetIndex(string filePath) => FilePath = filePath;

        /// <summary>Full path of the index file.</summary>
        public string FilePath { get; }

        public int Count => _entries.Count;

        /// <summary>Loads <c>&lt;folder&gt;/spritesheets.json</c>; a missing file is an empty index.</summary>
        public static RoSpriteSheetIndex Load(string folder = RoLedger.DefaultFolder)
        {
            if (string.IsNullOrWhiteSpace(folder)) throw new ArgumentException("A folder is required.", nameof(folder));
            string root = Path.IsPathRooted(folder) ? folder : Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", folder);
            var index = new RoSpriteSheetIndex(Path.GetFullPath(Path.Combine(root, FileName)));
            if (!File.Exists(index.FilePath)) return index;

            var model = JsonUtility.FromJson<FileModel>(File.ReadAllText(index.FilePath, Encoding.UTF8));
            if (model?.sheets != null)
                foreach (var e in model.sheets)
                    if (!string.IsNullOrEmpty(e?.Hash) && !string.IsNullOrEmpty(e.Sheet)) index.Record(e);
            return index;
        }

        /// <summary>The sheet of an SPR content hash, or null.</summary>
        public RoSheetEntry Find(string hash) =>
            hash != null && _entries.TryGetValue(hash.ToLowerInvariant(), out var e) ? e : null;

        /// <summary>Adds or replaces the entry of its hash.</summary>
        public void Record(RoSheetEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.Hash) || string.IsNullOrEmpty(entry.Sheet))
                throw new ArgumentException("A sheet entry needs a hash and a sheet path.", nameof(entry));
            _entries[entry.Hash.ToLowerInvariant()] = new RoSheetEntry
            {
                Hash = entry.Hash.ToLowerInvariant(),
                Sheet = entry.Sheet.Replace('\\', '/'),
                Source = entry.Source ?? string.Empty,
                HasIndex = entry.HasIndex,
                Rules = entry.Rules,
                IndexedCount = entry.IndexedCount
            };
        }

        /// <summary>Entries in ordinal order of their hash.</summary>
        public IEnumerable<RoSheetEntry> Entries
        {
            get
            {
                var keys = new List<string>(_entries.Keys);
                keys.Sort(StringComparer.Ordinal);
                foreach (var key in keys) yield return _entries[key];
            }
        }

        /// <summary>Writes the file when its content changed; true when it wrote. Same entries, same bytes.</summary>
        public bool Save()
        {
            var model = new FileModel();
            model.sheets.AddRange(Entries);
            string text = JsonUtility.ToJson(model, true).Replace("\r\n", "\n");
            if (!text.EndsWith("\n", StringComparison.Ordinal)) text += "\n";

            if (File.Exists(FilePath) && File.ReadAllText(FilePath, Encoding.UTF8) == text) return false;
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath) ?? ".");
            File.WriteAllText(FilePath, text, new UTF8Encoding(false));
            return true;
        }
    }
}
