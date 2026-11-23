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
    /// Shows the world model (SPEC-0010 FR-3 to FR-8, FR-16; PLAN-0010 4.9): one unit view per unit and one ground item view per
    /// item, taken from pools. It reads the model each frame by version numbers and never writes it. In steady state a sync
    /// allocates nothing.
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
        readonly Stack<GroundItemView> _itemPool = new Stack<GroundItemView>(PreWarm);
        readonly HashSet<int> _itemsWithoutSprite = new HashSet<int>();
        readonly GroundItemFacing _facing;
        Quaternion _cameraRotation;
        bool _turnItems;

        WorldModel _model;
        MapData _map;

        // By slot of the model.
        UnitView[] _views = Array.Empty<UnitView>();
        GroundItemView[] _items = Array.Empty<GroundItemView>();
        int[] _generations = Array.Empty<int>();
        int[] _versions = Array.Empty<int>();
        int[] _animations = Array.Empty<int>();
        EntityLook[] _looks = Array.Empty<EntityLook>();
        // SPEC-0011: the timeline's motion version last played, whether a motion plays, whether a death waits for its hit.
        int[] _motionVersions = Array.Empty<int>();
        bool[] _inMotion = Array.Empty<bool>();
        bool[] _deathHeld = Array.Empty<bool>();
        int _limit;

        // The own character's view stays loaded between two maps, so the first frame of the new one shows it (FR-12, D24).
        UnitView _ownView, _keptOwn;

        public WorldPresenter(UnitLoader loader, Material unitMaterial, Transform root, WorldSettings settings)
        {
            _loader = loader ?? throw new ArgumentNullException(nameof(loader));
            _unitMaterial = unitMaterial;
            _root = root;
            _settings = settings != null ? settings : throw new ArgumentNullException(nameof(settings));
            for (int i = 0; i < PreWarm; i++)
            {
                _pool.Push(NewView());
                _itemPool.Push(NewItem());
            }
            var facing = new GameObject("Ground Item Facing") { hideFlags = HideFlags.HideInHierarchy };
            if (root != null) facing.transform.SetParent(root, false);
            _facing = facing.AddComponent<GroundItemFacing>();
            _facing.Presenter = this;
        }

        /// <summary>The motions of the blows (SPEC-0011 FR-4, FR-9, FR-10); without one only the base animations play.</summary>
        public CombatTimeline Timeline { get; set; }

        /// <summary>Units shown.</summary>
        public int ShownCount { get; private set; }

        /// <summary>Items shown.</summary>
        public int ItemCount { get; private set; }

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
                if (!used)
                {
                    if (_generations[slot] != 0) Release(slot);
                    continue;
                }
                ref readonly var e = ref model[slot];
                if (_generations[slot] != e.Generation)
                {
                    if (_generations[slot] != 0) Release(slot);
                    _generations[slot] = e.Generation;
                    if (e.IsItem) ShowItem(slot, e);
                    else Show(slot, e);
                }
                else if (e.IsItem) UpdateItem(slot, e);
                else if (!ReferenceEquals(_views[slot], null)) Update(slot, _views[slot], e);
            }
            _limit = model.SlotLimit;
        }

        /// <summary>The view of a unit; null when it is not shown.</summary>
        public UnitView ViewOf(uint id) =>
            _model != null && _model.TryGetSlot(id, out int slot) && slot < _views.Length && _generations[slot] == _model[slot].Generation ? _views[slot] : null;

        /// <summary>The view of an item on the ground; null when it is not shown.</summary>
        public GroundItemView ItemViewOf(uint id) =>
            _model != null && _model.TryGetSlot(id, out int slot) && slot < _items.Length && _generations[slot] == _model[slot].Generation ? _items[slot] : null;

        /// <summary>Back to the pool with the own character's view too: the session is over.</summary>
        public void ForgetOwn()
        {
            ReleaseAll();
            var view = _keptOwn;
            _keptOwn = null;
            if (view == null) return;
            view.Clear();
            _pool.Push(view);
        }

        /// <summary>Back to the pool with every view but the own character's, which is hidden and kept (map change).</summary>
        public void ReleaseAll()
        {
            for (int i = 0; i < _views.Length; i++)
                if (_generations[i] != 0) Release(i);
            _limit = 0;
            _model = null;
        }

        public void Dispose()
        {
            ForgetOwn();
            while (_pool.Count > 0)
            {
                var view = _pool.Pop();
                if (view != null) UnityEngine.Object.Destroy(view.gameObject);
            }
            while (_itemPool.Count > 0)
            {
                var item = _itemPool.Pop();
                if (item != null) UnityEngine.Object.Destroy(item.gameObject);
            }
            if (_facing != null) UnityEngine.Object.Destroy(_facing.gameObject);
        }

        // ---- one unit ----

        void Show(int slot, in Entity e)
        {
            bool own = e.Id == _model.OwnId;
            var view = own && _keptOwn != null ? _keptOwn : Take();
            if (own)
            {
                _ownView = view;
                _keptOwn = null;
            }
            _views[slot] = view;
            _looks[slot] = e.Look;
            _versions[slot] = e.Version;
            _animations[slot] = e.AnimationVersion;
            _motionVersions[slot] = 0;
            _inMotion[slot] = false;
            _deathHeld[slot] = false;
            ShownCount++;
            view.Facing = e.Facing;
            view.HeadFacing = (HeadFacing)e.Head;
            Place(view, e);
            view.gameObject.SetActive(true);
            if (!EntityLookMapping.IsInvisible(e.Look)) view.SetLook(EntityLookMapping.ToUnitLook(e.Kind, e.Look));
            Animate(view, e);
            // It died before it came into view: it lies, it does not fall.
            if (e.State == EntityState.Dead) view.SetFrame(int.MaxValue / 2);
            Motion(slot, view, e);
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
                    // While a motion plays, or a death waits for its hit, the change is noted and played when it ends (PLAN-0011 4.9).
                    if (!_inMotion[slot] && !HoldsDeath(slot, e)) Animate(view, e);
                    else if (e.State == EntityState.Dead) _deathHeld[slot] = true;
                }
                else if (e.Walking && !_inMotion[slot]) view.Speed = WalkSpeed(e);
                Place(view, e);
            }
            else if (e.Walking)
            {
                view.Facing = e.Facing;
                Place(view, e);
            }
            Motion(slot, view, e);
        }

        bool HoldsDeath(int slot, in Entity e) => e.State == EntityState.Dead && Timeline != null && Timeline.HoldsDeath(slot, Timeline.Now);

        // SPEC-0011 FR-4, FR-9, FR-10: the timeline's motion when it changes; the base animation when it ends.
        void Motion(int slot, UnitView view, in Entity e)
        {
            var timeline = Timeline;
            if (timeline == null) return;
            if (_deathHeld[slot] && !timeline.HoldsDeath(slot, timeline.Now))
            {
                _deathHeld[slot] = false;
                _inMotion[slot] = false;
                Animate(view, e); // the death, after its hurt
                return;
            }
            ref readonly var m = ref timeline.MotionOf(slot);
            if (m.Version == _motionVersions[slot]) return;
            _motionVersions[slot] = m.Version;
            switch (m.Kind)
            {
                case MotionKind.Attack:
                    view.Speed = 1f;
                    view.PlayFor(m.AttackNumber == 3 ? PcAnimation.Attack3 : m.AttackNumber == 2 ? PcAnimation.Attack2 : PcAnimation.Attack1, m.DurationMs, PlayMode.OnceHold);
                    _inMotion[slot] = true;
                    break;
                case MotionKind.Hurt:
                    view.Speed = 1f;
                    view.PlayFor(PcAnimation.Hurt, m.DurationMs, PlayMode.OnceHold);
                    _inMotion[slot] = true;
                    break;
                case MotionKind.PickUp:
                    view.Speed = 1f;
                    view.Play(PcAnimation.PickUp, PlayMode.OnceHold);
                    _inMotion[slot] = true;
                    break;
                case MotionKind.Standby:
                    view.Speed = 1f;
                    view.Play(PcAnimation.Standby);
                    _inMotion[slot] = true;
                    break;
                default:
                    _inMotion[slot] = false;
                    if (e.State == EntityState.Dead && timeline.HoldsDeath(slot, timeline.Now)) _deathHeld[slot] = true;
                    else Animate(view, e);
                    break;
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

        // ---- one item ----

        void ShowItem(int slot, in Entity e)
        {
            var view = TakeItem();
            _items[slot] = view;
            _versions[slot] = e.Version;
            ItemCount++;
            view.PlaceOn(_map, Position(_map, e.PosX, e.PosY));
            view.Face(_cameraRotation);
            _turnItems = true;
            view.ShadowScale = _settings.ItemShadowScale;
            view.gameObject.SetActive(true);
            if (!view.Show(e.ItemId, e.Identified) && _itemsWithoutSprite.Add(e.ItemId))
                Debug.LogWarning($"[World] item {e.ItemId} has no ground sprite in the catalog");
            view.Height = e.Height * _map.CellSize;
        }

        void UpdateItem(int slot, in Entity e)
        {
            var view = _items[slot];
            if (ReferenceEquals(view, null)) return;
            float height = e.Height * _map.CellSize;
            if (view.Height != height) view.Height = height;
            if (_versions[slot] == e.Version) return;
            _versions[slot] = e.Version;
            if (view.ItemId != e.ItemId) view.Show(e.ItemId, e.Identified);
            view.PlaceOn(_map, Position(_map, e.PosX, e.PosY));
        }

        /// <summary>Items face the camera fully, as units do; written only when it turned or an item is new.</summary>
        internal void FaceItems(in Quaternion camera)
        {
            if (!_turnItems && camera.Equals(_cameraRotation)) return;
            _turnItems = false;
            _cameraRotation = camera;
            if (ItemCount == 0) return;
            for (int i = 0; i < _limit; i++)
                if (!ReferenceEquals(_items[i], null)) _items[i].Face(camera);
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

        GroundItemView NewItem() => GroundItemView.Create(_loader.Source, _unitMaterial, _root);

        GroundItemView TakeItem()
        {
            while (_itemPool.Count > 0)
            {
                var view = _itemPool.Pop();
                if (view != null) return view;
            }
            return NewItem();
        }

        // A view that shows the last unit would show it again for a moment: cleared first (D13).
        void Release(int slot)
        {
            _generations[slot] = 0;
            var item = _items[slot];
            if (!ReferenceEquals(item, null))
            {
                _items[slot] = null;
                ItemCount--;
                if (item == null) return;
                item.Clear();
                item.gameObject.SetActive(false);
                _itemPool.Push(item);
                return;
            }
            var view = _views[slot];
            if (ReferenceEquals(view, null)) return;
            _views[slot] = null;
            ShownCount--;
            bool own = ReferenceEquals(view, _ownView);
            if (own) _ownView = null;
            if (view == null) return; // destroyed with its scene
            view.gameObject.SetActive(false);
            if (own)
            {
                _keptOwn = view;
                return;
            }
            view.Clear();
            _pool.Push(view);
        }

        void Resize(int capacity)
        {
            Array.Resize(ref _views, capacity);
            Array.Resize(ref _items, capacity);
            Array.Resize(ref _generations, capacity);
            Array.Resize(ref _versions, capacity);
            Array.Resize(ref _animations, capacity);
            Array.Resize(ref _looks, capacity);
            Array.Resize(ref _motionVersions, capacity);
            Array.Resize(ref _inMotion, capacity);
            Array.Resize(ref _deathHeld, capacity);
        }
    }
}
