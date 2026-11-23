using System;

namespace Uninarok.World
{
    /// <summary>
    /// The server's range rule for the own character's attacks and pickups (SPEC-0011 FR-3, FR-10; PLAN-0011 4.4, H1 to H3), on
    /// server cells: the model's <c>CellX/CellY</c>.
    /// </summary>
    public static class AttackRange
    {
        /// <summary>Targets farther than this (square distance) are refused (<c>AREA_SIZE</c>).</summary>
        public const int MaxSquareDistance = 14;

        /// <summary>The item must be this close (square distance) to be picked up (<c>pc_takeitem</c>).</summary>
        public const int PickUpDistance = 2;

        /// <summary><c>distance_client</c>: the circular distance minus 0.0625, in whole cells, never below 0.</summary>
        public static int ClientDistance(int dx, int dy)
        {
            double d = Math.Sqrt((double)(dx * dx + dy * dy)) - 0.0625;
            return d < 0 ? 0 : (int)d;
        }

        /// <summary>
        /// <c>battle_check_range</c> for a player: within the range by the client distance, one cell more while the target walks;
        /// then adjacent, or not farther than 14 cells with a line of sight (D15).
        /// </summary>
        public static bool CanStrike(HerculesPathfinder paths, int ax, int ay, int tx, int ty, int range, bool targetWalks)
        {
            if (paths == null) throw new ArgumentNullException(nameof(paths));
            int dx = tx - ax, dy = ty - ay;
            if (ClientDistance(dx, dy) > Math.Max(0, range + (targetWalks ? 1 : 0))) return false;
            int square = Math.Max(Math.Abs(dx), Math.Abs(dy));
            if (square < 2) return true;
            return square <= MaxSquareDistance && paths.LineOfSight(ax, ay, tx, ty);
        }

        /// <summary><c>check_distance_bl(item, player, 2)</c>.</summary>
        public static bool CanPickUp(int ax, int ay, int ix, int iy) => Math.Max(Math.Abs(ix - ax), Math.Abs(iy - ay)) <= PickUpDistance;
    }
}
