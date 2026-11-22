using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;

namespace Uninarok.World.Tests
{
    // SPEC-0010 FR-9, FR-10, FR-17 (PLAN-0010 4.4, D3): the client's paths are the server's, step by step.
    public class HerculesPathfinderTests
    {
        internal static string FixturePath(string file) =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "../../docs/specs/0010-world-session/fixtures", file));

        // ---- the reference paths of uninarok_pathdump (fixtures README) ----

        struct Pair
        {
            public int X0, Y0, X1, Y1;
            public bool Passable, Found, FoundEasy, Straight, Accepted;
            public byte[] Steps, EasySteps;

            public override string ToString() => $"{X0},{Y0} to {X1},{Y1}";
        }

        static (IWalkGrid Grid, List<Pair> Pairs) Load(string map)
        {
            string file = FixturePath($"paths-{map}.bin");
            if (!File.Exists(file)) Assert.Ignore("no fixture " + file);
            var grid = new MapDataWalkGrid(WalkGridTests.LoadMap(map));
            using var r = new BinaryReader(File.OpenRead(file));
            Assert.AreEqual("UNPF", System.Text.Encoding.ASCII.GetString(r.ReadBytes(4)));
            Assert.AreEqual(1u, r.ReadUInt32());
            Assert.AreEqual(map, System.Text.Encoding.ASCII.GetString(r.ReadBytes(16)).TrimEnd('\0'));
            Assert.AreEqual((grid.Width, grid.Height), ((int)r.ReadUInt16(), (int)r.ReadUInt16()));
            r.ReadUInt32();
            int count = (int)r.ReadUInt32();
            Assert.AreEqual(Hash(grid), r.ReadUInt32(), "the client's cells are the server's");
            var pairs = new List<Pair>(count);
            for (int i = 0; i < count; i++)
            {
                var p = new Pair { X0 = r.ReadUInt16(), Y0 = r.ReadUInt16(), X1 = r.ReadUInt16(), Y1 = r.ReadUInt16() };
                int flags = r.ReadByte(), length = r.ReadByte(), easy = r.ReadByte();
                p.Passable = (flags & 1) != 0;
                p.Found = (flags & 2) != 0;
                p.FoundEasy = (flags & 4) != 0;
                p.Straight = (flags & 8) != 0;
                p.Accepted = (flags & 16) != 0;
                p.Steps = r.ReadBytes(length);
                p.EasySteps = r.ReadBytes(easy);
                pairs.Add(p);
            }
            return (grid, pairs);
        }

        // As the plugin: one byte per cell through CELL_CHKNOPASS, which never passes the last row and column.
        static uint Hash(IWalkGrid grid)
        {
            uint hash = 2166136261u;
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                {
                    bool pass = x < grid.Width - 1 && y < grid.Height - 1 && grid.IsPassable(x, y);
                    hash = unchecked((hash ^ (pass ? 1u : 0u)) * 16777619u);
                }
            return hash;
        }

        static string Steps(in WalkPath path)
        {
            var s = new char[path.Length];
            for (int i = 0; i < s.Length; i++) s[i] = (char)('0' + path.Step(i));
            return new string(s);
        }

        static string Steps(byte[] steps)
        {
            var s = new char[steps.Length];
            for (int i = 0; i < s.Length; i++) s[i] = (char)('0' + steps[i]);
            return new string(s);
        }

        static readonly string[] Maps = { "prontera", "izlude", "prt_fild08" };

        [Test, Category("NeedsData"), Ignore("pending T3.6")]
        public void HerculesPathfinder_MatchesServer([ValueSource(nameof(Maps))] string map)
        {
            var (grid, pairs) = Load(map);
            var finder = new HerculesPathfinder(grid);
            var path = new WalkPath();
            int different = 0;
            string first = null;
            foreach (var p in pairs)
            {
                var result = finder.Search(p.X0, p.Y0, p.X1, p.Y1, ref path);
                var expected = !p.Passable ? PathResult.Blocked
                    : !p.Found ? PathResult.NoPath
                    : p.X0 == p.X1 && p.Y0 == p.Y1 ? PathResult.SameCell : PathResult.Found;
                string steps = p.Found ? Steps(p.Steps) : string.Empty;
                if (result == expected && (!p.Found || (Steps(path) == steps && path.StartX == p.X0 && path.StartY == p.Y0))) continue;
                different++;
                first ??= $"{p}: {result} {(p.Found ? Steps(path) : "")}, the server {expected} {steps}";
            }
            Assert.AreEqual(10_000, pairs.Count);
            Assert.AreEqual(0, different, first);
        }

        [Test, Category("NeedsData"), Ignore("pending T3.7")]
        public void HerculesPathfinder_EasyPath_MatchesServer([ValueSource(nameof(Maps))] string map)
        {
            var (grid, pairs) = Load(map);
            var finder = new HerculesPathfinder(grid);
            var path = new WalkPath();
            int different = 0;
            string first = null;
            foreach (var p in pairs)
            {
                var result = finder.SearchEasy(p.X0, p.Y0, p.X1, p.Y1, ref path);
                bool found = result == PathResult.Found || result == PathResult.SameCell;
                if (found == p.FoundEasy && (!found || Steps(path) == Steps(p.EasySteps))) continue;
                different++;
                first ??= $"{p}: {result} {Steps(path)}, the server {p.FoundEasy} {Steps(p.EasySteps)}";
            }
            Assert.AreEqual(0, different, first);
        }

        [Test, Category("NeedsData"), Ignore("pending T3.7")]
        public void HerculesPathfinder_StraightLineAndWalkLimits_MatchServer([ValueSource(nameof(Maps))] string map)
        {
            var (grid, pairs) = Load(map);
            var finder = new HerculesPathfinder(grid);
            var path = new WalkPath();
            var rules = new WalkRules(17, 14);
            int different = 0;
            string first = null;
            foreach (var p in pairs)
            {
                bool straight = finder.StraightLine(p.X0, p.Y0, p.X1, p.Y1);
                var result = finder.SearchWalk(p.X0, p.Y0, p.X1, p.Y1, ref path, rules);
                bool accepted = result == PathResult.Found || result == PathResult.SameCell;
                bool tooLong = result == PathResult.TooLong;
                if (straight == p.Straight && accepted == p.Accepted && tooLong == (p.Found && !p.Accepted)) continue;
                different++;
                first ??= $"{p}: straight {straight}, {result}; the server straight {p.Straight}, found {p.Found}, accepted {p.Accepted}";
            }
            Assert.AreEqual(0, different, first);
        }

        // ---- synthetic maps: '.' passes, '#' does not; the first line is the north row ----

        sealed class TextGrid : IWalkGrid
        {
            readonly string[] _rows;

            public TextGrid(params string[] rows) => _rows = rows;

            public int Width => _rows[0].Length;
            public int Height => _rows.Length;
            public bool IsPassable(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height && _rows[Height - 1 - y][x] == '.';
        }

        sealed class OpenGrid : IWalkGrid
        {
            public int Width { get; set; } = 200;
            public int Height { get; set; } = 200;
            public Func<int, int, bool> Wall = (x, y) => false;
            public bool IsPassable(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height && !Wall(x, y);
        }

        [Test, Ignore("pending T3.6")]
        public void HerculesPathfinder_CornerRule()
        {
            // the diagonal from (1,1) to (2,2) would cut the corner of the wall at (2,1)
            var finder = new HerculesPathfinder(new TextGrid(
                ".....",
                ".....",
                ".....",
                "..#..",
                "....."));
            var path = new WalkPath();
            Assert.AreEqual(PathResult.Found, finder.Search(1, 1, 2, 2, ref path));
            Assert.AreEqual("06", Steps(path), "north, then east");
            Assert.AreEqual(PathResult.Found, finder.Search(1, 1, 0, 2, ref path));
            Assert.AreEqual("1", Steps(path), "north-west, free");
        }

        [Test, Ignore("pending T3.6")]
        public void HerculesPathfinder_SameCell()
        {
            var finder = new HerculesPathfinder(new OpenGrid());
            var path = new WalkPath { Length = 9 };
            Assert.AreEqual(PathResult.SameCell, finder.Search(10, 10, 10, 10, ref path));
            Assert.AreEqual(0, path.Length);
            Assert.AreEqual((10, 10), (path.StartX, path.StartY));
        }

        [Test, Ignore("pending T3.6")]
        public void HerculesPathfinder_BlockedDestination_AndOutside()
        {
            var finder = new HerculesPathfinder(new OpenGrid { Wall = (x, y) => x == 12 && y == 10 });
            var path = new WalkPath();
            Assert.AreEqual(PathResult.Blocked, finder.Search(10, 10, 12, 10, ref path));
            Assert.AreEqual(PathResult.Blocked, finder.Search(10, 10, -1, 10, ref path));
            Assert.AreEqual(PathResult.Blocked, finder.Search(198, 10, 199, 10, ref path), "the last column never passes");
            Assert.AreEqual(PathResult.NoPath, finder.Search(-1, 10, 5, 10, ref path), "a start outside the map");
        }

        [Test, Ignore("pending T3.6")]
        public void HerculesPathfinder_TheStartCellIsNotChecked()
        {
            var finder = new HerculesPathfinder(new OpenGrid { Wall = (x, y) => x == 10 && y == 10 });
            var path = new WalkPath();
            Assert.AreEqual(PathResult.Found, finder.Search(10, 10, 12, 10, ref path));
            Assert.AreEqual("66", Steps(path));
        }

        [Test, Ignore("pending T3.6")]
        public void HerculesPathfinder_NodeCollisionAborts()
        {
            // A wall south to north with one gap far away: the search spreads over more than 32 rows of one column group and two
            // cells ask for the same node (x + y * 32) & 1023, so the server gives up although a path of 32 steps may exist.
            var finder = new HerculesPathfinder(new OpenGrid { Wall = (x, y) => x == 52 && y != 190 });
            var path = new WalkPath();
            Assert.AreEqual(PathResult.NoPath, finder.Search(50, 100, 54, 100, ref path));
        }

        [Test, Ignore("pending T3.6")]
        public void HerculesPathfinder_LongerThan32Steps_IsNoPath()
        {
            var finder = new HerculesPathfinder(new OpenGrid { Height = 4, Wall = (x, y) => y != 1 });
            var path = new WalkPath();
            Assert.AreEqual(PathResult.NoPath, finder.Search(10, 1, 43, 1, ref path), "33 steps: a node collision or too long, never a path");
        }

        [Test, Ignore("pending T3.7")]
        public void HerculesPathfinder_Limits17And14()
        {
            var rules = new WalkRules(17, 14);
            var path = new WalkPath();
            var open = new HerculesPathfinder(new OpenGrid());
            Assert.AreEqual(PathResult.Found, open.SearchWalk(50, 50, 67, 50, ref path, rules));
            Assert.AreEqual(17, path.Length);
            Assert.AreEqual(PathResult.TooLong, open.SearchWalk(50, 50, 68, 50, ref path, rules));
            Assert.AreEqual(PathResult.Found, open.SearchWalk(50, 50, 68, 50, ref path, WalkRules.None));

            // a post on the straight line: 15 cells the long way round are refused, 14 are not
            var post = new HerculesPathfinder(new OpenGrid { Wall = (x, y) => x == 55 && y == 50 });
            Assert.IsFalse(post.StraightLine(50, 50, 65, 50));
            Assert.IsTrue(post.StraightLine(50, 50, 54, 50));
            Assert.AreEqual(PathResult.TooLong, post.SearchWalk(50, 50, 65, 50, ref path, rules));
            Assert.AreEqual(PathResult.Found, post.SearchWalk(50, 50, 64, 50, ref path, rules));
            Assert.AreEqual(14, path.Length);
            Assert.AreEqual(PathResult.Blocked, post.SearchWalk(50, 50, 55, 50, ref path, rules));
            Assert.AreEqual(PathResult.SameCell, post.SearchWalk(50, 50, 50, 50, ref path, rules));
        }

        [Test, Ignore("pending T3.7")]
        public void HerculesPathfinder_EasyPath_DiagonalFirst_AndNoDetour()
        {
            var path = new WalkPath();
            var open = new HerculesPathfinder(new OpenGrid());
            Assert.AreEqual(PathResult.Found, open.SearchEasy(50, 50, 55, 52, ref path));
            Assert.AreEqual("77666", Steps(path));
            var post = new HerculesPathfinder(new OpenGrid { Wall = (x, y) => x == 52 && y == 50 });
            Assert.AreEqual(PathResult.NoPath, post.SearchEasy(50, 50, 55, 50, ref path));
        }

        [Test, Ignore("pending T3.6")]
        public void HerculesPathfinder_ZeroAllocation1000Searches()
        {
            var finder = new HerculesPathfinder(new OpenGrid { Wall = (x, y) => x % 7 == 3 && y % 5 != 1 });
            var path = new WalkPath();
            int found = 0;
            Action run = () =>
            {
                for (int i = 0; i < 1000; i++)
                    if (finder.Search(50 + i % 11, 50 + i % 13, 60 - i % 7, 60 + i % 5, ref path) == PathResult.Found) found++;
            };
            run();
            Assert.That(() => run(), UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory());
            Assert.Greater(found, 0);
        }
    }
}
