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
    /// facing the camera as units do. Pooled by <see cref="WorldPresenter"/>; it has no <c>Update</c>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GroundItemView : MonoBehaviour
    {
        IUnitAssetSource _assets;
        Material _material;
        Transform _pivot;
        SpriteActor _actor;
        SpriteRenderer _still;
        AssetReference _held;
        int _request;

        /// <summary>The item shown or being loaded; 0: none.</summary>
        public int ItemId { get; private set; }
        /// <summary>Its sprite is loaded and shown.</summary>
        public bool Loaded { get; private set; }

        internal SpriteRenderer Still => _still;
        internal SpriteActor Actor => _actor;

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
                    Loaded = true;
                });
            }
            return true;
        }

        /// <summary>Shows nothing and releases the sprite.</summary>
        public void Clear()
        {
            _request++;
            ItemId = 0;
            Loaded = false;
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
            if (_held == null) return;
            _assets.Release(_held);
            _held = null;
        }

        public void PlaceOn(MapData map, Vector3 position)
        {
            if (map != null && map.TryGetHeight(position, out float y)) position.y = y;
            transform.position = position;
        }

        internal void Face(in Quaternion camera) => _pivot.rotation = camera;

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
