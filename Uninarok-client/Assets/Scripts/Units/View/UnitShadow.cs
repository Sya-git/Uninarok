using UnityEngine;
using UnityEngine.Rendering;

namespace Uninarok.Units
{
    /// <summary>
    /// The ground shadow of a unit (PLAN-0009 4.6, D12, D20; SPEC-0009 FR-11): the client's shadow sprite, flat on the ground
    /// under the unit, at the shadow table's scale × the unit's display scale. It keeps its long side across the screen
    /// (camera yaw only). Scale 0 draws nothing. A plain object owned by its <see cref="UnitView"/>: no component of its own.
    /// </summary>
    internal sealed class UnitShadow
    {
        /// <summary>Above the ground, so the two do not fight for depth.</summary>
        const float Lift = 0.02f;
        /// <summary>Candidate: the client draws it see-through; the static sprite carries no tint.</summary>
        static Color Tint => new Color(1f, 1f, 1f, 0.45f);

        readonly SpriteRenderer _renderer;
        readonly Transform _transform;

        public UnitShadow(Transform root, Material material)
        {
            var go = new GameObject("Shadow") { layer = root.gameObject.layer };
            _transform = go.transform;
            _transform.SetParent(root, false);
            _renderer = go.AddComponent<SpriteRenderer>();
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.color = Tint;
            if (material != null) _renderer.sharedMaterial = material;
            // Below every unit: shadows are not part of a unit's sorting group.
            _renderer.sortingOrder = -1;
            _renderer.enabled = false;
        }

        public SpriteRenderer Renderer => _renderer;

        public bool Visible
        {
            get => _renderer.enabled;
            set
            {
                bool on = value && _renderer.sprite != null;
                if (_renderer.enabled != on) _renderer.enabled = on;
            }
        }

        public float Scale { get; private set; }

        /// <summary>Takes the shadow of newly applied parts.</summary>
        public void Apply(UnitParts parts)
        {
            Scale = parts.HasShadow ? parts.ShadowScale * parts.Scale : 0f;
            var sprite = parts.ShadowSprite;
            if (sprite == null && parts.ShadowAnimation != null && parts.ShadowAnimation.Sprites.Length > 0) sprite = parts.ShadowAnimation.Sprites[0];
            _renderer.sprite = Scale > 0f ? sprite : null;
            _transform.localPosition = new Vector3(0f, Lift, 0f);
            _transform.localScale = new Vector3(Scale, Scale, 1f);
        }

        public void Turn(float yaw) => _transform.rotation = Quaternion.Euler(90f, yaw, 0f);
    }
}
