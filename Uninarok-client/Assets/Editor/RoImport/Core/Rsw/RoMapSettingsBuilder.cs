using UnityEngine;
using Uninarok.Map;
using Uninarok.RoImport.Core.Space;

namespace Uninarok.RoImport.Core.Rsw
{
    /// <summary>
    /// Builds the map-wide <see cref="MapSettings"/> from an RSW and its ground (PLAN-0006 4.5, SPEC-0006 FR-7). Pure.
    /// Heights and lengths go through <see cref="RoSpace"/> (Y flipped, 10 Ragnarok units per Unity unit); angles, colours
    /// and the shadow opacity are kept as stored (PLAN-0006 4.4).
    /// </summary>
    public static class RoMapSettingsBuilder
    {
        public static MapSettings Build(RoRsw rsw, int widthCubes, int heightCubes, float zoom, string gndVersion, string gatVersion)
        {
            var settings = new MapSettings();
            var water = rsw.Water;
            var light = rsw.Light;
            settings.Assign(widthCubes, heightCubes, zoom,
                RoSpace.Point(new Vector3(0f, water.Level, 0f)).y,
                water.Type,
                water.WaveHeight * RoUnits.Scale,
                water.WaveSpeed,
                water.WavePitch,
                light.Ambient,
                light.Diffuse,
                light.Longitude,
                light.Latitude,
                light.ShadowOpacity,
                gndVersion,
                gatVersion,
                $"{rsw.VersionMajor}.{rsw.VersionMinor}");
            return settings;
        }
    }
}
