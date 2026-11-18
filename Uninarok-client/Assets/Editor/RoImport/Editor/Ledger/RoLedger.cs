using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Source;
using Uninarok.RoImport.Editor.Writing;

namespace Uninarok.RoImport.Editor.Ledger
{
    /// <summary>
    /// Committed record of what each source produced, from which inputs and rules (plan 4.8, D5, D14; SPEC-0002 FR-5,
    /// FR-12, FR-16). One JSON file per category, deterministic: entries, outputs and dependencies sorted, fixed key order,
    /// LF newlines, no timestamps. It also bounds what the importer may touch: only outputs listed here are ever overwritten
    /// or removed, and the hand-made additive scenes can never be listed.
    /// </summary>
    public sealed class RoLedger
    {
        public const string DefaultFolder = "Assets/Editor/RoImport/Ledger";

        [Serializable]
        sealed class FileModel
        {
            public List<RoLedgerEntry> entries = new List<RoLedgerEntry>();
        }

        readonly Dictionary<string, RoLedgerEntry> _entries = new Dictionary<string, RoLedgerEntry>(StringComparer.Ordinal);
        readonly Dictionary<string, string> _owners = new Dictionary<string, string>(StringComparer.Ordinal);   // output -> source

        RoLedger(string category, string filePath)
        {
            Category = category;
            FilePath = filePath;
        }

        public string Category { get; }

        /// <summary>Full path of the ledger file.</summary>
        public string FilePath { get; }

        /// <summary>Entries in ordinal order of their source path.</summary>
        public IEnumerable<RoLedgerEntry> Entries
        {
            get
            {
                var keys = new List<string>(_entries.Keys);
                keys.Sort(StringComparer.Ordinal);
                foreach (var key in keys) yield return _entries[key];
            }
        }

        /// <summary>
        /// Loads <c>&lt;folder&gt;/&lt;category&gt;.json</c>; a missing file is an empty ledger. A project-relative folder is
        /// resolved against the project root. Tests pass a temporary folder.
        /// </summary>
        public static RoLedger Load(string category, string folder = DefaultFolder)
        {
            if (string.IsNullOrWhiteSpace(category) || category.IndexOfAny(new[] { '/', '\\' }) >= 0 || category == "." || category == "..")
                throw new ArgumentException($"'{category}' is not a ledger category name.", nameof(category));
            if (string.IsNullOrWhiteSpace(folder)) throw new ArgumentException("A ledger folder is required.", nameof(folder));

            string root = Path.IsPathRooted(folder) ? folder : Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", folder);
            var ledger = new RoLedger(category, Path.GetFullPath(Path.Combine(root, category + ".json")));
            if (!File.Exists(ledger.FilePath)) return ledger;

            var model = JsonUtility.FromJson<FileModel>(File.ReadAllText(ledger.FilePath, Encoding.UTF8));
            if (model?.entries != null)
                foreach (var entry in model.entries)
                    if (!string.IsNullOrEmpty(entry?.Source)) ledger.Put(Normalized(entry));
            return ledger;
        }

        public RoLedgerEntry Find(RoPath source) =>
            _entries.TryGetValue(source.Canonical, out var entry) ? entry : null;

        /// <summary>The source that owns an output path, or null for a path no entry lists (hand-made, or never produced).</summary>
        public string OwnerOf(string outputPath) =>
            outputPath != null && _owners.TryGetValue(outputPath.Replace('\\', '/'), out var source) ? source : null;

        /// <summary>
        /// True only if an entry exists for the source, its hash and rules version match, all its outputs exist, and every
        /// dependency's current hash equals the recorded one (an empty recorded hash means "was missing").
        /// </summary>
        public bool IsCurrent(RoPath source, string hash, int rules, IRoSource files, RoAssetWriter writer)
        {
            var entry = Find(source);
            if (entry == null || entry.SourceHash != hash || entry.Rules != rules) return false;

            foreach (string output in entry.Outputs)
                if (!writer.Exists(output)) return false;

            foreach (var dependency in entry.Dependencies)
            {
                if (!RoPath.TryParse(dependency.Source, out var path)) return false;
                string current = files.TryGetFile(path, out var file) ? file.ContentHash() : string.Empty;
                if (current != (dependency.Hash ?? string.Empty)) return false;
            }
            return true;
        }

        /// <summary>
        /// Records (or replaces) the entry of a source. Refused, with one <see cref="RoIssueCode.OutputCollision"/> warning,
        /// when one of its outputs is owned by another source: the second file is skipped (plan 4.5).
        /// Throws for a hand-made additive scene, which can never be owned (plan 4.13).
        /// </summary>
        public bool Record(RoLedgerEntry entry, RoReport report = null)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            if (string.IsNullOrEmpty(entry.Source)) throw new ArgumentException("An entry needs a source.", nameof(entry));
            var normalized = Normalized(entry);

            foreach (string output in normalized.Outputs)
            {
                if (output.EndsWith(RoOutputPaths.AdditiveSuffix + ".unity", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException($"'{output}' is a hand-made additive scene and cannot be owned by '{entry.Source}'.", nameof(entry));
                if (_owners.TryGetValue(output, out string owner) && owner != normalized.Source)
                {
                    report?.Warn(RoIssueCode.OutputCollision, normalized.Source, null,
                        $"output '{output}' is already produced by '{owner}'; this file is skipped");
                    return false;
                }
            }

            Remove(normalized.Source);
            Put(normalized);
            return true;
        }

        /// <summary>Removes the entry of a source, releasing its outputs (used when a preferred source takes an output over).</summary>
        public void Forget(RoPath source) => Remove(source.Canonical);

        /// <summary>Writes the file when its content changed. Deterministic: same entries, same bytes.</summary>
        public void Save()
        {
            var model = new FileModel();
            model.entries.AddRange(Entries);
            string text = JsonUtility.ToJson(model, true).Replace("\r\n", "\n");
            if (!text.EndsWith("\n", StringComparison.Ordinal)) text += "\n";

            if (File.Exists(FilePath) && File.ReadAllText(FilePath, Encoding.UTF8) == text) return;
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath) ?? ".");
            File.WriteAllText(FilePath, text, new UTF8Encoding(false));
        }

        void Put(RoLedgerEntry entry)
        {
            _entries[entry.Source] = entry;
            foreach (string output in entry.Outputs) _owners[output] = entry.Source;
        }

        void Remove(string source)
        {
            if (!_entries.TryGetValue(source, out var previous)) return;
            foreach (string output in previous.Outputs)
                if (_owners.TryGetValue(output, out string owner) && owner == source) _owners.Remove(output);
            _entries.Remove(source);
        }

        /// <summary>A copy with forward slashes, sorted distinct outputs and sorted dependencies (empty hash for missing).</summary>
        static RoLedgerEntry Normalized(RoLedgerEntry entry)
        {
            var outputs = new List<string>();
            foreach (string output in entry.Outputs ?? new string[0])
                if (!string.IsNullOrEmpty(output))
                {
                    string o = output.Replace('\\', '/');
                    if (!outputs.Contains(o)) outputs.Add(o);
                }
            outputs.Sort(StringComparer.Ordinal);

            var dependencies = new List<RoLedgerDependency>();
            foreach (var d in entry.Dependencies ?? new RoLedgerDependency[0])
                if (d != null && !string.IsNullOrEmpty(d.Source)) dependencies.Add(new RoLedgerDependency(d.Source, d.Hash));
            dependencies.Sort((a, b) => string.CompareOrdinal(a.Source, b.Source));

            return new RoLedgerEntry
            {
                Source = entry.Source,
                SourceHash = entry.SourceHash ?? string.Empty,
                Rules = entry.Rules,
                Outputs = outputs.ToArray(),
                Dependencies = dependencies.ToArray(),
            };
        }
    }
}
