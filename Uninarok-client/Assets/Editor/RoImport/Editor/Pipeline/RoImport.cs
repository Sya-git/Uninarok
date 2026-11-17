using System;
using System.Collections.Generic;
using UnityEngine;
using Uninarok.RoImport.Core.Materials;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Source;

namespace Uninarok.RoImport.Editor.Pipeline
{
    /// <summary>Options of one run (plan 4.10). Defaults are the project's source folder, output roots and ledgers.</summary>
    public sealed class RoImportOptions
    {
        public bool Force;
        public bool ShowProgress = true;
        public RoOutputRoots Roots;          // null: the default roots
        public IRoSource Source;             // null: a folder source over RoSourceLocation.Resolve()
        public string LedgerFolder;          // null: RoLedger.DefaultFolder
    }

    /// <summary>Conversions by Ragnarok path, from code or the editor (plan 4.10). Contract only: implemented by T6.4.</summary>
    public static class RoImport
    {
        public static RoResult<Texture2D> Texture(string roPath, RoImportOptions options = null) => throw new NotImplementedException("T6.4");
        public static RoResult<Material> Material(RoPath texture, RoSurface surface, bool twoSided, RoImportOptions options = null) => throw new NotImplementedException("T6.4");
        public static RoReport Run(IEnumerable<RoJob> jobs, RoImportOptions options = null) => throw new NotImplementedException("T6.4");
    }
}
