using UnityEngine;
using Uninarok.Networking.Data;

namespace Uninarok.Units
{
    /// <summary>
    /// World facing and camera turn to the sprite's action direction (PLAN-0009 4.3, D15; SPEC-0009 FR-6). Pure. The sprite
    /// counts clockwise from the front, the network counter-clockwise from north (docs/data/UninarokPosition.md).
    /// </summary>
    public static class UnitFacing
    {
        public const int Directions = 8;
        /// <summary>The network direction that shows the front at the default camera: South.</summary>
        public const int FrontOffset = 4;

        /// <summary>
        /// The camera turn (Unity yaw in degrees, any value; 0 looks north, 90 east) as a sector 0..7 whose borders lie 22.5°
        /// either side of each direction.
        /// </summary>
        public static int CameraSector(float yawDegrees) => Mathf.FloorToInt((yawDegrees + 22.5f) / 45f) & (Directions - 1);

        /// <summary>The action direction to show: 0 faces the viewer, then clockwise.</summary>
        public static int Shown(UninarokDirection world, int cameraSector) => (FrontOffset - (int)world - cameraSector) & (Directions - 1);
    }
}
