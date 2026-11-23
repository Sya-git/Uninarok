using UnityEngine;
using Uninarok.Sprites;

namespace Uninarok.World.View
{
    /// <summary>
    /// One projectile on the screen (SPEC-0011 FR-14; PLAN-0011 4.8): a driven <see cref="SpriteActor"/> of the arrow, in the camera
    /// plane and turned along its flight on the screen. Pooled by <see cref="CombatOverlay"/>, placed each frame.
    /// </summary>
    sealed class ProjectileView : MonoBehaviour
    {
        SpriteActor _actor;
        Transform _turn;

        internal static ProjectileView Create(Transform parent, SpriteAnimation arrow, Material material)
        {
            var go = new GameObject("Projectile");
            go.SetActive(false);
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<ProjectileView>();
            var turn = new GameObject("Turn");
            turn.transform.SetParent(go.transform, false);
            view._turn = turn.transform;
            var sprite = new GameObject("Sprite");
            sprite.SetActive(false);
            sprite.transform.SetParent(turn.transform, false);
            view._actor = sprite.AddComponent<SpriteActor>();
            view._actor.Driven = true;
            view._actor.Billboard = false;
            view._actor.Material = material;
            view._actor.Data = arrow;
            sprite.SetActive(true);
            view._actor.ShowFrame(0, 0, 0);
            return view;
        }

        /// <summary>At a point of its flight, facing the camera, its tip along <paramref name="screenAngle"/> (degrees, from the screen's x).</summary>
        internal void Place(in Vector3 position, in Quaternion camera, float screenAngle)
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            transform.SetPositionAndRotation(position, camera);
            // The arrow's picture points down (−y): turned so that its tip leads.
            _turn.localRotation = Quaternion.Euler(0f, 0f, screenAngle + 90f);
        }

        internal void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }
    }
}
