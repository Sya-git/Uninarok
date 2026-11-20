using System;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace Uninarok.Sprites
{
    /// <summary>
    /// The single update loop of every enabled <see cref="SpriteActor"/> (PLAN-0007 4.6, D4). Actors register when enabled;
    /// in play mode one hidden driver calls <see cref="Tick"/> from <c>LateUpdate</c>. The loop advances each actor's clock,
    /// writes renderers only on frame change and applies the billboard yaw only when the camera turned. No allocation per
    /// frame. Statics are reset on entering play mode (<see cref="AutoStaticsCleanupAttribute"/>), so the registry is rebuilt
    /// from the actors that enable again.
    /// </summary>
    [AutoStaticsCleanup]
    public static partial class SpriteSystem
    {
        static SpriteActor[] s_actors = new SpriteActor[64];
        static int s_count;
        static float s_lastYaw = float.NaN;
        static SpriteSystemDriver s_driver;

        /// <summary>The camera actors turn to; when null, <see cref="UnityEngine.Camera.main"/>.</summary>
        public static Camera Camera { get; set; }

        /// <summary>Number of registered (enabled) actors.</summary>
        public static int Count => s_count;

        internal static void Register(SpriteActor actor)
        {
            if (actor.SystemIndex >= 0) return;
            if (s_count == s_actors.Length) Array.Resize(ref s_actors, s_actors.Length * 2);
            actor.SystemIndex = s_count;
            actor.BillboardDirty = true;
            s_actors[s_count++] = actor;
            if (Application.isPlaying && s_driver == null) s_driver = SpriteSystemDriver.Create();
        }

        internal static void Unregister(SpriteActor actor)
        {
            int i = actor.SystemIndex;
            if (i < 0 || i >= s_count || s_actors[i] != actor) return;
            var last = s_actors[--s_count];
            s_actors[i] = last;
            last.SystemIndex = i;
            s_actors[s_count] = null;
            actor.SystemIndex = -1;
        }

        /// <summary>Advances every registered actor by <paramref name="deltaSeconds"/>.</summary>
        internal static void Tick(float deltaSeconds)
        {
            float deltaMs = deltaSeconds * 1000f;

            var camera = Camera != null ? Camera : UnityEngine.Camera.main;
            bool turned = false;
            float yaw = 0f;
            if (camera != null)
            {
                yaw = camera.transform.eulerAngles.y;
                turned = !(yaw == s_lastYaw);
                s_lastYaw = yaw;
            }

            for (int i = 0; i < s_count; i++)
            {
                var actor = s_actors[i];
                actor.Advance(deltaMs);
                if (camera != null && actor.Billboard && (turned || actor.BillboardDirty))
                {
                    actor.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                    actor.BillboardDirty = false;
                }
            }
        }

        internal static void ResetForTests()
        {
            for (int i = 0; i < s_count; i++)
                if (s_actors[i] != null) s_actors[i].SystemIndex = -1;
            Array.Clear(s_actors, 0, s_actors.Length);
            s_count = 0;
            s_lastYaw = float.NaN;
            Camera = null;
        }
    }
}
