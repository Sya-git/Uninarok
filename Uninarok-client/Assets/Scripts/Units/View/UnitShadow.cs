using UnityEngine;
using UnityEngine.Rendering;

namespace Uninarok.Units
{
    /// <summary>
    /// The ground shadow of a unit (PLAN-0009 4.6, D12, D20; SPEC-0009 FR-11): the client's shadow sprite, flat on the ground
    /// under the unit, at the shadow table's scale × the unit's display scale. It keeps its long side across the screen
    /// (camera yaw only). Scale 0 draws nothing. A plain object owned by its view (a unit's, or a ground item's, SPEC-0010 FR-16): no
    /// component of its own.
    /// </summary>
    public sealed class UnitShadow
    {
        /// <summary>Above the ground, so the two do not fight for depth.</summary>
        const float Lift = 0.02f;
        /// <summary>
        /// The ACT gives the sprite alpha 0x80, which the client blends in gamma space; over mid-bright ground that looks as 0.69
        /// does in our linear space (PLAN-0009 D27). The static sprite carries no tint.
        /// </summary>
        static Color Tint => new Color(1f, 1f, 1f, 0.69f);

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
        internal void Apply(UnitParts parts)
        {
            var sprite = parts.ShadowSprite;
            if (sprite == null && parts.ShadowAnimation != null && parts.ShadowAnimation.Sprites.Length > 0) sprite = parts.ShadowAnimation.Sprites[0];
            Apply(sprite, parts.HasShadow ? parts.ShadowScale * parts.Scale : 0f);
        }

        /// <summary>Takes a shadow sprite at a scale.</summary>
        public void Apply(Sprite sprite, float scale)
        {
            Scale = scale;
            _renderer.sprite = Scale > 0f ? sprite : null;
            _transform.localPosition = new Vector3(0f, Lift, 0f);
            _transform.localScale = new Vector3(Scale, Scale, 1f);
        }

        public void Turn(float yaw) => _transform.rotation = Quaternion.Euler(90f, yaw, 0f);
    }
}
