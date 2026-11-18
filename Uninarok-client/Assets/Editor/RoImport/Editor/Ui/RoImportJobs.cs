using System;
using System.Collections.Generic;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Source;
using Uninarok.RoImport.Editor.Pipeline;

namespace Uninarok.RoImport.Editor.Ui
{
    /// <summary>
    /// Turns the window's path list into jobs (plan 4.11): one Ragnarok path or folder prefix per line. A line ending with
    /// <c>/</c> (or <c>\</c>) is a folder and expands to every file under it; blank lines and <c>#</c> comments are ignored.
    /// </summary>
    public static class RoImportJobs
    {
        public sealed class Expansion
        {
            public readonly List<RoJob> Jobs = new List<RoJob>();
            public readonly List<string> Invalid = new List<string>();
        }

        public static Expansion Expand(string lines, RoJobKind kind, IRoSource source)
        {
            var result = new Expansion();
            foreach (string raw in (lines ?? string.Empty).Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;

                if (line.EndsWith("/", StringComparison.Ordinal) || line.EndsWith("\\", StringComparison.Ordinal))
                {
                    int before = result.Jobs.Count;
                    foreach (var path in source.Enumerate(line)) result.Jobs.Add(new RoJob(kind, path));
                    if (result.Jobs.Count == before) result.Invalid.Add(line + "  (no files)");
                }
                else if (RoPath.TryParse(line, out var path))
                {
                    result.Jobs.Add(new RoJob(kind, path));
                }
                else
                {
                    result.Invalid.Add(line + "  (not a Ragnarok path)");
                }
            }
            return result;
        }
    }
}
