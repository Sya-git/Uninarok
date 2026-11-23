using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using Uninarok.Networking.Core.Wire;
using Uninarok.Networking.Data;
using Uninarok.Networking.Packets.In.Map;

namespace Uninarok.World.Tests
{
    /// <summary>
    /// SPEC-0011 FR-1, FR-13 (PLAN-0011 6.1, 6.2; T2.4): the recorded fights (fixtures README) go through the real framer and
    /// router into a model, with no scene. At the end the own HP, SP, levels and EXP equal the server's last status line, and every
    /// damaged monster in view has the HP of its last view line.
    /// </summary>
    public class CombatReplayTests
    {
        internal static string FixturePath(string file) =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "../../docs/specs/0011-combat/fixtures", file));

        // S tick account hp max_hp sp max_sp base_level job_level base_exp job_exp next_base_exp next_job_exp
        readonly struct Status
        {
            public readonly uint Tick;
            public readonly long Hp, MaxHp, Sp, MaxSp, BaseExp, JobExp, NextBaseExp, NextJobExp;
            public readonly int BaseLevel, JobLevel;

            public Status(string line)
            {
                var f = line.Split(' ');
                Tick = uint.Parse(f[1]);
                Hp = long.Parse(f[3]);
                MaxHp = long.Parse(f[4]);
                Sp = long.Parse(f[5]);
                MaxSp = long.Parse(f[6]);
                BaseLevel = int.Parse(f[7]);
                JobLevel = int.Parse(f[8]);
                BaseExp = long.Parse(f[9]);
                JobExp = long.Parse(f[10]);
                NextBaseExp = long.Parse(f[11]);
                NextJobExp = long.Parse(f[12]);
            }

            public override string ToString() =>
                $"hp {Hp}/{MaxHp} sp {Sp}/{MaxSp} lv {BaseLevel}/{JobLevel} exp {BaseExp}/{JobExp} next {NextBaseExp}/{NextJobExp}";
        }

        static string Of(OwnStatus s) =>
            $"hp {s.Hp}/{s.MaxHp} sp {s.Sp}/{s.MaxSp} lv {s.BaseLevel}/{s.JobLevel} exp {s.BaseExp}/{s.JobExp} next {s.NextBaseExp}/{s.NextJobExp}";

        // The character server's data at the start: what the server had a second before the first packet.
        static byte[] CharacterAt(Status s) => OwnCharacterTests.Character(c =>
        {
            c.Hp = s.Hp;
            c.MaxHp = s.MaxHp;
            c.Sp = s.Sp;
            c.MaxSp = s.MaxSp;
            c.BaseLevel = (short)s.BaseLevel;
            c.JobLevel = s.JobLevel;
            c.BaseExp = (ulong)s.BaseExp;
            c.JobExp = (ulong)s.JobExp;
            return c;
        });

        [Test, Category("NeedsData")]
        public void WorldSession_CombatReplay([Values("combat-fight", "combat-death", "combat-bow", "combat-dual")] string capture)
        {
            string file = FixturePath(capture + ".bin"), server = FixturePath(capture + ".server.txt");
            if (!File.Exists(file) || !File.Exists(server)) Assert.Ignore("no fixture " + file);
            var lines = File.ReadAllLines(server);
            Status first = default, last = default;
            int lastView = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                if (!lines[i].StartsWith("S ")) continue;
                if (first.Tick == 0) first = new Status(lines[i]);
                last = new Status(lines[i]);
                lastView = i;
            }
            Assert.AreNotEqual(0u, last.Tick, "no status line");

            using var replay = new WorldReplayTests.Replay();
            // FR-1: a dead character stays dead whatever HP the server sends (the restart HP while dead).
            bool deadWithHp = false;
            replay.Rig.Client.Subscribe<PARAMETER_CHANGE>((in PARAMETER_CHANGE p) =>
            {
                var m = replay.Rig.Model;
                if (p.VarId == 5 && p.Value > 0 && m != null && m.TryGetSlot(m.OwnId, out int o) && m[o].State == EntityState.Dead) deadWithHp = true;
            });
            replay.Run(File.ReadAllBytes(file), last.Tick, CharacterAt(first));
            CollectionAssert.IsEmpty(replay.Errors);

            var status = replay.Rig.Session.Status;
            var model = replay.Rig.Model;
            Debug.Log($"{capture}: {replay.Packets} packets, {replay.Maps} map(s); client {Of(status)}; server {last}");
            Assert.AreEqual(last.ToString(), Of(status), "the own status at the server's last status line");

            // Each monster the server shows damaged at the end has that HP in the model; one at full HP is not reported (FR-7).
            var problems = new StringBuilder();
            int damaged = CompareHp(lines, lastView, model, problems);
            Assert.AreEqual(string.Empty, problems.ToString());

            // The end may have no damaged monster left: the same at the status line with the most of them.
            int most = -1, mostCount = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                if (!lines[i].StartsWith("S ")) continue;
                int n = 0;
                for (int k = i + 1; k < lines.Length && !lines[k].StartsWith("E "); k++)
                {
                    var f = lines[k].Split(' ');
                    if (f[0] == "U" && f[2] == "2" && f.Length >= 10 && int.Parse(f[8]) < int.Parse(f[9])) n++;
                }
                if (n > mostCount) { mostCount = n; most = i; }
            }
            int midway = 0;
            if (most >= 0)
            {
                using var part = new WorldReplayTests.Replay();
                part.Run(File.ReadAllBytes(file), new Status(lines[most]).Tick, CharacterAt(first));
                CollectionAssert.IsEmpty(part.Errors);
                midway = CompareHp(lines, most, part.Rig.Model, problems);
                Assert.AreEqual(string.Empty, problems.ToString(), "at the status line of tick " + new Status(lines[most]).Tick);
            }
            Debug.Log($"{capture}: {damaged} damaged monster(s) compared at the end, {midway} at the busiest status line; dead with HP above 0: {deadWithHp}");
            if (capture == "combat-death") Assert.IsTrue(deadWithHp, "the recorded death sends the restart HP while dead");
        }

        // The monsters of the view block after the status line `at`: damaged ones have the server's HP. Returns how many were damaged.
        static int CompareHp(string[] lines, int at, WorldModel model, StringBuilder problems)
        {
            int damaged = 0;
            for (int i = at + 1; i < lines.Length && !lines[i].StartsWith("E "); i++)
            {
                var f = lines[i].Split(' ');
                if (f[0] != "U" || f[2] != "2" || f.Length < 10) continue;
                uint id = uint.Parse(f[1]);
                int hp = int.Parse(f[8]), max = int.Parse(f[9]);
                if (!model.TryGetSlot(id, out int slot)) continue; // seen by the server but not sent yet
                int shown = model[slot].Hp;
                if (hp < max)
                {
                    damaged++;
                    if (shown != hp) problems.AppendLine($"monster {id}: hp {shown}, the server {hp}/{max}");
                }
                else if (shown != -1 && shown != max) problems.AppendLine($"monster {id} at full hp: the client has {shown}");
            }
            return damaged;
        }
    }
}
