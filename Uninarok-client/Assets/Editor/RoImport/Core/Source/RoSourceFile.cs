using System.IO;
using Uninarok.RoImport.Core.Paths;

namespace Uninarok.RoImport.Core.Source
{
    /// <summary>One source file found on disk (plan 4.2). Reading never writes to the source.</summary>
    public readonly struct RoSourceFile
    {
        readonly RoHashCache _hashes;

        internal RoSourceFile(RoPath path, string diskPath, long length, long lastWriteTicks, RoHashCache hashes)
        {
            Path = path;
            DiskPath = diskPath;
            Length = length;
            LastWriteTicks = lastWriteTicks;
            _hashes = hashes;
        }

        public RoPath Path { get; }

        /// <summary>The full path on disk, with the file's real letter case and Unicode form.</summary>
        public string DiskPath { get; }

        public long Length { get; }

        /// <summary>Last write time (UTC ticks) when the file was looked up; with <see cref="Length"/> it keys the hash cache.</summary>
        public long LastWriteTicks { get; }

        public byte[] ReadAllBytes() => File.ReadAllBytes(DiskPath);

        /// <summary>SHA-256 of the content as lowercase hex, through the hash cache when the source has one.</summary>
        public string ContentHash() =>
            _hashes != null ? _hashes.Get(DiskPath, Length, LastWriteTicks) : RoHashCache.HashOf(DiskPath);
    }
}
