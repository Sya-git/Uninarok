using UnityEngine;

namespace Uninarok.World.View
{
    /// <summary>
    /// The unit or ground item under the pointer (SPEC-0011 FR-2, FR-10; PLAN-0011 4.10, D10, D19): for each shown view the ray
    /// against its pivot's plane, which faces the camera, the point inside its <c>PickRect</c> (taken as at least
    /// <see cref="MinPx"/> pixels wide and high); the hit nearest to the camera wins. The own character, dead units and invisible
    /// looks never catch the pointer; other players and NPCs do, so a player in front of a monster hides it. Allocates nothing.
    /// </summary>
    public sealed class UnitPicker
    {
        /// <summary>The smallest size of a rectangle on the screen, in pixels (<c>CombatSettings.PickMinPx</c>).</summary>
        public float MinPx { get; set; } = 24f;

        /// <summary>The id of the nearest unit or item under <paramref name="pointer"/> (screen pixels); false when none.</summary>
        public bool TryPick(WorldPresenter presenter, WorldModel model, Camera camera, Vector2 pointer, out uint id)
        {
            id = 0;
            if (presenter == null || model == null || camera == null) return false;
            var ray = camera.ScreenPointToRay(pointer);
            var facing = camera.transform.rotation;
            var inverse = Quaternion.Inverse(facing);
            var forward = facing * Vector3.forward;
            var eye = camera.transform.position;
            // World units per pixel at a distance of 1 (perspective), or at any distance (orthographic).
            float perPixel = camera.orthographic
                ? 2f * camera.orthographicSize / Mathf.Max(1, camera.pixelHeight)
                : 2f * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1, camera.pixelHeight);

            float best = float.PositiveInfinity;
            int limit = Mathf.Min(presenter.SlotLimit, model.SlotLimit);
            for (int slot = 0; slot < limit; slot++)
            {
                if (!model.IsUsed(slot)) continue;
                ref readonly var e = ref model[slot];
                Vector3 origin;
                Rect rect;
                if (e.IsItem)
                {
                    var item = presenter.ItemAt(slot);
                    if (ReferenceEquals(item, null) || item == null || !item.Loaded) continue;
                    origin = item.transform.position + Vector3.up * item.Height;
                    rect = item.PickRect;
                }
                else
                {
                    if (e.Id == model.OwnId || e.State == EntityState.Dead || EntityLookMapping.IsInvisible(e.Look)) continue;
                    var view = presenter.UnitAt(slot);
                    if (ReferenceEquals(view, null) || view == null || !view.gameObject.activeInHierarchy) continue;
                    origin = view.transform.position;
                    rect = view.PickRect;
                }
                if (rect.width <= 0f || rect.height <= 0f) continue;

                float denominator = Vector3.Dot(ray.direction, forward);
                if (denominator <= 1e-6f) continue;
                float t = Vector3.Dot(origin - ray.origin, forward) / denominator;
                if (t <= 0f || t >= best) continue;
                var local = inverse * (ray.origin + ray.direction * t - origin);

                float min = MinPx * perPixel * (camera.orthographic ? 1f : Mathf.Max(0f, Vector3.Dot(origin - eye, forward)));
                if (rect.width < min) rect = new Rect(rect.center.x - min * 0.5f, rect.y, min, rect.height);
                if (rect.height < min) rect = new Rect(rect.x, rect.center.y - min * 0.5f, rect.width, min);
                if (!rect.Contains(new Vector2(local.x, local.y))) continue;
                best = t;
                id = e.Id;
            }
            return id != 0;
        }
    }
}
