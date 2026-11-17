using System;
using System.Collections.Generic;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;

namespace Uninarok.RoImport.Core.Source
{
    /// <summary>A data folder on disk, indexed by canonical path (plan 4.2). Contract only: implemented by T2.2.</summary>
    public sealed class RoFolderSource : IRoSource
    {
        public RoFolderSource(string dataRootOnDisk, RoReport report = null) { }

        public void Refresh() => throw new NotImplementedException("T2.2");
        public bool TryGetFile(RoPath path, out RoSourceFile file) => throw new NotImplementedException("T2.2");
        public IEnumerable<RoPath> Enumerate(string folderPrefix) => throw new NotImplementedException("T2.2");
    }
}
