using UnityEngine;

namespace Uninarok.Map
{
    /// <summary>
    /// The animation speed of a placed model, from the map file (PLAN-0006 4.3, D14, D17; SPEC-0006 FR-2). Unity does not
    /// save <see cref="Animator.speed"/> in a scene, so the map scene keeps it here and applies it when the object is
    /// enabled. Nothing else.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class MapModelAnimation : MonoBehaviour
    {
        public float speed = 1f;

        void OnEnable() => GetComponent<Animator>().speed = speed;
    }
}
