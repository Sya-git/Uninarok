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
        const int MoveCost = 10;
        const int MoveDiagonalCost = 14;
        const int Nodes = WalkPath.MaxSteps * WalkPath.MaxSteps;

        const int North = 1, West = 2, South = 4, East = 8;

        struct Node
        {
            public short X, Y;
            public short Parent; // index, −1 for the start
            public short G, F;
            public bool Closed;
        }

        readonly IWalkGrid _grid;
        readonly Node[] _nodes = new Node[Nodes];
        // Indices into the table; a node is in the heap at most once.
        readonly short[] _heap = new short[Nodes + 1];
        int _heapLength;

        public HerculesPathfinder(IWalkGrid grid) => _grid = grid ?? throw new ArgumentNullException(nameof(grid));

        public IWalkGrid Grid => _grid;

        // map_getcellp(CELL_CHKNOPASS): the last row and column never pass.
        bool NoPass(int x, int y) => x < 0 || y < 0 || x >= _grid.Width - 1 || y >= _grid.Height - 1 || !_grid.IsPassable(x, y);

        static int Index(int x, int y) => (x + y * WalkPath.MaxSteps) & (Nodes - 1);

        // Manhattan, overestimating, as the game client's.
        static int Heuristic(int x0, int y0, int x1, int y1) => MoveCost * (Math.Abs(x1 - x0) + Math.Abs(y1 - y0));

        // walk_choices[-dy + 1][dx + 1]
        static byte Direction(int dx, int dy)
        {
            if (dy > 0) return (byte)(dx < 0 ? 1 : dx > 0 ? 7 : 0);
            if (dy < 0) return (byte)(dx < 0 ? 3 : dx > 0 ? 5 : 4);
            return (byte)(dx < 0 ? 2 : 6);
        }

        /// <summary>The cell offset of a direction (<c>dirx</c>).</summary>
        public static int Dx(int direction) => direction == 0 || direction == 4 ? 0 : direction < 4 ? -1 : 1;

        /// <summary>The cell offset of a direction (<c>diry</c>), north positive.</summary>
        public static int Dy(int direction) => direction == 2 || direction == 6 ? 0 : direction > 2 && direction < 6 ? -1 : 1;

        // The checks path_search starts with; null when the search goes on.
        PathResult? Start(int x0, int y0, int x1, int y1, ref WalkPath path)
        {
            path.Length = 0;
            path.StartX = x0;
            path.StartY = y0;
            // The start cell itself is not checked.
            if (x0 < 0 || x0 >= _grid.Width || y0 < 0 || y0 >= _grid.Height) return PathResult.NoPath;
            if (NoPass(x1, y1)) return PathResult.Blocked;
            if (x0 == x1 && y0 == y1) return PathResult.SameCell;
            return null;
        }

        /// <summary><c>path_search</c> with flag 0: A* with the overestimating heuristic.</summary>
        public PathResult Search(int x0, int y0, int x1, int y1, ref WalkPath path)
        {
            var start = Start(x0, y0, x1, y1, ref path);
            if (start.HasValue) return start.Value;

            Array.Clear(_nodes, 0, Nodes);
            _heapLength = 0;
            int xs = _grid.Width - 1, ys = _grid.Height - 1;

            int i = Index(x0, y0);
            _nodes[i] = new Node { X = (short)x0, Y = (short)y0, Parent = -1, F = (short)Heuristic(x0, y0, x1, y1) };
            Push((short)i);

            short current;
            for (;;)
            {
                if (_heapLength == 0) return PathResult.NoPath;
                current = Pop();

                ref var node = ref _nodes[current];
                int x = node.X, y = node.Y, g = node.G;
                node.Closed = true;
                if (x == x1 && y == y1) break;

                // A diagonal only when both directions around it are allowed.
                int allowed = 0;
                if (y < ys && !NoPass(x, y + 1)) allowed |= North;
                if (y > 0 && !NoPass(x, y - 1)) allowed |= South;
                if (x < xs && !NoPass(x + 1, y)) allowed |= East;
                if (x > 0 && !NoPass(x - 1, y)) allowed |= West;

                // The server's order; a collision in any of them aborts after all were tried.
                bool error = false;
                if ((allowed & (South | East)) == (South | East) && !NoPass(x + 1, y - 1)) error |= !Add(x + 1, y - 1, g + MoveDiagonalCost, current, x1, y1);
                if ((allowed & East) != 0) error |= !Add(x + 1, y, g + MoveCost, current, x1, y1);
                if ((allowed & (North | East)) == (North | East) && !NoPass(x + 1, y + 1)) error |= !Add(x + 1, y + 1, g + MoveDiagonalCost, current, x1, y1);
                if ((allowed & North) != 0) error |= !Add(x, y + 1, g + MoveCost, current, x1, y1);
                if ((allowed & (North | West)) == (North | West) && !NoPass(x - 1, y + 1)) error |= !Add(x - 1, y + 1, g + MoveDiagonalCost, current, x1, y1);
                if ((allowed & West) != 0) error |= !Add(x - 1, y, g + MoveCost, current, x1, y1);
                if ((allowed & (South | West)) == (South | West) && !NoPass(x - 1, y - 1)) error |= !Add(x - 1, y - 1, g + MoveDiagonalCost, current, x1, y1);
                if ((allowed & South) != 0) error |= !Add(x, y - 1, g + MoveCost, current, x1, y1);
                if (error) return PathResult.NoPath;
            }

            int length = 0;
            for (int n = current; _nodes[n].Parent >= 0; n = _nodes[n].Parent) length++;
            if (length > WalkPath.MaxSteps) return PathResult.NoPath;

            path.Length = length;
            for (int n = current, step = length - 1; step >= 0; step--)
            {
                int parent = _nodes[n].Parent;
                path.SetStep(step, Direction(_nodes[n].X - _nodes[parent].X, _nodes[n].Y - _nodes[parent].Y));
                n = parent;
            }
            return PathResult.Found;
        }

        // add_path: false when the node's place in the table is taken by another cell.
        bool Add(int x, int y, int g, short parent, int x1, int y1)
        {
            int i = Index(x, y);
            ref var node = ref _nodes[i];
            if (node.X == x && node.Y == y)
            {
                if (g < node.G)
                {
                    node.G = (short)g;
                    node.Parent = parent;
                    node.F = (short)(g + Heuristic(x, y, x1, y1));
                    if (node.Closed) Push((short)i);
                    else if (!Update((short)i)) return false;
                    node.Closed = false;
                }
                return true;
            }
            if (node.X != 0 || node.Y != 0) return false;

            node.X = (short)x;
            node.Y = (short)y;
            node.G = (short)g;
            node.Parent = parent;
            node.F = (short)(g + Heuristic(x, y, x1, y1));
            node.Closed = false;
            Push((short)i);
            return true;
        }

        // ---- the heap of common/db.h: ties keep the order these loops give ----

        // BHEAP_PUSH2
        void Push(short node)
        {
            _heap[_heapLength] = node;
            SiftDown(0, _heapLength++);
        }

        // BHEAP_PEEK and BHEAP_POP2
        short Pop()
        {
            short top = _heap[0];
            _heap[0] = _heap[--_heapLength];
            if (_heapLength > 0) SiftUp(0);
            return top;
        }

        // heap_update_node, BHEAP_UPDATE
        bool Update(short node)
        {
            for (int i = 0; i < _heapLength; i++)
            {
                if (_heap[i] != node) continue;
                SiftDown(0, i);
                SiftUp(i);
                return true;
            }
            return false;
        }

        // BHEAP_SIFTDOWN: towards the root
        void SiftDown(int from, int index)
        {
            while (index > from)
            {
                int parent = (index - 1) / 2;
                if (_nodes[_heap[parent]].F <= _nodes[_heap[index]].F) break;
                Swap(parent, index);
                index = parent;
            }
        }

        // BHEAP_SIFTUP: to the leaves, then back up to its place
        void SiftUp(int index)
        {
            int i = index;
            for (int left = i * 2 + 1; left < _heapLength; left = i * 2 + 1)
            {
                int right = i * 2 + 2;
                int child = right >= _heapLength || _nodes[_heap[left]].F < _nodes[_heap[right]].F ? left : right;
                Swap(i, child);
                i = child;
            }
            SiftDown(index, i);
        }

        void Swap(int a, int b)
        {
            short t = _heap[a];
            _heap[a] = _heap[b];
            _heap[b] = t;
        }

        // ---- the other searches ----

        /// <summary><c>path_search</c> with flag 1: diagonally first, then straight; the first blocked cell is the end of it.</summary>
        public PathResult SearchEasy(int x0, int y0, int x1, int y1, ref WalkPath path)
        {
            var start = Start(x0, y0, x1, y1, ref path);
            if (start.HasValue) return start.Value;

            int dx = Math.Sign(x1 - x0), dy = Math.Sign(y1 - y0);
            int x = x0, y = y0, length = 0;
            while (length < WalkPath.MaxSteps)
            {
                path.SetStep(length++, Direction(dx, dy));
                x += dx;
                y += dy;
                if (x == x1) dx = 0;
                if (y == y1) dy = 0;
                if (dx == 0 && dy == 0) break;
                if (NoPass(x, y)) break;
            }
            if (x != x1 || y != y1) return PathResult.NoPath;
            path.Length = length;
            return PathResult.Found;
        }

        /// <summary><c>path_search_long</c> with <c>CELL_CHKNOPASS</c>: every cell of the straight line passes, the last included.</summary>
        public bool StraightLine(int x0, int y0, int x1, int y1)
        {
            int dx = x1 - x0;
            if (dx < 0)
            {
                (x0, x1) = (x1, x0);
                (y0, y1) = (y1, y0);
                dx = -dx;
            }
            int dy = y1 - y0;
            int weight = dx > Math.Abs(dy) ? dx : Math.Abs(y1 - y0);
            int wx = 0, wy = 0;
            while (x0 != x1 || y0 != y1)
            {
                wx += dx;
                wy += dy;
                if (wx >= weight)
                {
                    wx -= weight;
                    x0++;
                }
                if (wy >= weight)
                {
                    wy -= weight;
                    y0++;
                }
                else if (wy < 0)
                {
                    wy += weight;
                    y0--;
                }
                if (NoPass(x0, y0)) return false;
            }
            return true;
        }

        /// <summary>
        /// The path of another unit's walk, from the two ends its packet carries (PLAN-0010 D11, measured in T3.8): A*, also for units
        /// the server walks with its easy path (a chasing monster), which are right more often than not and at most a cell off; the
        /// easy path when A* finds none.
        /// </summary>
        public PathResult SearchForUnit(EntityKind kind, int x0, int y0, int x1, int y1, ref WalkPath path)
        {
            var result = Search(x0, y0, x1, y1, ref path);
            if (result == PathResult.NoPath && kind != EntityKind.Player && kind != EntityKind.Npc) result = SearchEasy(x0, y0, x1, y1, ref path);
            return result;
        }

        /// <summary>
        /// <see cref="Search"/> with the limits of <c>unit_walktoxy</c> (SPEC-0010 FR-9): what the server does with a walk request
        /// to this cell. On <see cref="PathResult.TooLong"/> the path is the one that is too long.
        /// </summary>
        public PathResult SearchWalk(int x0, int y0, int x1, int y1, ref WalkPath path, in WalkRules rules)
        {
            var result = Search(x0, y0, x1, y1, ref path);
            if (result != PathResult.Found) return result;
            if (path.Length > rules.MaxWalkPath) return PathResult.TooLong;
            if (path.Length > rules.MaxWalkPathObstructed && !StraightLine(x0, y0, x1, y1)) return PathResult.TooLong;
            return PathResult.Found;
        }
    }
}
