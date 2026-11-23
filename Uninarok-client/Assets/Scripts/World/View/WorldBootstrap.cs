using System;
using Unity.Profiling;
using UnityEngine;
using Uninarok.CameraRig;
using Uninarok.Catalog;
using Uninarok.Map;
using Uninarok.Networking.Client;
using Uninarok.Networking.Flow;
using Uninarok.Units;

namespace Uninarok.World.View
{
    /// <summary>
    /// Joins the network flow, the world and what is drawn (SPEC-0010 FR-1, FR-12, FR-15; PLAN-0010 4.7): it loads the map the
    /// flow asks for, begins the world on it, tells the flow, and runs the world each frame before the camera moves (§5). In
    /// Bootstrap, next to the network client; it owns everything of the world.
    /// </summary>
    [DefaultExecutionOrder(10)]
    [DisallowMultipleComponent]
    public sealed class WorldBootstrap : MonoBehaviour
    {
        public RoCamera CameraRig;
        public WorldSettings Settings;
        /// <summary><c>Settings/World/CombatSettings.asset</c> (SPEC-0011); without it the own combat uses its defaults and no blow is shown.</summary>
        public CombatSettings Combat;
        /// <summary><c>sprite_unit.mat</c>.</summary>
        public Material UnitMaterial;

        /// <summary>The whole world update of a frame: own character, model and views (FR-17).</summary>
        internal static readonly ProfilerMarker UpdateMarker = new ProfilerMarker("World.Update");

        NetworkFlow _flow;
        MapSceneLoader _scenes;
        AddressablesUnitAssetSource _assets;
        WorldPresenter _presenter;
        CombatOverlay _overlay;
        MapData _map;
        bool _mapReady;        // a model runs, with or without a map to show it on
        int _loads;
        bool _ownsAssets;

        public ServerClock Clock { get; private set; }
        public WorldSession Session { get; private set; }
        public WorldPresenter Presenter => _presenter;
        /// <summary>The map shown; null while none is.</summary>
        public MapData Map => _map;
        public string MapName { get; private set; }

        /// <summary>The map is loaded and the world begins, or ends (null data: no map, or one that can not be shown).</summary>
        public event Action<MapData> MapChanged;

        void Start()
        {
            if (Session != null) return; // a test brought its own connection
            var client = NetworkClient.Instance != null ? NetworkClient.Instance : FindAnyObjectByType<NetworkClient>();
            if (client == null || client.Flow == null)
            {
                Debug.LogWarning("[World] no NetworkClient in the scene; there is no world");
                enabled = false;
                return;
            }
            Connect(client, client.Flow, new UnityClock());
            LoadAssets();
        }

        internal void Connect(INetworkClient client, NetworkFlow flow, IClock clock)
        {
            _flow = flow;
            _scenes = new MapSceneLoader();
            Clock = new ServerClock(clock);
            // Before this component listens: the session ends the old world first.
            Session = new WorldSession(client, flow, Clock, Settings);
            if (Combat != null) Session.UseCombat(Combat);
            else Debug.LogWarning("[World] no combat settings: blows are not shown");
            _flow.MapLoadRequested += OnMapLoadRequested;
            _flow.StateChanged += OnStateChanged;
        }

        // The session does not wait for the catalog (D12): the model runs, and is shown when both are there.
        void LoadAssets() => AddressablesUnitAssetSource.LoadAsync((source, reason) =>
        {
            if (this == null)
            {
                source?.Dispose();
                return;
            }
            if (source == null)
            {
                Debug.LogError("[World] no sprite catalog, nothing is drawn: " + reason);
                return;
            }
            _ownsAssets = true;
            UseAssets(source);
        }, SpriteCatalogCategory.GroundItems);

        internal void UseAssets(AddressablesUnitAssetSource source)
        {
            _assets = source;
            var timeline = Session?.Timeline;
            if (timeline != null)
            {
                var types = new CatalogWeaponTypes(source);
                timeline.Weapons = types;
                timeline.Jobs = types;
            }
            UsePresenter(new WorldPresenter(new UnitLoader(source), UnitMaterial, transform, Settings) { Timeline = timeline });
        }

        internal void UsePresenter(WorldPresenter presenter)
        {
            _presenter = presenter;
            if (presenter != null && presenter.Timeline == null) presenter.Timeline = Session?.Timeline;
            if (presenter != null && Combat != null && Session != null)
            {
                if (_overlay == null) _overlay = gameObject.AddComponent<CombatOverlay>();
                _overlay.SpriteMaterial = UnitMaterial;
                _overlay.Use(Session, presenter, Combat);
            }
            if (_map != null) ShowMap();
        }

        // ---- map entry ----

        void OnMapLoadRequested()
        {
            LeaveMap();
            int load = ++_loads;
            MapName = MapSceneKeys.MapName(_flow.Context.Map.MapName.ToString());
            _scenes.Load(MapName, (result, root) =>
            {
                if (this == null || load != _loads || _flow.State != FlowState.LoadingMap) return;
                if (result == MapSceneLoader.Result.MissingScene) Debug.LogWarning($"[World] map '{MapName}' has no converted scene, or it is not registered (Tools/Ragnarok/Register Map Scenes): it is tracked but not shown");
                else if (result == MapSceneLoader.Result.Failed) Debug.LogWarning($"[World] the scene of map '{MapName}' could not be loaded: it is tracked but not shown");

                _map = result == MapSceneLoader.Result.Loaded ? root.Data : null;
                Session.BeginMap(_map);
                _mapReady = true;
                if (_map != null && _presenter != null) ShowMap();
                MapChanged?.Invoke(_map);
                _flow.MapLoaded();
            });
        }

        // The own character is on the screen before the first frame of the map (FR-12).
        void ShowMap()
        {
            _presenter.Map = _map;
            _presenter.Sync(Session.Model);
            FollowOwn();
            if (CameraRig != null) CameraRig.SnapToTarget();
        }

        void FollowOwn()
        {
            if (CameraRig == null || Session.Model == null) return;
            var own = _presenter.ViewOf(Session.Model.OwnId);
            CameraRig.Follow = own != null ? own.transform : null;
        }

        void LeaveMap()
        {
            if (CameraRig != null) CameraRig.Follow = null;
            _presenter?.ReleaseAll();
            if (_presenter != null) _presenter.Map = null;
            if (_overlay != null) _overlay.Clear();
            _map = null;
            if (!_mapReady) return;
            _mapReady = false;
            MapChanged?.Invoke(null);
        }

        void OnStateChanged(FlowState state)
        {
            if (state == FlowState.LoadingMap || state == FlowState.InGame) return;
            LeaveMap();
            if (state != FlowState.ConnectingToMap) _presenter?.ForgetOwn();
            _loads++;
            // Between two map servers the scene stays: the new one may ask for the same.
            if (state != FlowState.ConnectingToMap) _scenes.Unload();
        }

        // ---- the frame ----

        void LateUpdate()
        {
            var model = Session?.Model;
            if (model == null)
            {
                Session?.Restart.Update(Clock.Now); // out of a map: closed
                return;
            }
            using var marker = UpdateMarker.Auto();
            uint now = Clock.Now;
            model.Advance(now);
            Session.Combat.Tick(now);         // SPEC-0011, PLAN-0011 §2
            Session.Timeline?.Advance(now);
            Session.Restart.Update(now);
            if (_presenter != null && _map != null)
            {
                _presenter.Sync(model);
                if (CameraRig != null && CameraRig.Follow == null) FollowOwn();
            }
        }

        void OnDestroy()
        {
            if (_flow != null)
            {
                _flow.MapLoadRequested -= OnMapLoadRequested;
                _flow.StateChanged -= OnStateChanged;
            }
            if (CameraRig != null) CameraRig.Follow = null;
            Session?.Dispose();
            _presenter?.Dispose();
            _scenes?.Dispose();
            if (_ownsAssets) _assets?.Dispose();
        }
    }
}
