using System;

namespace Uninarok.RoImport.Core.Paths
{
    /// <summary>Canonical Ragnarok path (plan 4.1). Contract only: implemented by T1.3.</summary>
    public readonly struct RoPath : IEquatable<RoPath>, IComparable<RoPath>
    {
        public string Canonical => throw new NotImplementedException("T1.3");
        public string Extension => throw new NotImplementedException("T1.3");
        public string FileNameWithoutExtension => throw new NotImplementedException("T1.3");
        public RoPath Folder => throw new NotImplementedException("T1.3");

        public static RoPath Parse(string raw) => throw new NotImplementedException("T1.3");
        public static bool TryParse(string raw, out RoPath path) => throw new NotImplementedException("T1.3");
        public RoPath WithExtension(string extension) => throw new NotImplementedException("T1.3");
        public RoPath Combine(string relative) => throw new NotImplementedException("T1.3");

        public bool Equals(RoPath other) => throw new NotImplementedException("T1.3");
        public int CompareTo(RoPath other) => throw new NotImplementedException("T1.3");
        public override bool Equals(object obj) => throw new NotImplementedException("T1.3");
        public override int GetHashCode() => throw new NotImplementedException("T1.3");
        public static bool operator ==(RoPath a, RoPath b) => throw new NotImplementedException("T1.3");
        public static bool operator !=(RoPath a, RoPath b) => throw new NotImplementedException("T1.3");
    }
}
