using UnityEngine;

namespace Uninarok.RoImport.Core.Rsw
{
    // The parts of an RSW file (PLAN-0006 4.1). Values are raw: Ragnarok units, Y down, angles in degrees, positions
    // relative to the map centre. No rule of PLAN-0006 4.3 is applied here.

    /// <summary>The water plane: level in Ragnarok units, type, and wave height, speed and pitch.</summary>
    public readonly struct RoRswWater
    {
        public readonly float Level;
        public readonly int Type;
        public readonly float WaveHeight, WaveSpeed, WavePitch;

        public RoRswWater(float level, int type, float waveHeight, float waveSpeed, float wavePitch)
        {
            Level = level;
            Type = type;
            WaveHeight = waveHeight;
            WaveSpeed = waveSpeed;
            WavePitch = wavePitch;
        }
    }

    /// <summary>
    /// The map's global light: longitude and latitude in degrees (latitude from the zenith), diffuse and ambient colours
    /// as 0 to 1 floats, and the shadow opacity (GRF's <c>Intensity</c>).
    /// </summary>
    public readonly struct RoRswLight
    {
        public readonly int Longitude, Latitude;
        public readonly Color Diffuse, Ambient;
        public readonly float ShadowOpacity;

        public RoRswLight(int longitude, int latitude, Color diffuse, Color ambient, float shadowOpacity)
        {
            Longitude = longitude;
            Latitude = latitude;
            Diffuse = diffuse;
            Ambient = ambient;
            ShadowOpacity = shadowOpacity;
        }
    }

    /// <summary>
    /// A placed model. <see cref="File"/> is the path under <c>model/</c> as stored (backslashes); <see cref="Node"/> is
    /// ignored (PLAN-0006 4.3); <see cref="Collision"/> is read but not used (uninitialized memory on 2.1, T0.2).
    /// </summary>
    public readonly struct RoRswModel
    {
        public readonly string Name;
        public readonly int AnimType;
        public readonly float AnimSpeed;
        public readonly int Collision;
        public readonly string File, Node;
        public readonly Vector3 Position, Rotation, Scale;

        public RoRswModel(string name, int animType, float animSpeed, int collision, string file, string node,
            Vector3 position, Vector3 rotation, Vector3 scale)
        {
            Name = name;
            AnimType = animType;
            AnimSpeed = animSpeed;
            Collision = collision;
            File = file;
            Node = node;
            Position = position;
            Rotation = rotation;
            Scale = scale;
        }
    }

    /// <summary>A point light: colour as 0 to 1 floats and range in Ragnarok units.</summary>
    public readonly struct RoRswPointLight
    {
        public readonly string Name;
        public readonly Vector3 Position;
        public readonly Color Color;
        public readonly float Range;

        public RoRswPointLight(string name, Vector3 position, Color color, float range)
        {
            Name = name;
            Position = position;
            Color = color;
            Range = range;
        }
    }

    /// <summary>A sound source: wave file, volume, area (width and height), range and repeat interval in seconds.</summary>
    public readonly struct RoRswSound
    {
        public readonly string Name, File;
        public readonly Vector3 Position;
        public readonly float Volume, Range, Interval;
        public readonly int Width, Height;

        public RoRswSound(string name, string file, Vector3 position, float volume, int width, int height, float range, float interval)
        {
            Name = name;
            File = file;
            Position = position;
            Volume = volume;
            Width = width;
            Height = height;
            Range = range;
            Interval = interval;
        }
    }

    /// <summary>An effect emitter: preset id, emission delay and the four launch parameters.</summary>
    public readonly struct RoRswEffect
    {
        public readonly string Name;
        public readonly Vector3 Position;
        public readonly int Preset;
        public readonly float Delay;
        public readonly float A, B, C, D;

        public RoRswEffect(string name, Vector3 position, int preset, float delay, float a, float b, float c, float d)
        {
            Name = name;
            Position = position;
            Preset = preset;
            Delay = delay;
            A = a;
            B = b;
            C = c;
            D = d;
        }
    }
}
