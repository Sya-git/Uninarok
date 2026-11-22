using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Uninarok.CameraRig;
using Uninarok.Map;
using Uninarok.Networking.Data;

namespace Uninarok.Units.Dev
{
    /// <summary>
    /// The Entity Sandbox (PLAN-0009 4.8; SPEC-0009 FR-13): a real map without a server, with units of chosen looks. It loads
    /// the map scene additively, the catalog and the composition tables, and owns the spawned units;
    /// <see cref="EntitySandboxPanel"/> drives it by hand and <see cref="EntitySandboxStress"/> fills it. Dev only: the assembly
    /// does not exist in release builds (D21) and the scene is in no build list.
    /// </summary>
    public sealed class EntitySandbox : MonoBehaviour
    {
        public RoCamera Rig;
        /// <summary><c>sprite_unit.mat</c>.</summary>
        public Material UnitMaterial;
        public string MapScene = "Assets/Scenes/Maps/prontera.unity";
        public Vector2Int StartCell = new Vector2Int(156, 185);

        readonly List<UnitView> _units = new List<UnitView>();
        AddressablesUnitAssetSource _source;
        UnitLoader _loader;

        public bool Ready { get; private set; }
        /// <summary>What is going on, or why it failed.</summary>
        public string Status { get; private set; } = "starting";
        public MapData Map { get; private set; }
        public UnitLoader Loader => _loader;
        public IReadOnlyList<UnitView> Units => _units;
        public UnitView Selected { get; set; }
        public bool CameraFollowsSelected { get; set; }

        IEnumerator Start()
        {
            Status = "loading " + MapScene;
            yield return LoadMap();
            var root = FindAnyObjectByType<MapRoot>();
            Map = root != null ? root.Data : null;
            if (Map == null)
            {
                Status = MapScene + " has no map data";
                Debug.LogError("[EntitySandbox] " + Status);
                yield break;
            }
            if (Rig != null)
            {
                Rig.Target = Map.CellCenter(StartCell.x, StartCell.y);
                Rig.ResetNow();
            }

            Status = "loading the catalog";
            bool done = false;
            string error = null;
            AddressablesUnitAssetSource.LoadAsync((source, reason) =>
            {
                _source = source;
                error = reason;
                done = true;
            });
            while (!done) yield return null;
            if (_source == null)
            {
                Status = "no catalog: " + error;
                Debug.LogError("[EntitySandbox] " + Status);
                yield break;
            }
            _loader = new UnitLoader(_source);
            Ready = true;
            Status = "ready";
        }

        IEnumerator LoadMap()
        {
            if (FindAnyObjectByType<MapRoot>() != null) yield break; // the map is already open
            AsyncOperation load = null;
#if UNITY_EDITOR
            load = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(MapScene, new LoadSceneParameters(LoadSceneMode.Additive));
#else
            load = SceneManager.LoadSceneAsync(MapScene, LoadSceneMode.Additive);
#endif
            while (load != null && !load.isDone) yield return null;
        }

        void Update()
        {
            if (Rig != null) Rig.Follow = CameraFollowsSelected && Selected != null ? Selected.transform : null;
        }

        void OnDestroy()
        {
            Clear();
            if (Rig != null) Rig.Follow = null;
            _source?.Dispose();
            _source = null;
        }

        /// <summary>Spawns a unit on a cell.</summary>
        public UnitView Spawn(in UnitLook look, int x, int z, UninarokDirection facing = UninarokDirection.South)
        {
            var view = UnitView.Create(_loader, UnitMaterial, transform, look.Kind == UnitKind.Player ? $"Player {look.JobId}" : $"Unit {look.JobId}");
            view.Facing = facing;
            view.PlaceOn(Map, Map.CellCenter(x, z));
            view.SetLook(look);
            _units.Add(view);
            return view;
        }

        public void Remove(UnitView view)
        {
            if (!_units.Remove(view)) return;
            if (Selected == view) Selected = null;
            Destroy(view.gameObject);
        }

        public void Clear()
        {
            foreach (var u in _units)
                if (u != null) Destroy(u.gameObject);
            _units.Clear();
            Selected = null;
        }

        /// <summary>The unit nearest to a cell, within about one and a half cells.</summary>
        public UnitView UnitNear(int x, int z)
        {
            var at = Map.CellCenter(x, z);
            UnitView best = null;
            float bestDistance = 0.75f * 0.75f;
            foreach (var u in _units)
            {
                float d = (u.transform.position - at).sqrMagnitude;
                if (d >= bestDistance) continue;
                bestDistance = d;
                best = u;
            }
            return best;
        }

        /// <summary>Head left and right every 150 ms for two seconds, as <c>/doridori</c> will drive it.</summary>
        public void Doridori(UnitView view) => StartCoroutine(DoridoriRoutine(view));

        static IEnumerator DoridoriRoutine(UnitView view)
        {
            view.Play(PcAnimation.Idle);
            float end = Time.time + 2f;
            bool left = true;
            while (view != null && Time.time < end)
            {
                view.HeadFacing = left ? HeadFacing.Left : HeadFacing.Right;
                left = !left;
                yield return new WaitForSeconds(0.15f);
            }
            if (view != null) view.HeadFacing = HeadFacing.Straight;
        }

        /// <summary>The body through the eight directions, as <c>/bangbang</c> will drive it.</summary>
        public void Bangbang(UnitView view) => StartCoroutine(BangbangRoutine(view));

        static IEnumerator BangbangRoutine(UnitView view)
        {
            for (int i = 0; i < 8 && view != null; i++)
            {
                view.Facing = (UninarokDirection)(((int)view.Facing + 1) & 7);
                yield return new WaitForSeconds(0.1f);
            }
        }
    }
}
