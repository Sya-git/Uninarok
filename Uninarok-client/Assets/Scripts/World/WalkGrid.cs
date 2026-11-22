using System;
using Uninarok.Map;

namespace Uninarok.World
{
    /// <summary>The cells walks are found on (PLAN-0010 4.3): server cells, y growing north.</summary>
    public interface IWalkGrid
    {
        int Width { get; }
        int Height { get; }

        /// <summary><c>CELL_CHKNOPASS</c> negated; false outside.</summary>
        bool IsPassable(int x, int y);
    }

    /// <summary>The cells of a converted map (PLAN-0010 4.3): the server's (x, y) is the map data's (x, z) with z = y.</summary>
    public sealed class MapDataWalkGrid : IWalkGrid
    {
        readonly MapData _data;

        public MapDataWalkGrid(MapData data) => _data = data != null ? data : throw new ArgumentNullException(nameof(data));

        public MapData Data => _data;
        public int Width => _data.CellsX;
        public int Height => _data.CellsZ;
        public bool IsPassable(int x, int y) => _data.IsWalkable(x, y);
    }

    /// <summary>The cells of a map the client has no data of (PLAN-0010 4.7): nothing is known, so nothing is passable.</summary>
    public sealed class UnknownWalkGrid : IWalkGrid
    {
        public int Width => 0;
        public int Height => 0;
        public bool IsPassable(int x, int y) => false;
    }

    /// <summary>The server's step times (PLAN-0010 4.3): integers, as <c>unit.cpp</c>.</summary>
    public static class WalkTiming
    {
        /// <summary>Milliseconds of one step at a walk speed (ms per straight cell); a diagonal one takes 14/10 of it.</summary>
        public static int StepMs(int speed, bool diagonal) => diagonal ? speed * 14 / 10 : speed;

        /// <summary>Odd directions are diagonal (<c>walk_choices</c>).</summary>
        public static bool IsDiagonal(int direction) => (direction & 1) != 0;
    }
}
