using System;
using Uninarok.RoImport.Core.Paths;

namespace Uninarok.RoImport.Core.Source
{
    /// <summary>One source file found on disk (plan 4.2). Contract only: implemented by T2.2.</summary>
    public readonly struct RoSourceFile
    {
        public RoPath Path => throw new NotImplementedException("T2.2");
        public string DiskPath => throw new NotImplementedException("T2.2");
        public long Length => throw new NotImplementedException("T2.2");
        public byte[] ReadAllBytes() => throw new NotImplementedException("T2.2");
        public string ContentHash() => throw new NotImplementedException("T2.2");
    }
}
