using UnityEngine;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Rsm;
using Uninarok.RoImport.Editor.Pipeline;

namespace Uninarok.RoImport.Editor.Rsm
{
    /// <summary>
    /// RSM model to prefab, meshes, materials, clips and controller (PLAN-0003 4.9; SPEC-0003 FR-1 to FR-10).
    /// </summary>
    public sealed class RoRsmConverter : IRoConverter
    {
        public const string Category = "model";
        public const int RulesVersion = 1;

        public RoJobKind Kind => RoJobKind.Model;

        /// <summary><c>.rsm</c> and <c>.rsm2</c> files.</summary>
        public bool Handles(RoPath path) => path.Extension == ".rsm" || path.Extension == ".rsm2";

        public RoResult<Object> Convert(RoImportContext ctx, RoPath path) =>
            RoResult<Object>.Failed(new RoIssue(RoIssueCode.Unexpected, path.Canonical, null, "not implemented yet (TASKS-0003 T6.4)"));

        /// <summary>
        /// Converts a model already read into <paramref name="rsm"/> and records it in the ledger with
        /// <paramref name="sourceHash"/>. All-or-nothing: on failure no output is written. Used by <see cref="Convert"/>
        /// and by tests with hand-made models.
        /// </summary>
        internal static RoResult<GameObject> Write(RoImportContext ctx, RoPath path, RoRsm rsm, string sourceHash) =>
            RoResult<GameObject>.Failed(new RoIssue(RoIssueCode.Unexpected, path.Canonical, null, "not implemented yet (TASKS-0003 T6.3)"));
    }
}
