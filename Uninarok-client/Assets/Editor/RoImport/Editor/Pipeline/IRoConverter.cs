using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;

namespace Uninarok.RoImport.Editor.Pipeline
{
    /// <summary>
    /// Converts one source of one kind (plan 4.10). Expected conditions are never exceptions: the converter returns
    /// <see cref="RoStatus.Failed"/> or a placeholder-backed result (plan 4.9).
    /// </summary>
    public interface IRoConverter
    {
        RoJobKind Kind { get; }

        /// <summary>
        /// Whether a file is one this converter reads (by extension). Folder lines in the window and menu expand only to
        /// these (plan 4.11, D21); an explicit request for another file still reaches <see cref="Convert"/> and warns.
        /// </summary>
        bool Handles(RoPath path);

        RoResult<UnityEngine.Object> Convert(RoImportContext ctx, RoPath path);
    }
}
