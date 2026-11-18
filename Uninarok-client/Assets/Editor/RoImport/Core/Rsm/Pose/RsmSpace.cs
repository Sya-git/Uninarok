using UnityEngine;

namespace Uninarok.RoImport.Core.Rsm
{
    /// <summary>
    /// Ragnarok to Unity conversion of node transforms and vertices (PLAN-0003 4.5), built on <c>RoSpace</c>.
    /// </summary>
    public static class RsmSpace
    {
        public static Vector3 LocalPosition(RsmNodePose pose) => Vector3.zero;              // TASKS-0003 T3.2
        public static Quaternion LocalRotation(RsmNodePose pose) => Quaternion.identity;    // TASKS-0003 T3.2
        public static Vector3 LocalScale(RsmNodePose pose) => Vector3.one;                  // TASKS-0003 T3.2

        /// <summary>A node's mesh vertex in Unity space: <c>RoSpace.Point(A · v)</c>.</summary>
        public static Vector3 Vertex(Matrix4x4 vertexMatrix, Vector3 roVertex) => Vector3.zero;   // TASKS-0003 T3.2

        /// <summary>True when the vertex matrix mirrors (negative determinant): the winding is reversed once more.</summary>
        public static bool MirrorsWinding(Matrix4x4 vertexMatrix) => false;   // TASKS-0003 T3.2
    }
}
