using System;

namespace Uninarok.RoImport.Editor.Ledger
{
    /// <summary>
    /// What one source produced, and from which inputs (plan 4.8). Serialized by <c>JsonUtility</c> in field order, so the
    /// order of the fields below is the key order of the committed ledger file.
    /// </summary>
    [Serializable]
    public sealed class RoLedgerEntry
    {
        public string Source;                         // canonical source path
        public string SourceHash;                     // SHA-256 hex of the source file
        public int Rules;                             // converter rules version
        public string[] Outputs = new string[0];      // project paths owned by this source, sorted ordinally
        public RoLedgerDependency[] Dependencies = new RoLedgerDependency[0];   // sorted by source
    }

    /// <summary>An input of an entry other than its own source; an empty hash means the input was missing.</summary>
    [Serializable]
    public sealed class RoLedgerDependency
    {
        public string Source;
        public string Hash;

        public RoLedgerDependency() { }

        public RoLedgerDependency(string source, string hash)
        {
            Source = source;
            Hash = hash ?? string.Empty;
        }
    }
}
