using System;
using Unity.Profiling;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace Uninarok.Units
{
    /// <summary>
    /// The single update loop of every enabled <see cref="UnitView"/> (PLAN-0009 4.6; SPEC-0009 FR-5, FR-6, FR-14). It reads the
    /// camera once per tick, after the camera moved (§5), so every unit and every part of a unit switches sector on the same
    /// frame. No allocation per frame. Statics are reset on entering play mode.
    /// </summary>
    [AutoStaticsCleanup]
    public static partial class UnitSystem
    {
        internal static readonly ProfilerMarker TickMarker = new ProfilerMarker("UnitSystem.Tick");
        internal static readonly ProfilerMarker ComposeMarker = new ProfilerMarker("UnitComposer.Compose");

        static UnitView[] s_views = new UnitView[64];
        static int s_count;
        static Quaternion s_lastRotation;
        static bool s_hasRotation;
        static UnitSystemDriver s_driver;

        /// <summary>The output camera units face; when null, <see cref="UnityEngine.Camera.main"/>.</summary>
        public static Camera Camera { get; set; }

        /// <summary>Number of registered (enabled) units.</summary>
        public static int Count => s_count;

        /// <summary>The camera sector of the last tick (0 with no camera).</summary>
        public static int Sector { get; private set; }

        /// <summary>Time of the last tick in milliseconds (the sandbox readout).</summary>
        public static double LastTickMs { get; private set; }

        internal static void Register(UnitView view)
        {
            if (view.SystemIndex >= 0) return;
            if (s_count == s_views.Length) Array.Resize(ref s_views, s_views.Length * 2);
            view.SystemIndex = s_count;
            s_views[s_count++] = view;
            if (Application.isPlaying && s_driver == null) s_driver = UnitSystemDriver.Create();
        }

        internal static void Unregister(UnitView view)
        {
            int i = view.SystemIndex;
            if (i < 0 || i >= s_count || s_views[i] != view) return;
            var last = s_views[--s_count];
            s_views[i] = last;
            last.SystemIndex = i;
            s_views[s_count] = null;
            view.SystemIndex = -1;
        }

        /// <summary>Steps every registered unit by <paramref name="deltaSeconds"/>.</summary>
        internal static void Tick(float deltaSeconds)
        {
            TickMarker.Begin();
            long start = System.Diagnostics.Stopwatch.GetTimestamp();

            var camera = Camera != null ? Camera : UnityEngine.Camera.main;
            var rotation = camera != null ? camera.transform.rotation : Quaternion.identity;
            bool changed = !s_hasRotation || !rotation.Equals(s_lastRotation); // exact: != is approximate and would miss a slow turn
            s_lastRotation = rotation;
            s_hasRotation = true;
            float yaw = camera != null ? YawOf(rotation) : 0f;
            Sector = camera != null ? UnitFacing.CameraSector(yaw) : 0;

            float deltaMs = deltaSeconds * 1000f;
            // A unit may leave the registry during its own step (AnimationEnded): the last one takes its place and is stepped then.
            for (int i = 0; i < s_count;)
            {
                var view = s_views[i];
                view.Step(deltaMs, Sector, rotation, yaw, changed);
                if (i < s_count && s_views[i] == view) i++;
            }

            LastTickMs = (System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            TickMarker.End();
        }

        /// <summary>Steps one unit alone (the composer window, PLAN-0009 4.9): the same code path as the game.</summary>
        internal static void StepOne(UnitView view, float deltaSeconds, Quaternion cameraRotation, bool rotationChanged = true)
        {
            float yaw = YawOf(cameraRotation);
            view.Step(deltaSeconds * 1000f, UnitFacing.CameraSector(yaw), cameraRotation, yaw, rotationChanged);
        }

        /// <summary>The turn of a camera in degrees: 0 looks along +Z (north), 90 along +X (east).</summary>
        public static float YawOf(Quaternion rotation)
        {
            var f = rotation * Vector3.forward;
            if (f.x * f.x + f.z * f.z < 1e-6f) f = rotation * Vector3.up; // straight down: the top of the screen
            return Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
        }

        internal static void ResetForTests()
        {
            for (int i = 0; i < s_count; i++)
                if (s_views[i] != null) s_views[i].SystemIndex = -1;
            Array.Clear(s_views, 0, s_views.Length);
            s_count = 0;
            s_hasRotation = false;
            Sector = 0;
            Camera = null;
        }
    }
}
