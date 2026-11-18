using System.Collections.Generic;
using UnityEngine;

namespace Uninarok.RoImport.Core.Rsm
{
    /// <summary>Axis-aligned bounds of the rest-pose vertices of all nodes, in Ragnarok space (PLAN-0003 4.4 step 5).</summary>
    public static class RsmBounds
    {
        /// <summary>
        /// The bounds of <c>Model[n] · Vertex[n] · v</c> over every vertex of every node; <c>false</c> when the model
        /// has no vertex.
        /// </summary>
        public static bool OfRestPose(RoRsm rsm, IReadOnlyList<Matrix4x4> model, IReadOnlyList<Matrix4x4> vertex,
                                      out Vector3 min, out Vector3 max)
        {
            min = max = Vector3.zero;
            return false;   // TASKS-0003 T3.2
        }
    }
}
