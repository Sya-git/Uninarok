using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Uninarok.RoImport.Core.Space;

namespace Uninarok.RoImport.Core.Rsm
{
    /// <summary>File key time units to seconds (PLAN-0003 4.8, D4, D10): 1.x keys are milliseconds (T0.4).</summary>
    public static class RsmTiming
    {
        public const float UnitsPerSecond = 1000f;
        public const float SecondsPerUnit = 1f / UnitsPerSecond;

        /// <summary>Divides rather than multiplies, so that whole milliseconds give the nearest float (750 → 0.75).</summary>
        public static float Seconds(int units) => units / UnitsPerSecond;
    }

    /// <summary>One key of a curve, in seconds and Unity space.</summary>
    public readonly struct RsmKey<T>
    {
        public readonly float Time;
        public readonly T Value;

        public RsmKey(float time, T value)
        {
            Time = time;
            Value = value;
        }
    }

    /// <summary>The converted keys of one animated node (PLAN-0003 4.8). Empty arrays where the node has no keys.</summary>
    public sealed class RsmNodeCurves
    {
        /// <summary>Index into <see cref="RoRsm.Nodes"/>.</summary>
        public int Node;
        public RsmKey<Vector3>[] Position = Array.Empty<RsmKey<Vector3>>();
        public RsmKey<Quaternion>[] Rotation = Array.Empty<RsmKey<Quaternion>>();
        public RsmKey<Vector3>[] Scale = Array.Empty<RsmKey<Vector3>>();
    }

    /// <summary>The animated nodes that loop on one length: one clip and one animator layer (PLAN-0003 4.8, D10).</summary>
    public sealed class RsmLoopGroup
    {
        /// <summary>The nodes' last key time, in file units.</summary>
        public int LengthUnits;
        public float LengthSeconds;
        public IReadOnlyList<RsmNodeCurves> Nodes = Array.Empty<RsmNodeCurves>();
    }

    /// <summary>A model's animation, converted once in Core (PLAN-0003 4.8).</summary>
    public sealed class RsmAnimationData
    {
        /// <summary>One group per distinct node loop length, ascending by length. Empty for a static model.</summary>
        public IReadOnlyList<RsmLoopGroup> Loops = Array.Empty<RsmLoopGroup>();

        public bool IsAnimated => Loops.Count > 0;

        /// <summary>
        /// Converts the keys of every node: positions through <c>RoSpace.Point</c> (roots with the recentering),
        /// rotations by the Y-flip conjugation <c>(x, y, z, w) → (-x, y, -z, w)</c> keeping the file sign, scales
        /// unchanged; times through <see cref="RsmTiming"/>. Nodes are grouped by their last key time.
        /// </summary>
        public static RsmAnimationData Build(RoRsm rsm, RsmTree tree, RsmRestPose rest, RsmRules rules)
        {
            var byLength = new SortedDictionary<int, List<RsmNodeCurves>>();
            for (int n = 0; n < rsm.Nodes.Count; n++)
            {
                var node = rsm.Nodes[n];
                var scaleKeys = rules.HasScaleKeys ? node.ScaleKeys : Array.Empty<RoRsmScaleKey>();
                int last = Math.Max(LastTime(node.PosKeys.Select(k => k.Time)),
                           Math.Max(LastTime(node.RotKeys.Select(k => k.Time)), LastTime(scaleKeys.Select(k => k.Time))));
                // No keys, or keys that all sit at time 0: the node holds its first key, which is its rest pose.
                if (last <= 0) continue;

                var offset = tree.Parent[n] < 0 ? rest.Recenter : Vector3.zero;
                var curves = new RsmNodeCurves
                {
                    Node = n,
                    Position = node.PosKeys.OrderBy(k => k.Time)
                        .Select(k => new RsmKey<Vector3>(RsmTiming.Seconds(k.Time), RoSpace.Point(k.Position + offset))).ToArray(),
                    Rotation = node.RotKeys.OrderBy(k => k.Time)
                        .Select(k => new RsmKey<Quaternion>(RsmTiming.Seconds(k.Time), Conjugate(k.Rotation))).ToArray(),
                    Scale = scaleKeys.OrderBy(k => k.Time)
                        .Select(k => new RsmKey<Vector3>(RsmTiming.Seconds(k.Time), RoSpace.Scale(k.Scale))).ToArray()
                };
                if (!byLength.TryGetValue(last, out var list)) byLength.Add(last, list = new List<RsmNodeCurves>());
                list.Add(curves);
            }

            return new RsmAnimationData
            {
                Loops = byLength.Select(g => new RsmLoopGroup
                {
                    LengthUnits = g.Key,
                    LengthSeconds = RsmTiming.Seconds(g.Key),
                    Nodes = g.Value
                }).ToList()
            };
        }

        static int LastTime(IEnumerable<int> times) => times.DefaultIfEmpty(0).Max();

        /// <summary>
        /// The Y-flip conjugation <c>F·R·F</c> of a normalized key, <c>(x, y, z, w) → (-x, y, -z, w)</c>. The sign is
        /// kept as stored: the reference blends the stored quaternions (D10).
        /// </summary>
        static Quaternion Conjugate(Quaternion q)
        {
            float length = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            if (length <= 0f) return Quaternion.identity;
            return new Quaternion(-q.x / length, q.y / length, -q.z / length, q.w / length);
        }
    }
}
