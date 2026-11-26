using System;
using System.Collections.Generic;
using Uninarok.Catalog;
using Uninarok.Sprites;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Uninarok.Effects
{
    /// <summary>
    /// Finds the <see cref="SpriteAnimation"/> of a Sprite entry by its SPEC-0008 effect name (SPEC-0012 FR-4; plan 4.5 "Parts"):
    /// <see cref="SpriteCatalog.TryGetEffect"/> gives the reference, Addressables loads it, and the animation is kept for the
    /// runtime's life so every later play of the same name is a dictionary read (no allocation). A catalog may be absent (a world
    /// without sprites, a preview before it is set): then nothing is found and the part cannot draw. <see cref="Resolver"/>
    /// (tests, the editor preview) takes precedence over the catalog. Main thread only.
    /// </summary>
    internal sealed class EffectSpriteAnimations
    {
        readonly Dictionary<string, SpriteAnimation> found = new Dictionary<string, SpriteAnimation>(StringComparer.Ordinal);
        readonly List<AsyncOperationHandle<SpriteAnimation>> handles = new List<AsyncOperationHandle<SpriteAnimation>>();
        SpriteCatalog catalog;

        public SpriteCatalog Catalog
        {
            get => catalog;
            set
            {
                if (ReferenceEquals(catalog, value)) return;
                Clear();
                catalog = value;
            }
        }

        /// <summary>Optional lookup that wins over the catalog; null keeps the catalog path.</summary>
        public Func<string, SpriteAnimation> Resolver;

        public bool TryGet(string name, out SpriteAnimation animation, out string problem)
        {
            problem = null;
            animation = null;
            if (string.IsNullOrEmpty(name)) { problem = "names no sprite"; return false; }
            if (found.TryGetValue(name, out animation) && animation != null) return true;

            if (Resolver != null)
            {
                animation = Resolver(name);
                if (animation != null) { found[name] = animation; return true; }
                problem = "sprite '" + name + "' is not found by the resolver";
                return false;
            }
            if (catalog == null) { problem = "has no sprite catalog (sprite '" + name + "')"; return false; }
            if (!catalog.TryGetEffect(name, out var reference) || !reference.HasAnimation)
            {
                problem = "sprite '" + name + "' has no animation in the sprite catalog";
                return false;
            }

            var handle = reference.LoadAnimation();
            handle.WaitForCompletion();       // once per name for the runtime's life; later plays are a dictionary read
            if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
            {
                if (handle.IsValid()) Addressables.Release(handle);
                problem = "sprite '" + name + "' could not be loaded";
                return false;
            }
            handles.Add(handle);
            animation = handle.Result;
            found[name] = animation;
            return true;
        }

        /// <summary>Forgets what was found and releases what this loaded.</summary>
        public void Clear()
        {
            found.Clear();
            for (int i = 0; i < handles.Count; i++)
                if (handles[i].IsValid()) Addressables.Release(handles[i]);
            handles.Clear();
        }
    }
}
