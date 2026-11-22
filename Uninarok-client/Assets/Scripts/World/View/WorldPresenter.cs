using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using Uninarok.Map;
using Uninarok.Units;
using PlayMode = Uninarok.Units.PlayMode;

namespace Uninarok.World.View
{
    /// <summary>
    /// Shows the world model (SPEC-0010 FR-3 to FR-8, FR-16; PLAN-0010 4.9): one unit view per unit, taken from a pool. It reads
    /// the model each frame by version numbers and never writes it. In steady state a sync allocates nothing.
    /// </summary>
    public sealed class WorldPresenter : IDisposable
    {
        const int PreWarm = 64;

        internal static readonly ProfilerMarker SyncMarker = new ProfilerMarker("WorldPresenter.Sync");

        readonly UnitLoader _loader;
        readonly Material _unitMaterial;
        readonly Transform _root;
        readonly WorldSettings _settings;
        readonly Stack<UnitView> _pool = new Stack<UnitView>(PreWarm);

        WorldModel _model;
        MapData _map;

        // By slot of the model.
        UnitView[] _views = Array.Empty<UnitView>();
        int[] _generations = Array.Empty<int>();
        int[] _versions = Array.Empty<int>();
        int[] _animations = Array.Empty<int>();
        EntityLook[] _looks = Array.Empty<EntityLook>();
        int _limit;

        public WorldPresenter(UnitLoader loader, Material unitMaterial, Transform root, WorldSettings settings)
        {
            _loader = loader ?? throw new ArgumentNullException(nameof(loader));
            _unitMaterial = unitMaterial;
            _root = root;
            _settings = settings != null ? settings : throw new ArgumentNullException(nameof(settings));
            for (int i = 0; i < PreWarm; i++) _pool.Push(NewView());
        }

        /// <summary>Units shown.</summary>
        public int ShownCount { get; private set; }

        /// <summary>The map whose ground the views stand on; without one (null) nothing is drawn.</summary>
        public MapData Map
        {
            get => _map;
            set
            {
                if (_map == value) return;
                ReleaseAll();
                _map = value;
            }
        }

        /// <summary>Brings the views to the state of the model; call it each frame after <see cref="WorldModel.Advance"/>.</summary>
        public void Sync(WorldModel model)
        {
            if (model != _model) ReleaseAll();
            _model = model;
            if (model == null || _map == null) return;

            using var marker = SyncMarker.Auto();
            if (_views.Length < model.Capacity) Resize(model.Capacity);
            int limit = Math.Max(_limit, model.SlotLimit);
            for (int slot = 0; slot < limit; slot++)
            {
                bool used = slot < model.SlotLimit && model.IsUsed(slot);
                var view = _views[slot];
                if (!used)
                {
                    if (!ReferenceEquals(view, null)) Release(slot);
                    continue;
                }
                ref readonly var e = ref model[slot];
                if (_generations[slot] != e.Generation)
                {
                    if (!ReferenceEquals(view, null)) Release(slot);
                    _generations[slot] = e.Generation;
                    if (e.IsItem) continue;
                    Show(slot, e);
                }
                else if (ReferenceEquals(view, null)) continue;
                else Update(slot, view, e);
            }
            _limit = model.SlotLimit;
        }

        /// <summary>The view of a unit; null when it is not shown.</summary>
        public UnitView ViewOf(uint id) =>
            _model != null && _model.TryGetSlot(id, out int slot) && slot < _views.Length && _generations[slot] == _model[slot].Generation ? _views[slot] : null;

        /// <summary>Back to the pool with every view (map change, disconnect).</summary>
        public void ReleaseAll()
        {
            for (int i = 0; i < _views.Length; i++)
            {
                if (!ReferenceEquals(_views[i], null)) Release(i);
                _generations[i] = 0;
            }
            _limit = 0;
            _model = null;
        }

        public void Dispose()
        {
            ReleaseAll();
            while (_pool.Count > 0)
            {
                var view = _pool.Pop();
                if (view != null) UnityEngine.Object.Destroy(view.gameObject);
            }
        }

        // ---- one unit ----

        void Show(int slot, in Entity e)
        {
            var view = Take();
            _views[slot] = view;
            _looks[slot] = e.Look;
            _versions[slot] = e.Version;
            _animations[slot] = e.AnimationVersion;
            ShownCount++;
            view.Facing = e.Facing;
            view.HeadFacing = (HeadFacing)e.Head;
            Place(view, e);
            view.gameObject.SetActive(true);
            if (!EntityLookMapping.IsInvisible(e.Look)) view.SetLook(EntityLookMapping.ToUnitLook(e.Kind, e.Look));
            Animate(view, e);
            // It died before it came into view: it lies, it does not fall.
            if (e.State == EntityState.Dead) view.SetFrame(int.MaxValue / 2);
        }

        void Update(int slot, UnitView view, in Entity e)
        {
            if (_versions[slot] != e.Version)
            {
                _versions[slot] = e.Version;
                if (!_looks[slot].Equals(e.Look))
                {
                    _looks[slot] = e.Look;
                    if (EntityLookMapping.IsInvisible(e.Look)) view.Clear();
                    else view.SetLook(EntityLookMapping.ToUnitLook(e.Kind, e.Look));
                }
                view.Facing = e.Facing;
                view.HeadFacing = (HeadFacing)e.Head;
                if (_animations[slot] != e.AnimationVersion)
                {
                    _animations[slot] = e.AnimationVersion;
                    Animate(view, e);
                }
                else if (e.Walking) view.Speed = WalkSpeed(e);
                Place(view, e);
            }
            else if (e.Walking)
            {
                view.Facing = e.Facing;
                Place(view, e);
            }
        }

        // The animation plays at its own speed but the walk, which follows the walk speed (PLAN-0010 4.9).
        void Animate(UnitView view, in Entity e)
        {
            view.Speed = 1f;
            switch (e.State)
            {
                case EntityState.Dead:
                    view.Play(PcAnimation.Dead, PlayMode.OnceHold);
                    break;
                case EntityState.Sitting:
                    view.Play(PcAnimation.Sit);
                    break;
                default:
                    if (e.Walking)
                    {
                        view.Speed = WalkSpeed(e);
                        view.Play(PcAnimation.Walk);
                    }
                    else view.Play(PcAnimation.Idle);
                    break;
            }
        }

        float WalkSpeed(in Entity e) => Math.Max(1, (int)e.Speed) / (float)Math.Max(1, _settings.WalkAnimationSpeed);

        void Place(UnitView view, in Entity e) => view.PlaceOn(_map, Position(_map, e.PosX, e.PosY));

        /// <summary>A position in cells (whole numbers are centres) as a Unity position at height 0.</summary>
        public static Vector3 Position(MapData map, float x, float y)
        {
            var origin = map.Origin;
            return new Vector3(origin.x + (x + 0.5f) * map.CellSize, 0f, origin.y + (y + 0.5f) * map.CellSize * map.RowSign);
        }

        // ---- pool ----

        UnitView NewView()
        {
            var view = UnitView.Create(_loader, _unitMaterial, _root);
            view.gameObject.SetActive(false);
            return view;
        }

        UnitView Take()
        {
            while (_pool.Count > 0)
            {
                var view = _pool.Pop();
                if (view != null) return view;
            }
            return NewView();
        }

        // A view that shows the last unit would show it again for a moment: cleared first (D13).
        void Release(int slot)
        {
            var view = _views[slot];
            _views[slot] = null;
            _generations[slot] = 0;
            ShownCount--;
            if (view == null) return; // destroyed with its scene
            view.Clear();
            view.gameObject.SetActive(false);
            _pool.Push(view);
        }

        void Resize(int capacity)
        {
            Array.Resize(ref _views, capacity);
            Array.Resize(ref _generations, capacity);
            Array.Resize(ref _versions, capacity);
            Array.Resize(ref _animations, capacity);
            Array.Resize(ref _looks, capacity);
        }
    }
}
