using UnityEngine;
using Uninarok.RoImport.Core.Results;

namespace Uninarok.RoImport.Core.Rsm
{
    /// <summary>
    /// Builds one node's mesh (PLAN-0003 4.6): vertices welded by value, normals by shade type (<see cref="RsmShading"/>),
    /// faces grouped into submeshes by (texture, two-sided), converted to Unity space.
    /// </summary>
    public static class RsmMeshBuilder
    {
        /// <param name="vertexMatrix">The node's vertex matrix <c>A</c> (4.4 step 4).</param>
        /// <param name="reverseExtraWinding">True when <c>A</c> mirrors (<see cref="RsmSpace.MirrorsWinding"/>).</param>
        /// <param name="report">Receives one <see cref="RoIssueCode.RsmBadIndex"/> warning per node with bad faces.</param>
        /// <param name="referrer">The model, named in warnings.</param>
        public static RsmMeshData Build(RoRsmNode node, RsmShadeType shade, bool hasShadeType, in Matrix4x4 vertexMatrix,
                                        bool reverseExtraWinding, RoReport report, string referrer = null)
        {
            return new RsmMeshData();   // TASKS-0003 T4.3
        }
    }
}
