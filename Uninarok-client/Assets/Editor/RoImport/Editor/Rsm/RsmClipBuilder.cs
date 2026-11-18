using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Uninarok.RoImport.Core.Rsm;

namespace Uninarok.RoImport.Editor.Rsm
{
    /// <summary>
    /// One looping <see cref="AnimationClip"/> per loop group (PLAN-0003 4.8, D10): linear curves on <c>localPosition</c>,
    /// <c>localRotation</c> and <c>localScale</c> of each animated node, written key for key. Unity samples a rotation
    /// curve per component and normalizes, which is the reference's linear quaternion blend, so no keys are added and no
    /// signs changed (the clip's quaternion continuity is never "ensured").
    /// </summary>
    public static class RsmClipBuilder
    {
        public static AnimationClip Build(string name, RsmLoopGroup group, RsmTree tree)
        {
            var clip = new AnimationClip { name = name, frameRate = 60f };
            foreach (var node in group.Nodes)
            {
                string path = PathOf(node.Node, tree);
                if (node.Position.Length > 0)
                {
                    Set(clip, path, "m_LocalPosition.x", node.Position, k => k.x);
                    Set(clip, path, "m_LocalPosition.y", node.Position, k => k.y);
                    Set(clip, path, "m_LocalPosition.z", node.Position, k => k.z);
                }
                if (node.Rotation.Length > 0)
                {
                    Set(clip, path, "m_LocalRotation.x", node.Rotation, k => k.x);
                    Set(clip, path, "m_LocalRotation.y", node.Rotation, k => k.y);
                    Set(clip, path, "m_LocalRotation.z", node.Rotation, k => k.z);
                    Set(clip, path, "m_LocalRotation.w", node.Rotation, k => k.w);
                }
                if (node.Scale.Length > 0)
                {
                    Set(clip, path, "m_LocalScale.x", node.Scale, k => k.x);
                    Set(clip, path, "m_LocalScale.y", node.Scale, k => k.y);
                    Set(clip, path, "m_LocalScale.z", node.Scale, k => k.z);
                }
            }

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            settings.stopTime = group.LengthSeconds;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            return clip;
        }

        /// <summary>The node's path below the prefab root: the unique names from its root down, joined by <c>/</c>.</summary>
        public static string PathOf(int node, RsmTree tree)
        {
            var names = new List<string>();
            for (int n = node; n >= 0; n = tree.Parent[n]) names.Add(tree.UniqueNames[n]);
            names.Reverse();
            return string.Join("/", names);
        }

        static void Set<T>(AnimationClip clip, string path, string property, RsmKey<T>[] keys, System.Func<T, float> component)
        {
            var frames = new Keyframe[keys.Length];
            for (int i = 0; i < keys.Length; i++) frames[i] = new Keyframe(keys[i].Time, component(keys[i].Value));
            // Linear tangents: the slope to the neighbouring key on each side (no smoothing, 4.8).
            for (int i = 0; i < frames.Length; i++)
            {
                if (i > 0) frames[i].inTangent = Slope(frames[i - 1], frames[i]);
                if (i < frames.Length - 1) frames[i].outTangent = Slope(frames[i], frames[i + 1]);
                frames[i].weightedMode = WeightedMode.None;
            }
            clip.SetCurve(path, typeof(Transform), property, new AnimationCurve(frames));
        }

        static float Slope(Keyframe a, Keyframe b)
        {
            float dt = b.time - a.time;
            return dt > 0f ? (b.value - a.value) / dt : 0f;
        }
    }
}
