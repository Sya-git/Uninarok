using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Uninarok.Map;
using Uninarok.RoImport.Core.Gnd;
using Uninarok.RoImport.Core.Rsw;
using Uninarok.RoImport.Core.Space;

namespace Uninarok.RoImport.Editor.Map
{
    /// <summary>
    /// Builds a map's hierarchy into the active scene (PLAN-0006 4.6; SPEC-0006 FR-1, FR-2, FR-5, FR-6, FR-8, FR-9, FR-10):
    /// the root <c>Map_&lt;name&gt;</c> with <see cref="MapRoot"/>, the ground prefab instance, the four groups with their
    /// counts, and the sun; the ambient colour goes to the active scene's render settings. Models are prefab instances
    /// placed by <see cref="RoRswPlacement"/>; a missing model and the lights, sounds and effects are named markers.
    /// Everything is in file order. It only builds; the converter saves.
    /// </summary>
    public static class RoMapSceneBuilder
    {
        /// <summary>
        /// Static flags of the ground and of still instances: lightmaps, batching, reflection probes. No occlusion flags:
        /// they are deprecated in Unity 6.7 in favour of GPU occlusion culling, which needs none (PLAN-0006 D18).
        /// </summary>
        public const StaticEditorFlags StillFlags = StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic
                                                    | StaticEditorFlags.ReflectionProbeStatic;

        /// <summary>Static flags of animated instances: no batching, since they move (PLAN-0006 4.6).</summary>
        public const StaticEditorFlags MovingFlags = StaticEditorFlags.ContributeGI | StaticEditorFlags.ReflectionProbeStatic;

        /// <summary>What the builder needs: the RSW, its map space and rules, and the converted assets.</summary>
        public sealed class Input
        {
            public string MapName;
            public RoRsw Rsw;
            public RoMapSpace Space;
            public RoRswRules Rules;
            public MapData MapData;
            public GameObject GroundPrefab;
            /// <summary>The prefab of each model file the RSW names; a file absent here is a missing model.</summary>
            public IReadOnlyDictionary<string, GameObject> Models;
        }

        /// <summary>What the builder noticed, for the converter to report once per map.</summary>
        public sealed class Notes
        {
            public int ChildNodeNames;
            public readonly SortedSet<int> UnknownAnimTypes = new SortedSet<int>();
        }

        public static GameObject Build(Input input, Notes notes)
        {
            var rsw = input.Rsw;
            var root = new GameObject(RoMapNames.Root(input.MapName));
            root.AddComponent<MapRoot>().Set(input.MapName, input.MapData);

            var ground = (GameObject)PrefabUtility.InstantiatePrefab(input.GroundPrefab);
            ground.name = RoMapNames.Ground;
            ground.transform.SetParent(root.transform, false);
            SetFlags(ground, StillFlags);

            var models = Group(root, RoMapGroup.Models, rsw.Models.Count);
            foreach (var m in rsw.Models) AddModel(models, m, input, notes);

            var lights = Group(root, RoMapGroup.Lights, rsw.Lights.Count);
            foreach (var l in rsw.Lights)
            {
                var marker = Marker<MapLightMarker>(lights, l.Name, l.Position, input.Space);
                marker.color = l.Color;
                marker.range = l.Range * RoUnits.Scale;
            }

            var sounds = Group(root, RoMapGroup.Sounds, rsw.Sounds.Count);
            foreach (var s in rsw.Sounds)
            {
                var marker = Marker<MapSoundMarker>(sounds, s.Name, s.Position, input.Space);
                marker.file = s.File;
                marker.volume = s.Volume;
                marker.range = s.Range * RoUnits.Scale;
                marker.interval = s.Interval;
                marker.width = s.Width;
                marker.height = s.Height;
            }

            var effects = Group(root, RoMapGroup.Effects, rsw.Effects.Count);
            foreach (var e in rsw.Effects)
            {
                var marker = Marker<MapEffectMarker>(effects, e.Name, e.Position, input.Space);
                marker.preset = e.Preset;
                marker.delay = e.Delay;
                marker.a = e.A;
                marker.b = e.B;
                marker.c = e.C;
                marker.d = e.D;
            }

            AddSun(root, rsw.Light);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = RoRswLighting.Ambient(rsw.Light);
            return root;
        }

        static Transform Group(GameObject root, RoMapGroup group, int count)
        {
            var go = new GameObject(RoMapNames.GroupLabel(group, count));
            go.transform.SetParent(root.transform, false);
            return go.transform;
        }

        static void AddModel(Transform parent, in RoRswModel m, Input input, Notes notes)
        {
            RoRswPlacement.ToUnity(RoRswPlacement.ModelMatrixRo(m, input.Space, input.Rules), out var position, out var rotation, out var scale);

            GameObject go;
            if (input.Models.TryGetValue(m.File, out var prefab) && prefab != null)
            {
                go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.transform.SetParent(parent, false);
                Animate(go, prefab, m, notes, out bool moving);
                SetFlags(go, moving ? MovingFlags : StillFlags);
                if (!input.Rules.NodeNameSelectsRoot && IsChildNode(prefab.transform, m.Node)) notes.ChildNodeNames++;
            }
            else
            {
                go = new GameObject();
                go.transform.SetParent(parent, false);
                go.AddComponent<MapMissingModelMarker>().modelFile = m.File;
            }

            go.name = m.Name;
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
            go.transform.localScale = scale;
        }

        /// <summary>Type 0: a still prop (the animator, if any, is off). Type 2: the animator runs at the RSW speed (D14, D17).</summary>
        static void Animate(GameObject go, GameObject prefab, in RoRswModel m, Notes notes, out bool moving)
        {
            moving = false;
            int type = m.AnimType;
            if (type != 0 && type != 2)
            {
                notes.UnknownAnimTypes.Add(type);
                type = 2;
            }

            if (prefab.GetComponent<Animator>() == null) return;
            var animator = go.GetComponent<Animator>();
            if (type == 0)
            {
                animator.enabled = false;
                return;
            }
            animator.enabled = true;
            animator.speed = m.AnimSpeed;
            go.AddComponent<MapModelAnimation>().speed = m.AnimSpeed;   // Animator.speed is not saved in a scene (D17)
            moving = true;
        }

        /// <summary>True when the RSW node name is a node of the model below its root nodes (PLAN-0006 4.3).</summary>
        static bool IsChildNode(Transform prefabRoot, string node)
        {
            if (string.IsNullOrEmpty(node)) return false;
            foreach (var t in prefabRoot.GetComponentsInChildren<Transform>(true))
                if (t != prefabRoot && t.parent != prefabRoot && t.name == node) return true;
            return false;
        }

        static T Marker<T>(Transform parent, string name, Vector3 positionRo, RoMapSpace space) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = RoSpace.Point(space.FromRswRo(positionRo));
            return go.AddComponent<T>();
        }

        static void AddSun(GameObject root, in RoRswLight l)
        {
            var go = new GameObject(RoMapNames.Sun);
            go.transform.SetParent(root.transform, false);
            go.transform.localRotation = RoRswLighting.SunRotation(l);
            var sun = go.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = RoRswLighting.Sun(l);
            sun.intensity = 1f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = RoRswLighting.ShadowStrength(l);
        }

        static void SetFlags(GameObject go, StaticEditorFlags flags)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
        }
    }
}
