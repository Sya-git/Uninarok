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
            min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            bool any = false;
            for (int n = 0; n < rsm.Nodes.Count; n++)
            {
                var m = model[n] * vertex[n];
                foreach (var v in rsm.Nodes[n].Vertices)
                {
                    var p = m.MultiplyPoint3x4(v);
                    min = Vector3.Min(min, p);
                    max = Vector3.Max(max, p);
                    any = true;
                }
            }

            if (!any) min = max = Vector3.zero;
            return any;
        }
    }
}
