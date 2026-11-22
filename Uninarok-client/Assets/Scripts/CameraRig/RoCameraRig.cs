using Unity.Cinemachine;
using UnityEngine;

namespace Uninarok.CameraRig
{
    /// <summary>
    /// Builds the camera rig (PLAN-0009 4.7, D8): the Unity camera with its brain, one Cinemachine camera with an orbital
    /// follow and a rotation composer, and the target they track. <c>RoCameraRig.prefab</c> is this, saved; tests build it.
    /// </summary>
    public static class RoCameraRig
    {
        public static RoCamera Create(RoCameraSettings settings)
        {
            var root = new GameObject("RoCameraRig");
            root.SetActive(false);

            var output = Child(root, "Main Camera");
            output.tag = "MainCamera";
            output.AddComponent<Camera>();
            output.AddComponent<AudioListener>();
            var brain = output.AddComponent<CinemachineBrain>();
            brain.UpdateMethod = CinemachineBrain.UpdateMethods.LateUpdate;
            brain.BlendUpdateMethod = CinemachineBrain.BrainUpdateMethods.LateUpdate;
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);

            var target = Child(root, "Camera Target");

            var virtualCamera = Child(root, "RO Camera");
            var cinemachine = virtualCamera.AddComponent<CinemachineCamera>();
            cinemachine.Follow = target.transform;
            var orbit = virtualCamera.AddComponent<CinemachineOrbitalFollow>();
            var composer = virtualCamera.AddComponent<CinemachineRotationComposer>();
            composer.Damping = Vector2.zero;
            composer.TargetOffset = Vector3.zero;
            composer.Composition.ScreenPosition = Vector2.zero;
            composer.Composition.DeadZone.Enabled = false;
            composer.Composition.HardLimits.Enabled = false;
            composer.Lookahead.Enabled = false;
            composer.CenterOnActivate = true;

            var ro = root.AddComponent<RoCamera>();
            ro.Settings = settings;
            ro.Camera = cinemachine;
            ro.Orbit = orbit;
            ro.Brain = brain;
            ro.CameraTarget = target.transform;
            ro.ApplySettings();
            root.SetActive(true);
            return ro;
        }

        static GameObject Child(GameObject root, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            return go;
        }
    }
}
