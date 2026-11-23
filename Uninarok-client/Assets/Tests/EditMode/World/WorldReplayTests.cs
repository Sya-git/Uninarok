using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using Uninarok.Networking.Core.Framing;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Transport;
using Uninarok.Networking.Flow;
using Uninarok.Networking.Packets;
using Uninarok.Networking.Packets.In.Map;

namespace Uninarok.World.Tests
{
    /// <summary>
    /// SPEC-0010 FR-3, FR-4, FR-6, FR-18 (PLAN-0010 6; T5.6): recorded sessions of the real server (fixtures README) go through
    /// the real framer and router into a model, with no scene. A capture has no times: the ticks in its packets move the clock.
    /// The server's last view of the player, written by uninarok_pathdump, is what the model must hold at the end.
    /// </summary>
    public class WorldReplayTests
    {
        const uint Account = 2000000;

        sealed class Replay : IDisposable
        {
            public readonly WorldSessionTests.Rig Rig = new WorldSessionTests.Rig();
            public readonly List<string> Errors = new List<string>();
            /// <summary>Moves and stops of known units checked, and the farthest one was from where the server had it, in cells.</summary>
            public int Checked, Far;
            public float Worst;
            public string WorstAt = string.Empty;
            public int Packets, Maps;
            string _map;
            uint[] _nextTick;

            /// <summary>Feeds the capture up to the server's last look at the player (<paramref name="end"/>).</summary>
            public void Run(byte[] bytes, uint end)
            {
                bytes = Cut(bytes, end);
                var c = Rig.Client;
                // Before the session's own handlers, which it subscribes once the flow is on the map server.
                c.Subscribe<UNIT_MOVE>((in UNIT_MOVE p) =>
                {
                    Time(p.StartTick);
                    Check(p.UnitId, p.Move.SourceX, p.Move.SourceY, "move");
                });
                c.Subscribe<PLAYER_MOVE>((in PLAYER_MOVE p) =>
                {
                    Time(p.StartTick);
                    Check(Account, p.Move.SourceX, p.Move.SourceY, "own move");
                });
                c.Subscribe<UNIT_MOVE_ENTRY>((in UNIT_MOVE_ENTRY p) => Time(p.MoveStartTime));
                c.Subscribe<SERVER_HEARTBEAT_RESPONSE>((in SERVER_HEARTBEAT_RESPONSE p) => Time(p.ServerTimestamp));
                c.Subscribe<UNIT_STOP_MOVE>((in UNIT_STOP_MOVE p) =>
                {
                    // No tick: it was sent some time before the next one. Go on until the unit is there, not farther.
                    uint next = Packets < _nextTick.Length ? _nextTick[Packets] : Rig.Clock.Now;
                    while (Distance(p.UnitId, p.X, p.Y) > 1f && ServerClock.Diff(next, Rig.Clock.Now) > 0) Time(Rig.Clock.Now + 10);
                    Check(p.UnitId, p.X, p.Y, "stop");
                });
                Rig.Flow.MapLoadRequested += () =>
                {
                    Maps++;
                    _map = Uninarok.Map.MapSceneKeys.MapName(Rig.Flow.Context.Map.MapName.ToString());
                };
                Rig.ToLoadingMap(OwnCharacterTests.Character(), authenticate: false);
                Rig.Flow.Context.AccountId = Account;

                var router = new GeneratedPacketRouter(c.Dispatcher, (id, e) => Errors.Add($"0x{id:X4}: {e.Message}"));
                var framer = new PacketFramer(HerculesPacketTable.Instance, router);
                framer.Append(bytes);
                while (framer.Process(1) > 0)
                {
                    Packets++;
                    if (Rig.Flow.State != FlowState.LoadingMap || _map == null) continue;
                    Rig.Session.BeginMap(WalkGridTests.LoadMap(_map));
                    _map = null;
                    Rig.Flow.MapLoaded();
                }
                Assert.IsFalse(framer.IsFaulted, framer.FaultReason);
                foreach (var e in c.HandlerErrors) Errors.Add(e.ToString());
            }

            // The packets before the first one sent after `end`, and for each the next tick known after it.
            byte[] Cut(byte[] bytes, uint end)
            {
                var ticks = new List<uint?>();
                int i = 0;
                while (i + 2 <= bytes.Length)
                {
                    ushort id = BitConverter.ToUInt16(bytes, i);
                    int length = HerculesPacketTable.Instance.GetLength(id);
                    if (length == -1) length = BitConverter.ToUInt16(bytes, i + 2);
                    int at = id == PacketIds.UNIT_MOVE ? 12 : id == PacketIds.PLAYER_MOVE || id == PacketIds.SERVER_HEARTBEAT_RESPONSE ? 2 : id == PacketIds.UNIT_MOVE_ENTRY ? 37 : 0;
                    uint? tick = at == 0 ? (uint?)null : BitConverter.ToUInt32(bytes, i + at);
                    if (tick.HasValue && ServerClock.Diff(tick.Value, end) > 0) break;
                    ticks.Add(tick);
                    i += length;
                }
                _nextTick = new uint[ticks.Count];
                uint next = end;
                for (int k = ticks.Count - 1; k >= 0; k--) _nextTick[k] = next = ticks[k] ?? next;
                return bytes.AsSpan(0, i).ToArray();
            }

            // The client's clock is at least here: the packet was sent at this server tick.
            void Time(uint tick)
            {
                int ahead = ServerClock.Diff(tick, Rig.Clock.Now);
                if (ahead > 0 && Rig.Clock.IsSynced) Rig.Local.AdvanceMs(ahead);
                Rig.Model?.Advance(Rig.Clock.Now);
            }

            float Distance(uint id, int x, int y)
            {
                var model = Rig.Model;
                if (model == null || !model.TryGetSlot(id, out int slot)) return 0f;
                return Math.Max(Math.Abs(model[slot].PosX - x), Math.Abs(model[slot].PosY - y));
            }

            void Check(uint id, int x, int y, string what)
            {
                var model = Rig.Model;
                if (model == null || !model.TryGetSlot(id, out int slot)) return;
                ref readonly var e = ref model[slot];
                if (e.State == EntityState.Dead) return;
                Checked++;
                float distance = Math.Max(Math.Abs(e.PosX - x), Math.Abs(e.PosY - y));
                if (distance > 1.001f) Far++;
                if (distance <= Worst) return;
                Worst = distance;
                WorstAt = $"{what} of {e.Kind} {id} ({e.Look.Job}): shown {e.PosX:0.00},{e.PosY:0.00}, the server {x},{y}";
            }

            public void Dispose() => Rig.Dispose();
        }

        readonly struct Seen
        {
            public readonly uint Id;
            public readonly int Type, Class, X, Y, State;
            public readonly bool Walking;

            public Seen(string[] f)
            {
                Id = uint.Parse(f[1]);
                Type = int.Parse(f[2]);
                Class = int.Parse(f[3]);
                X = int.Parse(f[4]);
                Y = int.Parse(f[5]);
                Walking = f[6] != "0";
                State = int.Parse(f[7]);
            }
        }

        [Test, Category("NeedsData")]
        public void WorldSession_Replay([Values("world-prontera", "world-prt_fild08", "world-warp")] string capture)
        {
            string file = HerculesPathfinderTests.FixturePath(capture + ".bin"), view = HerculesPathfinderTests.FixturePath(capture + ".view.txt");
            if (!File.Exists(file) || !File.Exists(view)) Assert.Ignore("no fixture " + file);
            var lines = File.ReadAllLines(view);
            var own = lines[0].Split(' '); // A tick account map x y
            uint tick = uint.Parse(own[1]);
            using var replay = new Replay();
            replay.Run(File.ReadAllBytes(file), tick);
            CollectionAssert.IsEmpty(replay.Errors);

            var model = replay.Rig.Model;
            UnityEngine.Debug.Log($"{replay.Packets} packets, {replay.Maps} map(s), {model.Count} entities; {replay.Checked} moves and stops checked, " +
                                  $"{replay.Far} farther than a cell, worst {replay.Worst:0.00}: {replay.WorstAt}");

            int ahead = ServerClock.Diff(tick, replay.Rig.Clock.Now);
            if (ahead > 0) replay.Rig.Local.AdvanceMs(ahead);
            model.Advance(replay.Rig.Clock.Now);

            Assert.AreEqual(own[3], ((MapDataWalkGrid)model.Grid).Data.MapName);
            var expected = new Dictionary<uint, Seen>();
            for (int i = 1; i < lines.Length; i++)
                if (lines[i].StartsWith("U ")) expected.Add(new Seen(lines[i].Split(' ')).Id, new Seen(lines[i].Split(' ')));

            var problems = new StringBuilder();
            int compared = 0;
            for (int i = 0; i < model.SlotLimit; i++)
            {
                if (!model.IsUsed(i)) continue;
                ref readonly var e = ref model[i];
                if (e.Id == model.OwnId)
                {
                    Near(problems, e, int.Parse(own[4]), int.Parse(own[5]), e.Walking ? 1 : 0);
                    continue;
                }
                if (e.State == EntityState.Dead && e.Kind == EntityKind.Monster) continue; // off the server's map, still shown
                if (!expected.Remove(e.Id, out var s))
                {
                    problems.AppendLine($"the server has no {e.Kind} {e.Id} ({e.Look.Job}) in view; the client has it on {e.CellX},{e.CellY}");
                    continue;
                }
                compared++;
                int job = e.IsItem ? e.ItemId : e.Look.Job;
                if (job != s.Class) problems.AppendLine($"{e.Id}: class {job}, the server {s.Class}");
                Near(problems, e, s.X, s.Y, s.Walking || e.Walking ? 1 : 0);
            }
            foreach (var s in expected.Values) problems.AppendLine($"missing: {s.Id} class {s.Class} on {s.X},{s.Y}");
            if (problems.Length > 0) UnityEngine.Debug.Log(problems.ToString());
            Assert.AreEqual(string.Empty, problems.ToString());
            Assert.Greater(compared, 0);
            Assert.LessOrEqual(replay.Far, FarAllowed(replay.Checked), replay.WorstAt);
        }

        // Which path a chasing monster took is not in the packets (D11): a few may be off mid-walk.
        static int FarAllowed(int checkedMoves) => 0;

        static void Near(StringBuilder problems, in Entity e, int x, int y, int within)
        {
            if (Math.Abs(e.CellX - x) > within || Math.Abs(e.CellY - y) > within)
                problems.AppendLine($"{e.Kind} {e.Id}: cell {e.CellX},{e.CellY}, the server {x},{y} (within {within})");
        }
    }
}
