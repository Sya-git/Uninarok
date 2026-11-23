using NUnit.Framework;

namespace Uninarok.World.Tests
{
    // SPEC-0011 FR-3, FR-10 (PLAN-0011 4.4, H1 to H3, D15): the server's range rule on server cells.
    public class AttackRangeTests
    {
        // '.' walkable, '#' wall, '~' cliff (attacks pass, nobody walks); the first line is the north row.
        sealed class TextGrid : IWalkGrid
        {
            readonly string[] _rows;
            public TextGrid(params string[] rows) => _rows = rows;
            public int Width => _rows[0].Length;
            public int Height => _rows.Length;
            char At(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height ? _rows[Height - 1 - y][x] : '#';
            public bool IsPassable(int x, int y) => At(x, y) == '.';
            public bool BlocksSight(int x, int y) => At(x, y) == '#';
        }

        static HerculesPathfinder Open(int size = 40) => new HerculesPathfinder(new TextGrid(Rows(size, '.')));

        static string[] Rows(int size, char c)
        {
            var rows = new string[size];
            for (int i = 0; i < size; i++) rows[i] = new string(c, size);
            return rows;
        }

        [Test]
        public void AttackRange_ClientDistance_Table()
        {
            // (int)max(0, sqrt(dx² + dy²) − 0.0625), as path.cpp distance_client
            var rows = new (int dx, int dy, int expected)[]
            {
                (0, 0, 0), (1, 0, 0), (0, -1, 0), (1, 1, 1), (2, 0, 1), (-2, 1, 2), (2, 2, 2), (3, 0, 2), (3, 4, 4), (5, 0, 4),
                (9, 0, 8), (10, 0, 9), (6, 7, 9), (-14, 14, 19),
            };
            foreach (var r in rows) Assert.AreEqual(r.expected, AttackRange.ClientDistance(r.dx, r.dy), $"{r.dx},{r.dy}");
        }

        [Test]
        public void AttackRange_RangeOneAndNine()
        {
            var paths = Open();
            Assert.IsTrue(AttackRange.CanStrike(paths, 10, 10, 11, 11, 1, false), "diagonal neighbour");
            Assert.IsTrue(AttackRange.CanStrike(paths, 10, 10, 12, 10, 1, false), "two cells straight are 1 by the client distance");
            Assert.IsFalse(AttackRange.CanStrike(paths, 10, 10, 12, 11, 1, false));
            Assert.IsTrue(AttackRange.CanStrike(paths, 10, 10, 20, 10, 9, false), "a bow user at range 9 reaches 10 cells straight");
            Assert.IsFalse(AttackRange.CanStrike(paths, 10, 10, 21, 10, 9, false));
            Assert.IsFalse(AttackRange.CanStrike(paths, 10, 10, 10, 10 + 15, 30, false), "more than 14 cells is never in range");
        }

        [Test]
        public void AttackRange_WalkingTargetOneMore()
        {
            var paths = Open();
            Assert.IsFalse(AttackRange.CanStrike(paths, 10, 10, 13, 10, 1, false));
            Assert.IsTrue(AttackRange.CanStrike(paths, 10, 10, 13, 10, 1, true));
        }

        [Test]
        public void AttackRange_WallBlocks_CliffDoesNot()
        {
            var paths = new HerculesPathfinder(new TextGrid(
                "...#......",
                "....#.....",
                "..........",
                "....~.....",
                ".........."));
            // y counts from the south row: the wall is on row 3, the cliff on row 1.
            Assert.IsFalse(AttackRange.CanStrike(paths, 2, 3, 6, 3, 9, false), "a wall between");
            Assert.IsTrue(AttackRange.CanStrike(paths, 2, 1, 6, 1, 9, false), "a cliff between");
            Assert.IsTrue(AttackRange.CanStrike(paths, 3, 3, 4, 3, 1, false), "next to the wall: no sight check");
            Assert.IsTrue(paths.LineOfSight(0, 4, 8, 4), "the wall on the last row does not block, as on the server");
            Assert.IsFalse(paths.LineOfSight(0, 3, 8, 3), "the same wall one row lower blocks");
            Assert.IsFalse(new HerculesPathfinder(new UnknownWalkGrid()).LineOfSight(0, 0, 3, 0), "no map data, no sight");
        }

        [Test]
        public void AttackRange_CanPickUp()
        {
            Assert.IsTrue(AttackRange.CanPickUp(10, 10, 12, 12));
            Assert.IsTrue(AttackRange.CanPickUp(10, 10, 8, 10));
            Assert.IsFalse(AttackRange.CanPickUp(10, 10, 13, 10));
            Assert.IsFalse(AttackRange.CanPickUp(10, 10, 11, 7));
        }
    }
}
