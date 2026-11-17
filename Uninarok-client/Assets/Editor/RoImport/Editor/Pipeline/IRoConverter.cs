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
        RoResult<UnityEngine.Object> Convert(RoImportContext ctx, RoPath path);
    }
}
