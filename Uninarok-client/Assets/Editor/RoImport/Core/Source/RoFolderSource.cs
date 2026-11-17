using System;
using System.Collections.Generic;
using System.IO;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;

namespace Uninarok.RoImport.Core.Source
{
    /// <summary>
    /// A data folder on disk (plan 4.2, SPEC-0002 FR-1). On first use the folder is enumerated once and indexed by the
    /// canonical form of each file's path, so a lookup is independent of letter case, slash direction and Unicode form.
    /// <see cref="Refresh"/> rebuilds the index. Nothing here writes to the folder.
    /// </summary>
    public sealed class RoFolderSource : IRoSource
    {
        readonly string _root;
        readonly RoReport _report;
        readonly RoHashCache _hashes;

        Dictionary<RoPath, string> _index;      // canonical path -> full disk path
        List<RoPath> _sorted;                   // canonical paths, ordinal order

        /// <param name="dataRootOnDisk">The folder that Ragnarok paths are relative to (the <c>data</c> folder).</param>
        /// <param name="report">Receives <see cref="RoIssueCode.SourceDuplicate"/> warnings; optional.</param>
        /// <param name="hashes">Hash cache used by <see cref="RoSourceFile.ContentHash"/>; null hashes without caching.</param>
        public RoFolderSource(string dataRootOnDisk, RoReport report = null, RoHashCache hashes = null)
        {
            if (string.IsNullOrWhiteSpace(dataRootOnDisk)) throw new ArgumentException("A data root is required.", nameof(dataRootOnDisk));
            _root = Path.GetFullPath(dataRootOnDisk).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            _report = report;
            _hashes = hashes;
        }

        public string Root => _root;

        /// <summary>Rebuilds the index from disk (picks up added, removed and renamed files).</summary>
        public void Refresh()
        {
            _index = null;
            _sorted = null;
            EnsureIndex();
        }

        public bool TryGetFile(RoPath path, out RoSourceFile file)
        {
            EnsureIndex();
            file = default;
            if (path.IsEmpty || !_index.TryGetValue(path, out string diskPath)) return false;

            var info = new FileInfo(diskPath);
            if (!info.Exists) return false;                                    // removed since the index was built
            file = new RoSourceFile(path, diskPath, info.Length, info.LastWriteTimeUtc.Ticks, _hashes);
            return true;
        }

        public IEnumerable<RoPath> Enumerate(string folderPrefix)
        {
            EnsureIndex();
            string prefix;
            if (string.IsNullOrWhiteSpace(folderPrefix)) prefix = string.Empty;
            else if (RoPath.TryParse(folderPrefix, out var folder)) prefix = folder.Canonical + "/";
            else yield break;

            foreach (var path in _sorted)
                if (path.Canonical.StartsWith(prefix, StringComparison.Ordinal))
                    yield return path;
        }

        void EnsureIndex()
        {
            if (_index != null) return;
            _index = new Dictionary<RoPath, string>();
            _sorted = new List<RoPath>();
            if (!Directory.Exists(_root)) return;

            // Ordinal disk-path order makes the winner among duplicates deterministic (plan 4.2).
            var files = new List<string>(Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories));
            files.Sort(StringComparer.Ordinal);

            foreach (string disk in files)
            {
                string relative = disk.Substring(_root.Length + 1);
                if (!RoPath.TryParseRelative(relative, out var path)) continue;    // not expressible as a Ragnarok path

                if (_index.TryGetValue(path, out string winner))
                {
                    _report?.Warn(RoIssueCode.SourceDuplicate, path.Canonical, null,
                        $"'{disk}' has the same Ragnarok path as '{winner}', which is used");
                    continue;
                }
                _index.Add(path, disk);
                _sorted.Add(path);
            }
            _sorted.Sort();
        }
    }
}
