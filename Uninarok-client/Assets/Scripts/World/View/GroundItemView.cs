using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering;
using Uninarok.Catalog;
using Uninarok.Map;
using Uninarok.Sprites;
using Uninarok.Units;

namespace Uninarok.World.View
{
    /// <summary>
    /// One item on the ground (SPEC-0010 FR-16; PLAN-0010 4.9): its ground sprite from the catalog, an animation or a still,
    /// facing the camera as units do, over the client's shadow. Pooled by <see cref="WorldPresenter"/>; it has no <c>Update</c>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GroundItemView : MonoBehaviour
    {
        IUnitAssetSource _assets;
        Material _material;
        Transform _pivot;
        SpriteActor _actor;
        SpriteRenderer _still;
        UnitShadow _shadow;
        float _yaw;
        AssetReference _held, _heldShadow;
        int _request;

        /// <summary>The item shown or being loaded; 0: none.</summary>
        public int ItemId { get; private set; }
        /// <summary>Its sprite is loaded and shown.</summary>
        public bool Loaded { get; private set; }

        internal SpriteRenderer Still => _still;
        internal SpriteActor Actor => _actor;
        internal UnitShadow Shadow => _shadow;

        /// <summary>The rectangle of its sprite in the pivot's plane, in Unity units; empty until it is shown (SPEC-0011 FR-10).</summary>
        public Rect PickRect { get; private set; }

        /// <summary>The scale of its shadow; 0: none. Counts from the next <see cref="Show"/>.</summary>
        public float ShadowScale { get; set; }

        /// <summary>How high above the ground it is, in world units; its shadow stays on the ground.</summary>
        public float Height
        {
            get => _pivot.localPosition.y;
            set => _pivot.localPosition = new Vector3(0f, value, 0f);
        }

        internal static GroundItemView Create(IUnitAssetSource assets, Material material, Transform parent)
        {
            var go = new GameObject("Ground Item");
            go.SetActive(false);
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<GroundItemView>();
            view._assets = assets;
            view._material = material;
            var pivot = new GameObject("Pivot");
            view._pivot = pivot.transform;
            view._pivot.SetParent(go.transform, false);
            return view;
        }

        /// <summary>Shows an item's sprite; false when the catalog has none (the caller reports it).</summary>
        public bool Show(int itemId, bool identified)
        {
            Clear();
            ItemId = itemId;
            if (!_assets.Catalog.TryGetGroundItem(itemId, identified, out var sprite) || sprite.IsEmpty) return false;
            int request = _request;
            if (sprite.HasAnimation)
            {
                _held = sprite.Animation;
                _assets.Acquire<SpriteAnimation>(sprite.Animation, a =>
                {
                    if (request != _request || a == null) return;
                    EnsureActor();
                    _actor.Data = a;
                    _actor.gameObject.SetActive(true);
                    _actor.ShowFrame(0, 0, 0);
                    PickRect = a.Actions.Length > 0 && a.Actions[0].FrameCount > 0 && a.TryGetFrameRect(a.Actions[0].FirstFrame, out var rect) ? rect : default;
                    Loaded = true;
                });
            }
            else
            {
                _held = sprite.Still;
                _assets.Acquire<Sprite>(sprite.Still, s =>
                {
                    if (request != _request || s == null) return;
                    EnsureStill();
                    _still.sprite = s;
                    _still.enabled = true;
                    var b = s.bounds;
                    PickRect = Rect.MinMaxRect(b.min.x, b.min.y, b.max.x, b.max.y);
                    Loaded = true;
                });
            }
            ShowShadow(request);
            return true;
        }

        void ShowShadow(int request)
        {
            var tables = _assets.Tables;
            if (ShadowScale <= 0f || tables == null || tables.Shadow.IsEmpty) return;
            if (tables.Shadow.HasAnimation)
            {
                _heldShadow = tables.Shadow.Animation;
                _assets.Acquire<SpriteAnimation>(tables.Shadow.Animation, a =>
                {
                    if (request == _request && a != null && a.Sprites.Length > 0) Apply(a.Sprites[0]);
                });
            }
            else
            {
                _heldShadow = tables.Shadow.Still;
                _assets.Acquire<Sprite>(tables.Shadow.Still, s =>
                {
                    if (request == _request && s != null) Apply(s);
                });
            }

            void Apply(Sprite sprite)
            {
                _shadow ??= new UnitShadow(transform, _material);
                _shadow.Apply(sprite, ShadowScale);
                _shadow.Turn(_yaw);
                _shadow.Visible = true;
            }
        }

        /// <summary>Shows nothing and releases the sprite.</summary>
        public void Clear()
        {
            _request++;
            ItemId = 0;
            Loaded = false;
            PickRect = default;
            if (_actor != null)
            {
                _actor.Hide();
                _actor.Data = null;
                _actor.gameObject.SetActive(false);
            }
            if (_still != null)
            {
                _still.enabled = false;
                _still.sprite = null;
            }
            Height = 0f;
            if (_shadow != null)
            {
                _shadow.Visible = false;
                _shadow.Apply(null, 0f);
            }
            Release(ref _held);
            Release(ref _heldShadow);
        }

        void Release(ref AssetReference held)
        {
            if (held == null) return;
            _assets.Release(held);
            held = null;
        }

        public void PlaceOn(MapData map, Vector3 position)
        {
            if (map != null && map.TryGetHeight(position, out float y)) position.y = y;
            transform.position = position;
        }

        internal void Face(in Quaternion camera)
        {
            _pivot.rotation = camera;
            _yaw = UnitSystem.YawOf(camera);
            if (_shadow != null) _shadow.Turn(_yaw);
        }

        void OnDestroy() => Clear();

        void EnsureActor()
        {
            if (_actor != null) return;
            var go = new GameObject("Sprite");
            go.SetActive(false);
            go.transform.SetParent(_pivot, false);
            _actor = go.AddComponent<SpriteActor>();
            _actor.Driven = true;
            _actor.Billboard = false;
            _actor.Material = _material;
            _actor.SetGround(0f, 1f); // SPEC-0009 FR-16
        }

        void EnsureStill()
        {
            if (_still != null) return;
            var go = new GameObject("Still");
            go.transform.SetParent(_pivot, false);
            _still = go.AddComponent<SpriteRenderer>();
            _still.shadowCastingMode = ShadowCastingMode.Off;
            _still.receiveShadows = false;
            if (_material != null) _still.sharedMaterial = _material;
            go.transform.localScale = new Vector3(1f, 1f, SpriteActor.GroundScaleZ(0f, 1f)); // SPEC-0009 FR-16
        }
    }

    /// <summary>
    /// Turns the ground items to the camera after it moved (the order of <c>UnitSystem</c>'s driver, PLAN-0010 §5): all of them
    /// when it turned, new ones at once.
    /// </summary>
    [AddComponentMenu("")]
    [DefaultExecutionOrder(905)]
    internal sealed class GroundItemFacing : MonoBehaviour
    {
        internal WorldPresenter Presenter { get; set; }

        void LateUpdate()
        {
            var camera = UnitSystem.Camera != null ? UnitSystem.Camera : Camera.main;
            if (camera != null && Presenter != null) Presenter.FaceItems(camera.transform.rotation);
        }
    }
}
