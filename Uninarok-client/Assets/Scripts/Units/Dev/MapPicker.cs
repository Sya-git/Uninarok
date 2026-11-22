using UnityEngine;
using Uninarok.Map;

namespace Uninarok.Units.Dev
{
    /// <summary>The map cell under a ray, from the map data alone (no colliders): the sandbox's click (PLAN-0009 4.8).</summary>
    public static class MapPicker
    {
        const float Step = 0.2f;

        /// <summary>The first cell whose ground the ray goes under, within <paramref name="distance"/> units.</summary>
        public static bool TryPick(MapData map, Ray ray, float distance, out int x, out int z)
        {
            x = z = -1;
            if (map == null) return false;
            bool above = false;
            for (float t = 0f; t <= distance; t += Step)
            {
                var p = ray.GetPoint(t);
                if (!map.TryGetHeight(p, out float y))
                {
                    above = false;
                    continue;
                }
                if (p.y >= y)
                {
                    above = true;
                    continue;
                }
                if (!above) continue;
                // Between the last step and this one: halve a few times.
                float lo = t - Step, hi = t;
                for (int i = 0; i < 8; i++)
                {
                    float mid = (lo + hi) * 0.5f;
                    var m = ray.GetPoint(mid);
                    if (map.TryGetHeight(m, out float my) && m.y < my) hi = mid;
                    else lo = mid;
                }
                return map.TryGetCell(ray.GetPoint(hi), out x, out z);
            }
            return false;
        }
    }
}
