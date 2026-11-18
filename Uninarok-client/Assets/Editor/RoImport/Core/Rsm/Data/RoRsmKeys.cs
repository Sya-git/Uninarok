using UnityEngine;

namespace Uninarok.RoImport.Core.Rsm
{
    /// <summary>A position keyframe (plan 4.1). <see cref="Time"/> is in the file's units (milliseconds for 1.x, plan 4.8).</summary>
    public struct RoRsmPosKey
    {
        public int Time;
        public Vector3 Position;

        public RoRsmPosKey(int time, Vector3 position)
        {
            Time = time;
            Position = position;
        }
    }

    /// <summary>A rotation keyframe (plan 4.1): a quaternion <c>(x, y, z, w)</c> in Ragnarok space, as stored.</summary>
    public struct RoRsmRotKey
    {
        public int Time;
        public Quaternion Rotation;

        public RoRsmRotKey(int time, Quaternion rotation)
        {
            Time = time;
            Rotation = rotation;
        }
    }

    /// <summary>A scale keyframe (plan 4.1). Only versions 2.x have them.</summary>
    public struct RoRsmScaleKey
    {
        public int Time;
        public Vector3 Scale;

        public RoRsmScaleKey(int time, Vector3 scale)
        {
            Time = time;
            Scale = scale;
        }
    }
}
