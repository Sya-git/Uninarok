using Unity.Scripting.LifecycleManagement;
using Uninarok.Map;
using UnityEngine;

namespace Uninarok.Effects
{
    /// <summary>
    /// The game's effect player (SPEC-0012 FR-6, FR-9; plan 4.5, 5): one <see cref="EffectRuntime"/> created on the first play
    /// and ticked by a hidden <see cref="EffectSystemDriver"/> from <c>LateUpdate</c>. Plays by path: an unknown path plays the
    /// placeholder. Main thread only. Statics are reset on entering play mode
    /// (<see cref="AutoStaticsCleanupAttribute"/>); the driver destroys the runtime with it.
    /// <para>Wiring (done by the bootstrap): <see cref="Catalog"/>, <see cref="Settings"/> (the <c>EffectSettings</c> asset; no
    /// Resources fallback, the repo has none), and optionally <see cref="SpriteCatalog"/> and <see cref="SpriteMaterial"/>
    /// for Sprite entries. Without a catalog or settings a play returns <c>default</c> with one warning.</para>
    /// </summary>
    [AutoStaticsCleanup]
    public static partial class EffectSystem
    {
        static EffectCatalog s_catalog;
        static EffectSettings s_settings;
        static Uninarok.Catalog.SpriteCatalog s_spriteCatalog;
        static Material s_spriteMaterial;
        static EffectRuntime s_runtime = null;
        static EffectSystemDriver s_driver;
        static bool s_warnedNotReady;

        /// <summary>
        /// The catalog plays resolve through. Replacing it (or setting null) stops every effect, destroys the runtime and then
        /// releases the old catalog (<see cref="EffectCatalog.Release"/>), so the system owns what it is given.
        /// </summary>
        public static EffectCatalog Catalog
        {
            get => s_catalog;
            set
            {
                if (ReferenceEquals(s_catalog, value)) return;
                var old = s_catalog;
                DisposeRuntime();                      // StopAll first: nothing may use assets the release drops
                s_catalog = value;
                old?.Release();
                s_warnedNotReady = false;
            }
        }

        /// <summary>The <c>EffectSettings</c> asset (shader, placeholder texture, lead values). Changing it rebuilds the runtime.</summary>
        public static EffectSettings Settings
        {
            get => s_settings;
            set
            {
                if (s_settings == value) return;
                DisposeRuntime();
                s_settings = value;
                s_warnedNotReady = false;
            }
        }

        /// <summary>The sprite catalog Sprite entries are found through (SPEC-0008); null: a sprite entry cannot draw.</summary>
        public static Uninarok.Catalog.SpriteCatalog SpriteCatalog
        {
            get => s_spriteCatalog;
            set
            {
                s_spriteCatalog = value;
                if (s_runtime != null) s_runtime.SpriteCatalog = value;
            }
        }

        /// <summary>Shared material of the sprite actors (<c>sprite_default.mat</c>); null keeps Unity's default sprite material.</summary>
        public static Material SpriteMaterial
        {
            get => s_spriteMaterial;
            set
            {
                s_spriteMaterial = value;
                if (s_runtime != null) s_runtime.SpriteMaterial = value;
            }
        }

        /// <summary>Active effects, waiting ones included; 0 before the first play.</summary>
        public static int ActiveCount => s_runtime != null ? s_runtime.ActiveCount : 0;

        /// <summary>Plays the effect at <paramref name="path"/> at a world point (an unknown path plays the placeholder).
        /// <c>default</c> when there is no catalog or settings, or the pool is full.</summary>
        public static EffectHandle Play(string path, Vector3 point, bool loop = false)
        {
            var rt = Ensure();
            return rt != null ? rt.PlayPath(path, point, loop) : default;
        }

        /// <summary>As <see cref="Play"/>, following the unit's position (not its rotation).</summary>
        public static EffectHandle PlayOn(string path, Transform unit, bool loop = false)
        {
            var rt = Ensure();
            return rt != null ? rt.PlayOnPath(path, unit, loop) : default;
        }

        /// <summary>As <see cref="Play"/>, at the centre of a map cell.</summary>
        public static EffectHandle PlayAtCell(string path, MapData map, int x, int y, bool loop = false)
        {
            var rt = Ensure();
            return rt != null ? rt.PlayAtCellPath(path, map, x, y, loop) : default;
        }

        public static void SetSpeed(EffectHandle h, float speed) => s_runtime?.SetSpeed(h, speed);

        public static void Stop(EffectHandle h) => s_runtime?.Stop(h);

        public static void StopAll() => s_runtime?.StopAll();

        public static bool IsActive(EffectHandle h) => s_runtime != null && s_runtime.IsActive(h);

        /// <summary>Called by the driver's LateUpdate.</summary>
        internal static void Tick(float deltaSeconds) => s_runtime?.Tick(deltaSeconds);

        /// <summary>The driver went away (play mode ended, or someone destroyed it): the runtime goes with it.</summary>
        internal static void DriverDestroyed(EffectSystemDriver driver)
        {
            if (!ReferenceEquals(s_driver, driver)) return;
            DisposeRuntime();
            s_driver = null;
        }

        /// <summary>The hidden driver object (tests); null before the first play.</summary>
        internal static EffectSystemDriver Driver => s_driver;

        /// <summary>The runtime behind the system (tests); null before the first play.</summary>
        internal static EffectRuntime Runtime => s_runtime;

        /// <summary>Destroys the driver and forgets every static (tests). Not <see cref="Catalog"/>'s release.</summary>
        internal static void ResetForTests()
        {
            DisposeRuntime();
            if (s_driver != null) Object.Destroy(s_driver.gameObject);
            s_driver = null;
            s_catalog = null;
            s_settings = null;
            s_spriteCatalog = null;
            s_spriteMaterial = null;
            s_warnedNotReady = false;
        }

        static EffectRuntime Ensure()
        {
            if (s_runtime != null) return s_runtime;
            if (s_catalog == null || s_settings == null || !Application.isPlaying)
            {
                if (!s_warnedNotReady)
                {
                    s_warnedNotReady = true;
                    Debug.LogWarning(!Application.isPlaying
                        ? "[Effects] EffectSystem plays only in play mode; nothing was played."
                        : "[Effects] EffectSystem has no " + (s_catalog == null ? "Catalog" : "Settings") + " set; nothing was played.");
                }
                return null;
            }

            if (s_driver == null) s_driver = EffectSystemDriver.Create();
            s_runtime = new EffectRuntime(s_catalog, s_settings, s_driver.transform)
            {
                SpriteCatalog = s_spriteCatalog,
                SpriteMaterial = s_spriteMaterial,
            };
            return s_runtime;
        }

        static void DisposeRuntime()
        {
            if (s_runtime == null) return;
            s_runtime.StopAll();
            s_runtime.Dispose();
            s_runtime = null;
        }
    }
}
