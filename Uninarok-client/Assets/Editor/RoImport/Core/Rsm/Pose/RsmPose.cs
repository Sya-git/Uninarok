using System;
using UnityEngine;

namespace Uninarok.RoImport.Core.Rsm
{
    /// <summary>A node's rest transform in Ragnarok space: <c>Local = T(Position) · R(Rotation) · S(Scale)</c>.</summary>
    public struct RsmNodePose
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;

        public RsmNodePose(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            Position = position;
            Rotation = rotation;
            Scale = scale;
        }

        public Matrix4x4 Matrix => Matrix4x4.TRS(Position, Rotation, Scale);
    }

    /// <summary>The rest pose of a whole model in Ragnarok space (PLAN-0003 4.4). Arrays are indexed by node.</summary>
    public sealed class RsmRestPose
    {
        /// <summary>Each node's local rest transform; roots include <see cref="Recenter"/>.</summary>
        public RsmNodePose[] Local = Array.Empty<RsmNodePose>();
        /// <summary>Each node's model-space matrix, <c>Model(parent) · Local</c>, including the recentering.</summary>
        public Matrix4x4[] Model = Array.Empty<Matrix4x4>();
        /// <summary>Each node's vertex matrix <c>A = [OffsetMatrix | Offset]</c>, applied to its own vertices only.</summary>
        public Matrix4x4[] Vertex = Array.Empty<Matrix4x4>();
        /// <summary>The translation added to every root: <c>(-center.x, -max.y, -center.z)</c> of the bounds.</summary>
        public Vector3 Recenter;
        /// <summary>The rest-pose bounds of all vertices after the recentering.</summary>
        public Vector3 BoundsMin, BoundsMax;
    }

    /// <summary>
    /// Rule set R1 (PLAN-0003 4.4): pure functions in Ragnarok space with column vectors (<c>p' = M · p</c>).
    /// </summary>
    public static class RsmPose
    {
        /// <summary>A node's rest transform without recentering (4.4 steps 1 and 2).</summary>
        public static RsmNodePose RestLocal(RoRsmNode node, RsmRules rules) => default;   // TASKS-0003 T3.2

        /// <summary><c>A = T(Offset) · OffsetMatrix</c>; the file's groups of three are columns (D9).</summary>
        public static Matrix4x4 VertexMatrix(RoRsmNode node, RsmRules rules) => Matrix4x4.identity;   // TASKS-0003 T3.2

        /// <summary>The rest pose of every node, recentered (4.4 steps 3 to 6).</summary>
        public static RsmRestPose ComputeRest(RoRsm rsm, RsmTree tree, RsmRules rules) => null;   // TASKS-0003 T3.2
    }
}
