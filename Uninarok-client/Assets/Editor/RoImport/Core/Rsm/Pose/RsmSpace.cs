using UnityEngine;
using Uninarok.RoImport.Core.Space;

namespace Uninarok.RoImport.Core.Rsm
{
    /// <summary>
    /// Ragnarok to Unity conversion of node transforms and vertices (PLAN-0003 4.5), built on <see cref="RoSpace"/>.
    /// Because the Y flip is a conjugation, translation, rotation and scale convert separately and the prefab's transform
    /// hierarchy equals the converted matrix chain of 4.4.
    /// </summary>
    public static class RsmSpace
    {
        public static Vector3 LocalPosition(RsmNodePose pose) => RoSpace.Point(pose.Position);

        public static Quaternion LocalRotation(RsmNodePose pose) => RoSpace.Rotation(Matrix4x4.Rotate(pose.Rotation));

        /// <summary>Unchanged; a negative component (mirrored node) stays negative and Unity flips its culling.</summary>
        public static Vector3 LocalScale(RsmNodePose pose) => RoSpace.Scale(pose.Scale);

        /// <summary>A node's mesh vertex in Unity space: <c>RoSpace.Point(A · v)</c>.</summary>
        public static Vector3 Vertex(Matrix4x4 vertexMatrix, Vector3 roVertex) =>
            RoSpace.Point(vertexMatrix.MultiplyPoint3x4(roVertex));

        /// <summary>True when the vertex matrix mirrors (negative determinant): the winding is reversed once more.</summary>
        public static bool MirrorsWinding(Matrix4x4 vertexMatrix) => vertexMatrix.determinant < 0f;
    }
}
