using System;

namespace Uninarok.World
{
    /// <summary>What a path search found (PLAN-0010 4.4).</summary>
    public enum PathResult : byte
    {
        Found,
        /// <summary>Start and end are the same cell: an empty path.</summary>
        SameCell,
        /// <summary>The destination is not passable.</summary>
        Blocked,
        NoPath,
        /// <summary>A path exists, longer than the server walks.</summary>
        TooLong,
    }

    /// <summary>The steps of a walk in fixed storage (PLAN-0010 4.4): no allocation.</summary>
    public struct WalkPath
    {
        /// <summary><c>MAX_WALKPATH</c>.</summary>
        public const int MaxSteps = 32;

        public int Length;
        public int StartX, StartY;

        ulong _low, _high; // four bits per step

        /// <summary>The direction of step <paramref name="i"/>, 0..7 (<c>UninarokDirection</c> values).</summary>
        public readonly byte Step(int i) => (byte)(i < 16 ? (_low >> (i * 4)) & 0xF : (_high >> ((i - 16) * 4)) & 0xF);

        internal void SetStep(int i, byte direction)
        {
            if (i < 16) _low = (_low & ~(0xFUL << (i * 4))) | ((ulong)direction << (i * 4));
            else _high = (_high & ~(0xFUL << ((i - 16) * 4))) | ((ulong)direction << ((i - 16) * 4));
        }
    }

    /// <summary>
    /// The server's path search, ported line by line from Hercules <c>src/map/path.cpp</c> (SPEC-0010 FR-10; PLAN-0010 4.4, D3):
    /// the same 32 × 32 node table with the same collisions, the same binary heap, the same neighbour order. It owns its table and
    /// heap: a search allocates nothing. Main thread, one search at a time.
    /// </summary>
    public sealed class HerculesPathfinder
    {
        public HerculesPathfinder(IWalkGrid grid) { }

        public PathResult Search(int x0, int y0, int x1, int y1, ref WalkPath path) => throw new NotImplementedException();
        public PathResult SearchEasy(int x0, int y0, int x1, int y1, ref WalkPath path) => throw new NotImplementedException();
        public bool StraightLine(int x0, int y0, int x1, int y1) => throw new NotImplementedException();
        public PathResult SearchWalk(int x0, int y0, int x1, int y1, ref WalkPath path, in WalkRules rules) => throw new NotImplementedException();
    }
}
