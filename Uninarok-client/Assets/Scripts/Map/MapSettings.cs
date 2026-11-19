using System;
using UnityEngine;

namespace Uninarok.Map
{
    /// <summary>
    /// Map-wide settings from the RSW and the GND (SPEC-0006 FR-7, PLAN-0006 4.5): size, water, global lighting and the
    /// source versions. Filled by the importer; read-only at runtime. Lengths are in Unity units, angles in degrees,
    /// colours as stored in the file (0 to 1). No music: out of scope (PLAN-0006 D12).
    /// </summary>
    [Serializable]
    public sealed class MapSettings
    {
        [SerializeField] int widthCubes, heightCubes;
        [SerializeField] float zoom;
        [SerializeField] float waterLevel;
        [SerializeField] int waterType;
        [SerializeField] float waveHeight, waveSpeed, wavePitch;
        [SerializeField] Color ambient, sunColor;
        [SerializeField] int longitude, latitude;
        [SerializeField] float shadowOpacity;
        [SerializeField] string gndVersion = string.Empty, gatVersion = string.Empty, rswVersion = string.Empty;

        /// <summary>Ground size in cubes (GND).</summary>
        public int WidthCubes => widthCubes;
        public int HeightCubes => heightCubes;
        /// <summary>Ragnarok units per cube (GND).</summary>
        public float Zoom => zoom;
        /// <summary>World height of the water plane in Unity units (Y up).</summary>
        public float WaterLevel => waterLevel;
        public int WaterType => waterType;
        /// <summary>Wave height in Unity units; speed and pitch as stored.</summary>
        public float WaveHeight => waveHeight;
        public float WaveSpeed => waveSpeed;
        public float WavePitch => wavePitch;
        public Color Ambient => ambient;
        public Color SunColor => sunColor;
        /// <summary>Sun angles in degrees; latitude is measured from the zenith.</summary>
        public int Longitude => longitude;
        public int Latitude => latitude;
        public float ShadowOpacity => shadowOpacity;
        /// <summary>Source versions as "major.minor".</summary>
        public string GndVersion => gndVersion;
        public string GatVersion => gatVersion;
        public string RswVersion => rswVersion;

        /// <summary>Fills the settings (the importer and tests).</summary>
        internal void Assign(int width, int height, float cubeZoom, float level, int type, float wave, float speed, float pitch,
            Color ambientColor, Color sun, int lon, int lat, float opacity, string gnd, string gat, string rsw)
        {
            widthCubes = width;
            heightCubes = height;
            zoom = cubeZoom;
            waterLevel = level;
            waterType = type;
            waveHeight = wave;
            waveSpeed = speed;
            wavePitch = pitch;
            ambient = ambientColor;
            sunColor = sun;
            longitude = lon;
            latitude = lat;
            shadowOpacity = opacity;
            gndVersion = gnd ?? string.Empty;
            gatVersion = gat ?? string.Empty;
            rswVersion = rsw ?? string.Empty;
        }
    }
}
