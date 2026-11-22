using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace Uninarok.World.Tests
{
    // SPEC-0010 FR-6, FR-10 (PLAN-0010 4.4, D11; T3.8): how much of the server's real walks the client's rule finds. The log was
    // written by uninarok_pathdump on a running server (fixtures README).
    public class WalkLogTests
    {
        /// <summary>The share of monster walks the rule must find: T3.8 measured 2,760 of 2,763 (the easy path first: 2,722).</summary>
        const double MonsterShare = 0.998;

        readonly struct Walk
        {
            public readonly EntityKind Kind;
            public readonly bool Easy;
            public readonly int X0, Y0, X1, Y1;
            public readonly string Steps;

            public Walk(string[] f)
            {
                Kind = f[4] == "1" ? EntityKind.Player : f[4] == "2" ? EntityKind.Monster : EntityKind.Npc;
                Easy = f[5] != "0";
                X0 = int.Parse(f[7]);
                Y0 = int.Parse(f[8]);
                X1 = int.Parse(f[9]);
                Y1 = int.Parse(f[10]);
                Steps = f[11] == "-" ? string.Empty : f[11];
            }
        }

        static List<Walk> Load()
        {
            string file = HerculesPathfinderTests.FixturePath("walklog-prt_fild08.txt");
            if (!File.Exists(file)) Assert.Ignore("no fixture " + file);
            var walks = new List<Walk>();
            foreach (string line in File.ReadLines(file))
                if (line.StartsWith("W ")) walks.Add(new Walk(line.Split(' ')));
            return walks;
        }

        sealed class Score
        {
            public int Count, Same, Easy, EasySame, Worst;

            public double Share => Count == 0 ? 1.0 : (double)Same / Count;

            public void Add(in Walk w, in WalkPath path, bool found)
            {
                Count++;
                if (w.Easy) Easy++;
                string steps = string.Empty;
                for (int i = 0; found && i < path.Length; i++) steps += (char)('0' + path.Step(i));
                if (steps == w.Steps)
                {
                    Same++;
                    if (w.Easy) EasySame++;
                }
                else Worst = Math.Max(Worst, Distance(w, steps));
            }

            // The largest distance (in cells) between the two at the same step.
            static int Distance(in Walk w, string steps)
            {
                int worst = 0, ax = w.X0, ay = w.Y0, bx = w.X0, by = w.Y0;
                for (int i = 0; i < Math.Max(steps.Length, w.Steps.Length); i++)
                {
                    if (i < steps.Length)
                    {
                        ax += HerculesPathfinder.Dx(steps[i] - '0');
                        ay += HerculesPathfinder.Dy(steps[i] - '0');
                    }
                    if (i < w.Steps.Length)
                    {
                        bx += HerculesPathfinder.Dx(w.Steps[i] - '0');
                        by += HerculesPathfinder.Dy(w.Steps[i] - '0');
                    }
                    worst = Math.Max(worst, Math.Max(Math.Abs(ax - bx), Math.Abs(ay - by)));
                }
                return worst;
            }

            public override string ToString() => $"{Same} of {Count} ({Share:P2}), easy {EasySame} of {Easy}, largest distance {Worst}";
        }

        [Test, Category("NeedsData")]
        public void SearchForUnit_WalkLog_PrtFild08()
        {
            var walks = Load();
            var finder = new HerculesPathfinder(new MapDataWalkGrid(WalkGridTests.LoadMap("prt_fild08")));
            var path = new WalkPath();
            Score monsters = new Score(), easyFirst = new Score(), players = new Score();
            foreach (var w in walks)
            {
                if (w.Kind == EntityKind.Monster)
                {
                    monsters.Add(w, path, Found(finder.SearchForUnit(w.Kind, w.X0, w.Y0, w.X1, w.Y1, ref path), path, out path));
                    var easy = finder.SearchEasy(w.X0, w.Y0, w.X1, w.Y1, ref path);
                    if (easy == PathResult.NoPath) easy = finder.Search(w.X0, w.Y0, w.X1, w.Y1, ref path);
                    easyFirst.Add(w, path, Found(easy, path, out path));
                }
                else players.Add(w, path, Found(finder.SearchForUnit(w.Kind, w.X0, w.Y0, w.X1, w.Y1, ref path), path, out path));
            }
            UnityEngine.Debug.Log($"monsters, the rule: {monsters}");
            UnityEngine.Debug.Log($"monsters, the easy path first: {easyFirst}");
            UnityEngine.Debug.Log($"others, A*: {players}");
            Assert.Greater(monsters.Count, 100);
            Assert.AreEqual(players.Count, players.Same, "players always walk the A* path");
            Assert.GreaterOrEqual(monsters.Share, MonsterShare);
            Assert.LessOrEqual(monsters.Worst, 1);
        }

        static bool Found(PathResult result, in WalkPath path, out WalkPath same)
        {
            same = path;
            return result == PathResult.Found;
        }
    }
}
