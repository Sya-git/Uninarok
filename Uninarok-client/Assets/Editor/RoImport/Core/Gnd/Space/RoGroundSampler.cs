using UnityEngine;

namespace Uninarok.RoImport.Core.Gnd
{
    /// <summary>
    /// The height of the ground surface at a point (PLAN-0005 4.4): the top quad of the cube under the point, split by
    /// the diagonal of <see cref="RoGndRules"/>, as the mesh builder draws it. Used by the GAT check of SPEC-0005 FR-3 and
    /// by the ground contact check of PLAN-0006. Pure: works on <see cref="RoGnd"/> in Ragnarok space.
    /// </summary>
    public static class RoGroundSampler
    {
        /// <summary>
        /// The ground height (Ragnarok units, unconverted, Y down) at the Ragnarok map-space position's x and z; false
        /// outside the map or over a cube without a top (a gap).
        /// </summary>
        public static bool HeightAt(RoGnd gnd, RoGndRules rules, Vector3 roPosition, out float height)
        {
            height = 0f;
            if (gnd == null || !(gnd.Zoom > 0f)) return false;
            float fc = roPosition.x / gnd.Zoom;
            float fr = roPosition.z / (gnd.Zoom * RoMapSpace.RowDirection);
            if (!(fc >= 0f && fr >= 0f && fc <= gnd.Width && fr <= gnd.Height)) return false;

            // the far edge of the map belongs to the last cube
            int column = Mathf.Min((int)fc, gnd.Width - 1), row = Mathf.Min((int)fr, gnd.Height - 1);
            var cube = gnd.CubeAt(column, row);
            if (cube.TileUp < 0) return false;

            float u = fc - column, v = fr - row;
            if (rules.Diagonal == RoGndDiagonal.H10ToH01)
            {
                height = u + v <= 1f
                    ? cube.H00 + u * (cube.H10 - cube.H00) + v * (cube.H01 - cube.H00)
                    : cube.H11 + (1f - u) * (cube.H01 - cube.H11) + (1f - v) * (cube.H10 - cube.H11);
            }
            else
            {
                height = u >= v
                    ? cube.H00 + u * (cube.H10 - cube.H00) + v * (cube.H11 - cube.H10)
                    : cube.H00 + v * (cube.H01 - cube.H00) + u * (cube.H11 - cube.H01);
            }
            return true;
        }
    }
}
