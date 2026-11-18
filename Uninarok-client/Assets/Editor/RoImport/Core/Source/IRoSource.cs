using System.Collections.Generic;
using Uninarok.RoImport.Core.Paths;

namespace Uninarok.RoImport.Core.Source
{
    /// <summary>Resolves Ragnarok paths to source files (plan 4.2, SPEC-0002 FR-1). Never writes to the source.</summary>
    public interface IRoSource
    {
        /// <summary>Finds a file by its Ragnarok path; false (never an exception) when there is none.</summary>
        bool TryGetFile(RoPath path, out RoSourceFile file);

        /// <summary>
        /// Every file whose canonical path starts with <paramref name="folderPrefix"/> (normalized like a path, e.g.
        /// <c>model/</c>; "" for all), in ordinal order of the canonical path.
        /// </summary>
        IEnumerable<RoPath> Enumerate(string folderPrefix);
    }
}
