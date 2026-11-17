using System;

namespace Uninarok.RoImport.Core.Paths
{
    /// <summary>A string that is not a valid Ragnarok path (plan 4.1, rule 5).</summary>
    public sealed class RoPathException : ArgumentException
    {
        public string Raw { get; }

        public RoPathException(string raw, string reason)
            : base($"Not a valid Ragnarok path '{raw}': {reason}")
        {
            Raw = raw;
        }
    }
}
