using UnityEngine;

namespace Uninarok.CameraRig
{
    /// <summary>
    /// The camera's limits, defaults and speeds (PLAN-0009 4.7; SPEC-0009 FR-12). Limits are the client's for a map without a
    /// row in <c>viewpointtable.txt</c>; the default view and the wheel step are measured side by side with the original
    /// client (TASKS-0009 T6.4). Distances in Unity units (0.1 Ragnarok world units), angles in degrees below the horizon.
    /// </summary>
    [CreateAssetMenu(menuName = "Uninarok/Ro Camera Settings")]
    public sealed class RoCameraSettings : ScriptableObject
    {
        /// <summary>Vertical field of view.</summary>
        public float FieldOfView = 15f;
        public float NearClip = 1f, FarClip = 150f;

        [Header("Limits (viewpointtable header: range 230, scope 170, altitude -50 to -65)")]
        public float MinDistance = 23f;
        public float MaxDistance = 40f;
        public float MinPitch = 50f, MaxPitch = 65f;

        [Header("Default view (T6.4)")]
        public float DefaultDistance = 40f;
        public float DefaultPitch = 50f;
        public float DefaultYaw;

        [Header("Smoothing: Cinemachine damp times equal to roBrowser's easing (D8)")]
        public float TurnDampTime = 0.384f;
        public float TiltDampTime = 0.384f;
        public float ZoomDampTime = 0.384f;
        public float FollowDampTime = 0.768f;
        /// <summary>The followed unit stays on whole pixels while the camera catches up (D30).</summary>
        public bool AlignToPixels = true;

        [Header("Controls")]
        public float TurnDegreesPerScreenWidth = 720f;
        public float PitchDegreesPerScreenHeight = 300f;
        /// <summary>Units per wheel notch (about 25 world units, research lab; T6.4).</summary>
        public float WheelStep = 2.5f;
        /// <summary>Units for a ctrl + drag over the whole screen height (T6.4).</summary>
        public float CtrlDragZoomPerScreenHeight = 34f;
        public float DoubleClickSeconds = 0.5f;
        public float DoubleClickPixels = 10f;
    }
}
