using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Uninarok.Networking.Packets.In.Map;
using static Uninarok.World.Tests.WorldSessionTests;

namespace Uninarok.World.Tests
{
    // SPEC-0011 FR-12 (PLAN-0011 §6.1): the session, the own combat and the timeline allocate nothing per frame while 100 monsters
    // fight the own character, after warm-up.
    public class CombatAllocationTests
    {
        static DAMAGE Damage(uint source, uint target, int damage, int type, short count, int attackMt) =>
            new DAMAGE(Hex(Le(source) + Le(target) + Le(1234u) + Le((uint)attackMt) + Le(288u) + Le((uint)damage) + "00" + Le16(count) + $"{type:X2}" + Le(0u)));

        [Test]
        public void Combat_ZeroAllocation_100Fighting()
        {
            var settings = ScriptableObject.CreateInstance<CombatSettings>();
            try
            {
                using var rig = new Rig().ToInGame(OwnCharacterTests.Character());
                rig.Session.UseCombat(settings);
                uint own = rig.Model.OwnId;
                const int Monsters = 100;
                var hits = new DAMAGE[Monsters];
                var backs = new DAMAGE[Monsters];
                for (int i = 0; i < Monsters; i++)
                {
                    uint id = 0x068EB5EC + (uint)i;
                    rig.Client.Deliver(Stand(id, 140 + i % 20, 195 + i / 20));
                    int[] types = { 0, 10, 8, 11 };
                    hits[i] = Damage(id, own, i % 7 == 0 ? 0 : 12, i % 3 == 0 ? 4 : 0, 1, 400 + i * 10);
                    backs[i] = Damage(own, id, 30 + i, types[i % types.Length], (short)(i % 3 + 1), 480);
                }
                rig.Session.Combat.Attack(0x068EB5EC + 50, true);
                rig.Client.RecordSends = false; // the fake client's list of sent packets would allocate, not the session

                int frame = 0;
                Action run = () =>
                {
                    for (int f = 0; f < 600; f++, frame++)
                    {
                        int i = frame % Monsters;
                        rig.Client.Deliver(hits[i]);
                        rig.Client.Deliver(backs[(i * 37) % Monsters]);
                        rig.Local.AdvanceMs(16);
                        uint now = rig.Clock.Now;
                        rig.Model.Advance(now);
                        rig.Session.Combat.Tick(now);
                        rig.Session.Timeline.Advance(now);
                    }
                };
                run();
                Assert.Greater(rig.Session.Timeline.Numbers.Length, 50, "numbers are shown");
                Assert.That(() => run(), UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }
    }
}
