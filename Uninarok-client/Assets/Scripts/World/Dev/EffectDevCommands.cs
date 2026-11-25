using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Uninarok.Catalog;
using Uninarok.Effects;
using Uninarok.World.View;

namespace Uninarok.World.Dev
{
    /// <summary>What the dev command plays through (the game's <see cref="EffectSystem"/> in the game, a fake in tests).</summary>
    internal interface IEffectBackend
    {
        bool IsReady { get; }
        bool IsLoading { get; }
        /// <summary>Starts loading what plays need (once); <paramref name="done"/> gets null on success, else the reason.</summary>
        void BeginLoad(Action<string> done);
        bool HasCharacter { get; }
        bool HasMap { get; }
        bool InMap(int x, int y);
        /// <summary>True when the catalog knows the path; <paramref name="key"/> is the name to show.</summary>
        bool Resolve(string path, out string key);
        EffectHandle PlayOnSelf(string path, bool loop);
        EffectHandle PlayAtCell(string path, int x, int y, bool loop);
        void Stop(EffectHandle handle);
        void StopAll();
    }

    /// <summary>
    /// <c>/effect</c> (SPEC-0012 FR-8, PLAN-0012 4.8, D31): plays an effect on the own character or at a cell, and stops what it
    /// started. Loads the catalog on first use. Dev only (assembly-level guard).
    /// </summary>
    internal sealed class EffectDevCommands
    {
        readonly IEffectBackend _backend;
        readonly Action<string> _show;
        readonly List<EffectHandle> _handles = new List<EffectHandle>(16);
        string _pending;

        internal EffectDevCommands(IEffectBackend backend, Action<string> show)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _show = show ?? (_ => { });
        }

        /// <summary>The handles started and not yet stopped (tests).</summary>
        internal int Started => _handles.Count;

        /// <summary>Adds <c>/effect</c> to the command line of a world and stops its effects on a map change.</summary>
        internal static EffectDevCommands Register(DevCommandLine line, WorldBootstrap world)
        {
            var backend = new WorldEffectBackend(world);
            var commands = new EffectDevCommands(backend, line.Show);
            line.AddWithArguments("/effect", commands.Run);
            world.MapChanged += _ => commands.OnMapChanged();
            return commands;
        }

        internal void OnMapChanged()
        {
            _backend.StopAll();
            _handles.Clear();
        }

        /// <summary>Runs the arguments of <c>/effect</c>; returns the message to show.</summary>
        internal string Run(string args)
        {
            if (!EffectCommand.TryParse(args, out var cmd, out var error)) return error;
            if (cmd.Kind == EffectCommandKind.Stop) return Stop();

            if (!_backend.IsReady)
            {
                _pending = args;
                if (_backend.IsLoading) return "loading effects, try again in a moment";
                _backend.BeginLoad(OnLoaded);
                return "loading effects...";
            }
            return Play(cmd);
        }

        void OnLoaded(string failure)
        {
            var args = _pending;
            _pending = null;
            if (failure != null)
            {
                _show("effects could not be loaded: " + failure);
                return;
            }
            if (args != null) _show(Run(args));
        }

        string Stop()
        {
            int n = 0;
            for (int i = 0; i < _handles.Count; i++)
            {
                _backend.Stop(_handles[i]);
                n++;
            }
            _handles.Clear();
            return "stopped " + n;
        }

        string Play(EffectCommand cmd)
        {
            if (cmd.Kind == EffectCommandKind.PlayOnSelf)
            {
                if (!_backend.HasCharacter) return "no character";
            }
            else
            {
                if (!_backend.HasMap) return "no map";
                if (!_backend.InMap(cmd.CellX, cmd.CellY)) return $"cell {cmd.CellX} {cmd.CellY} is outside the map";
            }

            bool known = _backend.Resolve(cmd.Path, out string key);
            var handle = cmd.Kind == EffectCommandKind.PlayOnSelf
                ? _backend.PlayOnSelf(cmd.Path, cmd.Loop)
                : _backend.PlayAtCell(cmd.Path, cmd.CellX, cmd.CellY, cmd.Loop);
            if (!handle.IsValid) return "nothing was played (see the log)";
            _handles.Add(handle);
            return known ? "playing " + key : "unknown effect path: " + cmd.Path + " (placeholder playing)";
        }
    }

    /// <summary>The backend on the game's <see cref="EffectSystem"/> and a world; the catalog, the settings and the effect sprite catalog load through Addressables (D31).</summary>
    sealed class WorldEffectBackend : IEffectBackend
    {
        const string SettingsKey = "Assets/Settings/Effects/EffectSettings.asset";

        readonly WorldBootstrap _world;
        AsyncOperationHandle<EffectCatalog> _catalog;
        AsyncOperationHandle<EffectSettings> _settings;
        AsyncOperationHandle<SpriteCatalog> _sprites;
        Action<string> _done;
        int _waiting;
        string _failure;

        internal WorldEffectBackend(WorldBootstrap world) => _world = world;

        public bool IsReady => EffectSystem.Catalog != null && EffectSystem.Settings != null;
        public bool IsLoading { get; private set; }

        public void BeginLoad(Action<string> done)
        {
            IsLoading = true;
            _done = done;
            _failure = null;
            _waiting = 3;
            _catalog = EffectCatalog.LoadAsync();
            _settings = Addressables.LoadAssetAsync<EffectSettings>(SettingsKey);
            _sprites = SpriteCatalog.LoadAsync(SpriteCatalogCategory.Effects);
            _catalog.Completed += _ => OneDone();
            _settings.Completed += _ => OneDone();
            _sprites.Completed += _ => OneDone();
        }

        void OneDone()
        {
            if (--_waiting > 0) return;
            IsLoading = false;
            var done = _done;
            _done = null;
            if (_catalog.Status != AsyncOperationStatus.Succeeded) _failure = "catalog: " + _catalog.OperationException?.Message;
            else if (_settings.Status != AsyncOperationStatus.Succeeded) _failure = "settings: " + _settings.OperationException?.Message;
            if (_failure != null)
            {
                if (_catalog.IsValid()) Addressables.Release(_catalog);
                if (_settings.IsValid()) Addressables.Release(_settings);
                done?.Invoke(_failure);
                return;
            }
            // A sprite catalog that failed leaves sprite entries undrawn; the rest plays.
            if (_sprites.Status == AsyncOperationStatus.Succeeded) EffectSystem.SpriteCatalog = _sprites.Result;
            else Debug.LogWarning("[Effects] no effect sprite catalog; sprite entries cannot draw");
            EffectSystem.SpriteMaterial = _world.UnitMaterial;
            EffectSystem.Settings = _settings.Result;
            EffectSystem.Catalog = _catalog.Result;
            done?.Invoke(null);
        }

        public bool HasCharacter => OwnTransform() != null;
        public bool HasMap => _world.Map != null;
        public bool InMap(int x, int y) => _world.Map != null && _world.Map.InBounds(x, y);

        public bool Resolve(string path, out string key)
        {
            key = path;
            if (!EffectSystem.Catalog.TryResolve(path, out _)) return false;
            if (EffectPath.TryNormalize(path, out var normal)) key = normal;
            return true;
        }

        public EffectHandle PlayOnSelf(string path, bool loop) => EffectSystem.PlayOn(path, OwnTransform(), loop);
        public EffectHandle PlayAtCell(string path, int x, int y, bool loop) => EffectSystem.PlayAtCell(path, _world.Map, x, y, loop);
        public void Stop(EffectHandle handle) => EffectSystem.Stop(handle);
        public void StopAll() => EffectSystem.StopAll();

        Transform OwnTransform()
        {
            var model = _world.Session?.Model;
            var view = model != null ? _world.Presenter?.ViewOf(model.OwnId) : null;
            return view != null ? view.transform : null;
        }
    }
}
