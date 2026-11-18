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
        public static RsmNodePose RestLocal(RoRsmNode node, RsmRules rules)
        {
            bool fromKeys = rules.RestFromFirstKey;
            var position = fromKeys && node.PosKeys.Length > 0 ? node.PosKeys[0].Position : node.Position;
            var rotation = fromKeys && node.RotKeys.Length > 0 ? Normalized(node.RotKeys[0].Rotation) : AxisAngle(node);
            var scale = fromKeys && rules.HasScaleKeys && node.ScaleKeys.Length > 0 ? node.ScaleKeys[0].Scale : node.Scale;
            return new RsmNodePose(position, rotation, scale);
        }

        /// <summary><c>A = T(Offset) · OffsetMatrix</c>; the file's groups of three are columns (D9).</summary>
        public static Matrix4x4 VertexMatrix(RoRsmNode node, RsmRules rules)
        {
            var a = Matrix4x4.identity;
            var m = node.OffsetMatrix;
            if (m != null && m.Length == 9)
                for (int group = 0; group < 3; group++)
                    for (int row = 0; row < 3; row++)
                        a[row, group] = m[3 * group + row];
            if (rules.OffsetVectorInVertexMatrix)
            {
                a.m03 = node.Offset.x;
                a.m13 = node.Offset.y;
                a.m23 = node.Offset.z;
            }
            return a;
        }

        /// <summary>The rest pose of every node, recentered (4.4 steps 3 to 6).</summary>
        public static RsmRestPose ComputeRest(RoRsm rsm, RsmTree tree, RsmRules rules)
        {
            int count = rsm.Nodes.Count;
            var local = new RsmNodePose[count];
            var vertex = new Matrix4x4[count];
            for (int n = 0; n < count; n++)
            {
                local[n] = RestLocal(rsm.Nodes[n], rules);
                vertex[n] = VertexMatrix(rsm.Nodes[n], rules);
            }

            var model = Chain(tree, local);
            var recenter = Vector3.zero;
            if (RsmBounds.OfRestPose(rsm, model, vertex, out var min, out var max) && rules.Recenter)
            {
                recenter = new Vector3(-(min.x + max.x) / 2f, -max.y, -(min.z + max.z) / 2f);
                foreach (int root in tree.Roots) local[root].Position += recenter;
                model = Chain(tree, local);
                min += recenter;
                max += recenter;
            }

            return new RsmRestPose
            {
                Local = local,
                Model = model,
                Vertex = vertex,
                Recenter = recenter,
                BoundsMin = min,
                BoundsMax = max
            };
        }

        /// <summary><c>Model(n) = Model(parent) · Local(n)</c>, parents first (4.4 step 3).</summary>
        static Matrix4x4[] Chain(RsmTree tree, RsmNodePose[] local)
        {
            var model = new Matrix4x4[local.Length];
            foreach (int n in tree.Order)
            {
                int parent = tree.Parent[n];
                model[n] = parent < 0 ? local[n].Matrix : model[parent] * local[n].Matrix;
            }
            return model;
        }

        static Quaternion AxisAngle(RoRsmNode node)
        {
            var axis = node.RotationAxis;
            if (axis.sqrMagnitude <= 0f || node.RotationAngle == 0f) return Quaternion.identity;
            return Quaternion.AngleAxis(node.RotationAngle * Mathf.Rad2Deg, axis.normalized);
        }

        static Quaternion Normalized(Quaternion q)
        {
            float length = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            return length > 0f ? new Quaternion(q.x / length, q.y / length, q.z / length, q.w / length) : Quaternion.identity;
        }
    }
}
