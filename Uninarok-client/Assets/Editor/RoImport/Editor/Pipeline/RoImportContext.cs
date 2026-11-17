using System;
using Uninarok.RoImport.Core.Imaging;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Source;
using Uninarok.RoImport.Editor.Ledger;
using Uninarok.RoImport.Editor.Writing;

namespace Uninarok.RoImport.Editor.Pipeline
{
    /// <summary>Everything one conversion run shares (plan 4.10). Contract only: implemented by T5.2.</summary>
    public sealed class RoImportContext
    {
        public RoImportContext(IRoSource source, RoOutputRoots roots, string ledgerFolder = RoLedger.DefaultFolder, RoReport report = null)
        {
            throw new NotImplementedException("T5.2");
        }

        public IRoSource Source { get; }
        public RoAssetWriter Writer { get; }
        public RoReport Report { get; }
        public RoOutputRoots Roots { get; }
        public IRoPngCodec Png { get; set; }
        public bool Force { get; set; }

        public RoLedger Ledger(string category) => throw new NotImplementedException("T5.2");
    }
}
