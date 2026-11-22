using Unity.Cinemachine;
using UnityEngine;
using Uninarok.Units;

namespace Uninarok.CameraRig
{
    /// <summary>
    /// The RO camera (PLAN-0009 4.7, D8, D13, D22; SPEC-0009 FR-12): turns, tilts and zooms a Cinemachine orbit around a
    /// target, easing towards the wanted values. Before the brain moves the camera each frame it clamps the wanted values,
    /// damps the current ones with Cinemachine's <see cref="Damper"/> and writes the three axes and the target. The target is
    /// eased here too, not by the orbit's own damping, so the view never swings while it follows (D22). While it catches up, the
    /// followed unit is kept on whole pixels of the screen, so its sprite is sampled the same way in every frame (D30).
    /// </summary>
    [DefaultExecutionOrder(50)]
    [DisallowMultipleComponent]
    public sealed class RoCamera : MonoBehaviour
    {
        public RoCameraSettings Settings;
        public CinemachineCamera Camera;
        public CinemachineOrbitalFollow Orbit;
        public CinemachineBrain Brain;
        /// <summary>What the orbit tracks; moved by this component.</summary>
        public Transform CameraTarget;
        /// <summary>Optional: <see cref="Target"/> follows it each frame.</summary>
        public Transform Follow;

        // Wanted and current (eased) values; yaw is unbounded so a turn never takes the short way round.
        float _wantedYaw, _wantedPitch, _wantedDistance;
        float _yaw, _pitch, _distance;
        bool _started;
        Vector3 _eased; // the eased target before it is moved to whole pixels

        /// <summary>The ground point the camera orbits.</summary>
        public Vector3 Target { get; set; }

        /// <summary>The current (eased) turn in degrees, 0..360; 0 looks north (+Z), 90 east (+X).</summary>
        public float Yaw => Mathf.Repeat(_yaw, 360f);
        public float Pitch => _pitch;
        public float Distance => _distance;
        public float WantedYaw => _wantedYaw;
        public float WantedPitch => _wantedPitch;
        public float WantedDistance => _wantedDistance;

        /// <summary>The output camera the brain drives; null without a brain.</summary>
        public Camera OutputCamera => Brain != null ? Brain.OutputCamera : null;

        /// <summary>The facing sector of the camera the brain rendered with last.</summary>
        public int Sector
        {
            get
            {
                var output = OutputCamera;
                return UnitFacing.CameraSector(output != null ? UnitSystem.YawOf(output.transform.rotation) : Yaw);
            }
        }

        void OnEnable()
        {
            if (Settings == null) return;
            ApplySettings();
            if (!_started)
            {
                _started = true;
                ResetNow();
            }
            if (Application.isPlaying && OutputCamera != null) UnitSystem.Camera = OutputCamera;
        }

        void OnDisable()
        {
            if (Application.isPlaying && UnitSystem.Camera == OutputCamera) UnitSystem.Camera = null;
        }

        void OnValidate()
        {
            if (Settings != null && Camera != null && Orbit != null) ApplySettings();
        }

        void Start()
        {
            // The brain finds its output camera in Awake, which may come after OnEnable.
            if (Application.isPlaying && OutputCamera != null) UnitSystem.Camera = OutputCamera;
        }

        // Before CinemachineBrain (order 100), after everything that moved the followed unit this frame.
        void LateUpdate() => Step(Time.deltaTime);

        // ---- controls ----

        public void Turn(float degrees) => _wantedYaw += degrees;

        public void Tilt(float degrees) => _wantedPitch = ClampPitch(_wantedPitch + degrees);

        /// <summary>Positive moves away.</summary>
        public void Zoom(float units) => _wantedDistance = ClampDistance(_wantedDistance + units);

        /// <summary>Back to the default turn, the short way round.</summary>
        public void ResetTurn() => _wantedYaw = _yaw + Mathf.DeltaAngle(_yaw, Settings.DefaultYaw);

        public void ResetTilt() => _wantedPitch = ClampPitch(Settings.DefaultPitch);

        public void ResetZoom() => _wantedDistance = ClampDistance(Settings.DefaultDistance);

        /// <summary>Every value to its default and the target to its place, at once.</summary>
        public void ResetNow()
        {
            _yaw = _wantedYaw = Settings.DefaultYaw;
            _pitch = _wantedPitch = ClampPitch(Settings.DefaultPitch);
            _distance = _wantedDistance = ClampDistance(Settings.DefaultDistance);
            SnapToTarget();
        }

        /// <summary>
        /// Jumps to the target now, keeping turn, tilt and zoom (map change, teleport; D13): the target object moves with no
        /// easing and Cinemachine is told it was warped, so nothing slides across the map.
        /// </summary>
        public void SnapToTarget()
        {
            if (Follow != null) Target = Follow.position;
            if (CameraTarget != null)
            {
                var delta = Target - CameraTarget.position;
                CameraTarget.position = _eased = Target;
                if (Camera != null) Camera.OnTargetObjectWarped(CameraTarget, delta);
            }
            WriteAxes();
        }

        // ---- the step ----

        internal void Step(float deltaSeconds)
        {
            if (Settings == null || Orbit == null) return;
            _wantedPitch = ClampPitch(_wantedPitch);
            _wantedDistance = ClampDistance(_wantedDistance);

            _yaw += Damper.Damp(_wantedYaw - _yaw, Settings.TurnDampTime, deltaSeconds);
            _pitch += Damper.Damp(_wantedPitch - _pitch, Settings.TiltDampTime, deltaSeconds);
            _distance += Damper.Damp(_wantedDistance - _distance, Settings.ZoomDampTime, deltaSeconds);

            // Keep the unbounded turn small, so a long session loses no precision.
            if (_yaw > 3600f || _yaw < -3600f)
            {
                float turns = Mathf.Floor(_yaw / 360f) * 360f;
                _yaw -= turns;
                _wantedYaw -= turns;
            }

            if (Follow != null) Target = Follow.position;
            if (CameraTarget != null)
            {
                _eased += Damper.Damp(Target - _eased, Settings.FollowDampTime, deltaSeconds);
                var output = OutputCamera;
                CameraTarget.position = Settings.AlignToPixels && output != null ? AlignedTo(output.pixelHeight) : _eased;
            }
            WriteAxes();
        }

        // The eased target moved by less than a pixel across the view, so that what it follows is a whole number of pixels from it.
        internal Vector3 AlignedTo(int pixelHeight)
        {
            if (pixelHeight <= 0) return _eased;
            var rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 right = rotation * Vector3.right, up = rotation * Vector3.up;
            var delta = Target - _eased;
            float depth = _distance + Vector3.Dot(delta, rotation * Vector3.forward);
            if (depth <= 0f) return _eased;
            float unitsPerPixel = 2f * depth * Mathf.Tan(Settings.FieldOfView * 0.5f * Mathf.Deg2Rad) / pixelHeight;
            float x = Vector3.Dot(delta, right) / unitsPerPixel, y = Vector3.Dot(delta, up) / unitsPerPixel;
            return _eased + ((x - Mathf.Round(x)) * right + (y - Mathf.Round(y)) * up) * unitsPerPixel;
        }

        void WriteAxes()
        {
            if (Orbit == null) return;
            Orbit.HorizontalAxis.Value = Mathf.DeltaAngle(0f, _yaw); // −180..180
            Orbit.VerticalAxis.Value = _pitch;
            Orbit.Radius = _distance;
        }

        float ClampPitch(float p) => Mathf.Clamp(p, Settings.MinPitch, Settings.MaxPitch);
        float ClampDistance(float d) => Mathf.Clamp(d, Settings.MinDistance, Settings.MaxDistance);

        /// <summary>Lens, axis ranges and damping from the settings.</summary>
        internal void ApplySettings()
        {
            if (Settings == null) return;
            if (Camera != null)
            {
                var lens = Camera.Lens;
                lens.FieldOfView = Settings.FieldOfView;
                lens.NearClipPlane = Settings.NearClip;
                lens.FarClipPlane = Settings.FarClip;
                Camera.Lens = lens;
            }
            if (Orbit == null) return;
            Orbit.OrbitStyle = CinemachineOrbitalFollow.OrbitStyles.Sphere;
            Orbit.TargetOffset = Vector3.zero;
            Orbit.TrackerSettings.BindingMode = Unity.Cinemachine.TargetTracking.BindingMode.WorldSpace;
            Orbit.TrackerSettings.PositionDamping = Vector3.zero; // the target is eased here (D22)
            Orbit.TrackerSettings.RotationDamping = Vector3.zero;
            Orbit.TrackerSettings.QuaternionDamping = 0f;
            Orbit.HorizontalAxis.Range = new Vector2(-180f, 180f);
            Orbit.HorizontalAxis.Wrap = true;
            Orbit.HorizontalAxis.Recentering.Enabled = false;
            Orbit.VerticalAxis.Range = new Vector2(Settings.MinPitch, Settings.MaxPitch);
            Orbit.VerticalAxis.Wrap = false;
            Orbit.VerticalAxis.Recentering.Enabled = false;
            Orbit.RadialAxis.Range = new Vector2(1f, 1f); // the distance is the radius itself
            Orbit.RadialAxis.Value = 1f;
            Orbit.RadialAxis.Recentering.Enabled = false;
        }
    }
}
