using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using Uninarok.Map;

namespace Uninarok.World.View
{
    /// <summary>
    /// Loads and unloads map scenes (SPEC-0010 FR-1, FR-15; PLAN-0010 4.7, D5): the converted scene and the additive scene beside
    /// it, additively beside Bootstrap, through Addressables. One map at a time: the previous one is unloaded first, and a newer
    /// request supersedes one still loading. Main thread.
    /// </summary>
    public sealed class MapSceneLoader : IDisposable
    {
        public enum Result : byte
        {
            Loaded,
            /// <summary>The map has no converted scene.</summary>
            MissingScene,
            Failed,
        }

        readonly List<AsyncOperationHandle<SceneInstance>> _scenes = new List<AsyncOperationHandle<SceneInstance>>();
        string _map;       // loaded, or being loaded
        MapRoot _root;     // set once loaded
        int _request;
        bool _disposed;

        /// <summary>The map whose scene is loaded; null while none is.</summary>
        public string LoadedMap => _root != null ? _map : null;

        /// <summary>
        /// Loads a map (its name, without suffix) and calls <paramref name="done"/> once, unless a newer request or
        /// <see cref="Unload"/> came first. A map that is loaded already stays as it is.
        /// </summary>
        public void Load(string mapName, Action<Result, MapRoot> done)
        {
            if (string.IsNullOrEmpty(mapName)) throw new ArgumentException("a map name is needed", nameof(mapName));
            if (done == null) throw new ArgumentNullException(nameof(done));
            if (_disposed) throw new ObjectDisposedException(nameof(MapSceneLoader));
            if (_root != null && _map == mapName)
            {
                _request++;
                done(Result.Loaded, _root);
                return;
            }

            Unload();
            int request = _request;
            _map = mapName;
            Exists(MapSceneKeys.Scene(mapName), exists =>
            {
                if (request != _request) return;
                if (!exists)
                {
                    _map = null;
                    done(Result.MissingScene, null);
                    return;
                }
                Scene(MapSceneKeys.Scene(mapName), request, scene =>
                {
                    if (!scene.HasValue || !TryFindRoot(scene.Value, out var root))
                    {
                        if (scene.HasValue) Debug.LogError($"[World] the scene of map '{mapName}' has no map root");
                        Unload();
                        done(Result.Failed, null);
                        return;
                    }
                    // Its lighting settings are the map's.
                    SceneManager.SetActiveScene(scene.Value);
                    Additive(mapName, request, () =>
                    {
                        _root = root;
                        done(Result.Loaded, root);
                    });
                });
            });
        }

        // Most maps have none; one that fails to load is not worth losing the map.
        void Additive(string mapName, int request, Action next) =>
            Exists(MapSceneKeys.Additive(mapName), exists =>
            {
                if (request != _request) return;
                if (!exists) next();
                else Scene(MapSceneKeys.Additive(mapName), request, _ => next());
            });

        /// <summary>Unloads the map and forgets a load in flight.</summary>
        public void Unload()
        {
            _request++;
            _map = null;
            _root = null;
            foreach (var handle in _scenes) UnloadScene(handle);
            _scenes.Clear();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Unload();
        }

        static bool TryFindRoot(Scene scene, out MapRoot root)
        {
            foreach (var go in scene.GetRootGameObjects())
                if (go.TryGetComponent(out root) && root.Data != null) return true;
            root = null;
            return false;
        }

        static void Exists(string key, Action<bool> done)
        {
            var locations = Addressables.LoadResourceLocationsAsync(key, typeof(SceneInstance));
            locations.Completed += h =>
            {
                bool exists = h.Status == AsyncOperationStatus.Succeeded && h.Result != null && h.Result.Count > 0;
                Addressables.Release(h);
                done(exists);
            };
        }

        // Loads one scene; the callback gets null when it failed. A superseded one is unloaded and calls nothing.
        void Scene(string key, int request, Action<Scene?> done)
        {
            var handle = Addressables.LoadSceneAsync(key, LoadSceneMode.Additive);
            handle.Completed += h =>
            {
                bool ok = h.Status == AsyncOperationStatus.Succeeded;
                if (request != _request)
                {
                    UnloadScene(h);
                    return;
                }
                if (ok) _scenes.Add(h);
                else if (h.IsValid()) Addressables.Release(h);
                done(ok ? h.Result.Scene : (Scene?)null);
            };
        }

        static void UnloadScene(AsyncOperationHandle<SceneInstance> handle)
        {
            if (!handle.IsValid()) return;
            if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result.Scene.isLoaded) Addressables.UnloadSceneAsync(handle);
            else Addressables.Release(handle);
        }
    }
}
