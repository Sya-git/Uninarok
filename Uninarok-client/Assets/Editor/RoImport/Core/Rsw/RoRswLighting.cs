using UnityEngine;
using Uninarok.RoImport.Core.Space;

namespace Uninarok.RoImport.Core.Rsw
{
    /// <summary>
    /// The scene light from the RSW's global light (PLAN-0006 4.4, SPEC-0006 FR-8, verified by TASKS-0006 T0.3): flat
    /// ambient colour and one directional sun, colours as stored with no factor. Pure.
    /// </summary>
    public static class RoRswLighting
    {
        /// <summary>The flat ambient colour (<c>RenderSettings.ambientLight</c>).</summary>
        public static Color Ambient(in RoRswLight l) => l.Ambient;

        /// <summary>The sun's colour, with intensity 1.</summary>
        public static Color Sun(in RoRswLight l) => l.Diffuse;

        /// <summary>
        /// The sun's rotation: latitude is measured from the zenith, so the direction toward the light in Ragnarok space
        /// is <c>(-cos lon · sin lat, -cos lat, sin lon · sin lat)</c>; the sun points the other way, converted with
        /// <see cref="RoSpace.Direction"/>.
        /// </summary>
        public static Quaternion SunRotation(in RoRswLight l)
        {
            float lon = l.Longitude * Mathf.Deg2Rad, lat = l.Latitude * Mathf.Deg2Rad;
            var towardLightRo = new Vector3(-Mathf.Cos(lon) * Mathf.Sin(lat), -Mathf.Cos(lat), Mathf.Sin(lon) * Mathf.Sin(lat));
            var forward = -RoSpace.Direction(towardLightRo);
            var up = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.999f ? Vector3.forward : Vector3.up;
            return Quaternion.LookRotation(forward, up);
        }

        /// <summary>The sun's shadow strength: one minus the file's shadow opacity, clamped to 0 to 1.</summary>
        public static float ShadowStrength(in RoRswLight l) => Mathf.Clamp01(1f - l.ShadowOpacity);
    }
}
