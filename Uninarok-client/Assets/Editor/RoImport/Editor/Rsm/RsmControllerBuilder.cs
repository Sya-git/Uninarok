using System.Collections.Generic;
using UnityEditor.Animations;
using UnityEngine;

namespace Uninarok.RoImport.Editor.Rsm
{
    /// <summary>
    /// The animator controller of an animated model (PLAN-0003 4.8, D5, D10): one layer per loop group (the first is the
    /// base layer, the others override with weight 1 on disjoint nodes), each with one looping default state <c>Play</c>
    /// holding the group's clip; no transitions, no parameters.
    /// </summary>
    public static class RsmControllerBuilder
    {
        public const string StateName = "Play";

        /// <summary>True when <paramref name="controller"/> already has exactly these layers and clips.</summary>
        public static bool Matches(AnimatorController controller, IReadOnlyList<AnimationClip> clips)
        {
            var layers = controller.layers;
            if (layers.Length != clips.Count || controller.parameters.Length != 0) return false;
            for (int i = 0; i < layers.Length; i++)
            {
                var sm = layers[i].stateMachine;
                if (sm == null || sm.states.Length != 1 || sm.defaultState == null) return false;
                if (sm.defaultState.name != StateName || sm.defaultState.motion != clips[i]) return false;
                if (sm.anyStateTransitions.Length != 0 || sm.defaultState.transitions.Length != 0) return false;
                if (i > 0 && (layers[i].defaultWeight != 1f || layers[i].blendingMode != AnimatorLayerBlendingMode.Override)) return false;
            }
            return true;
        }

        /// <summary>Replaces the controller's layers with one layer per clip.</summary>
        public static void Build(AnimatorController controller, IReadOnlyList<AnimationClip> clips)
        {
            while (controller.layers.Length > 0) controller.RemoveLayer(0);
            foreach (var p in controller.parameters) controller.RemoveParameter(p);

            for (int i = 0; i < clips.Count; i++)
            {
                controller.AddLayer(i == 0 ? "Base Layer" : "Loop " + (i + 1));
                var layers = controller.layers;
                var sm = layers[i].stateMachine;
                var state = sm.AddState(StateName);
                state.motion = clips[i];
                sm.defaultState = state;
                layers[i].defaultWeight = 1f;
                layers[i].blendingMode = AnimatorLayerBlendingMode.Override;
                controller.layers = layers;
            }
        }
    }
}
