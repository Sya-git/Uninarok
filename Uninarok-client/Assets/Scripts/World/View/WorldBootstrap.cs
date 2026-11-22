using System;
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
        /// <summary><c>sprite_unit.mat</c>.</summary>
        public Material UnitMaterial;

        NetworkFlow _flow;
        MapSceneLoader _scenes;
        AddressablesUnitAssetSource _assets;
        WorldPresenter _presenter;
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
            UsePresenter(new WorldPresenter(new UnitLoader(source), UnitMaterial, transform, Settings));
        }

        internal void UsePresenter(WorldPresenter presenter)
        {
            _presenter = presenter;
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
                if (result == MapSceneLoader.Result.MissingScene) Debug.LogWarning($"[World] map '{MapName}' has no converted scene: it is tracked but not shown");
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
            _map = null;
            if (!_mapReady) return;
            _mapReady = false;
            MapChanged?.Invoke(null);
        }

        void OnStateChanged(FlowState state)
        {
            if (state == FlowState.LoadingMap || state == FlowState.InGame) return;
            LeaveMap();
            _loads++;
            // Between two map servers the scene stays: the new one may ask for the same.
            if (state != FlowState.ConnectingToMap) _scenes.Unload();
        }

        // ---- the frame ----

        void LateUpdate()
        {
            var model = Session?.Model;
            if (model == null) return;
            uint now = Clock.Now;
            Session.Own.Tick(now);
            model.Advance(now);
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
