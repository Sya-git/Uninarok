using System;
using System.Collections.Generic;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Source;
using Uninarok.RoImport.Editor.Writing;

namespace Uninarok.RoImport.Editor.Ledger
{
    /// <summary>Committed record of what each source produced (plan 4.8). Contract only: implemented by T4.6.</summary>
    public sealed class RoLedger
    {
        public const string DefaultFolder = "Assets/Editor/RoImport/Ledger";

        public static RoLedger Load(string category, string folder = DefaultFolder) => throw new NotImplementedException("T4.6");

        public string FilePath => throw new NotImplementedException("T4.6");
        public IEnumerable<RoLedgerEntry> Entries => throw new NotImplementedException("T4.6");
        public bool IsCurrent(RoPath source, string hash, int rules, IRoSource files, RoAssetWriter writer) => throw new NotImplementedException("T4.6");
        public RoLedgerEntry Find(RoPath source) => throw new NotImplementedException("T4.6");
        public bool Record(RoLedgerEntry entry, RoReport report = null) => throw new NotImplementedException("T4.6");
        public string OwnerOf(string outputPath) => throw new NotImplementedException("T4.6");
        public void Save() => throw new NotImplementedException("T4.6");
    }
}
