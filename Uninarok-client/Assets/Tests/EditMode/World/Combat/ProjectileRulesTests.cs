using NUnit.Framework;
using UnityEngine;
using Uninarok.Networking.Data;

namespace Uninarok.World.Tests
{
    // SPEC-0011 FR-13, FR-14 (PLAN-0011 4.7): which blows have a projectile and when it flies, alone and through the timeline.
    public class ProjectileRulesTests
    {
        sealed class Grid : IWalkGrid
        {
            public int Width => 64;
            public int Height => 64;
            public bool IsPassable(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
            public bool BlocksSight(int x, int y) => !IsPassable(x, y);
        }

        // The look's right-hand value is its type already.
        sealed class SameTypes : IWeaponTypes
        {
            public int TypeOf(int job, int right, int left) => right;
        }

        const uint T = 10_000;

        static Blow Hit(uint attacker, uint target, int attackMs = 432) =>
            new Blow(attacker, target, T, attackMs, 300, 10, 0, 1, BlowKind.Normal, false, 0);

        [Test]
        public void ProjectileRules_Players()
        {
            Assert.AreEqual(ProjectileKind.Arrow, ProjectileRules.Of(EntityKind.Player, 3, 11), "a bow");
            foreach (int weapon in new[] { 0, 1, 13, 14, 17, 18, 19, 20, 21, 22 })
                Assert.AreEqual(ProjectileKind.None, ProjectileRules.Of(EntityKind.Player, 24, weapon), $"weapon type {weapon}");
        }

        [Test]
        public void ProjectileRules_Monsters()
        {
            Assert.AreEqual(ProjectileKind.Arrow, ProjectileRules.Of(EntityKind.Monster, 1016, 0), "archer skeleton");
            Assert.AreEqual(ProjectileKind.Arrow, ProjectileRules.Of(EntityKind.Monster, 1189, 0), "orc archer");
            Assert.AreEqual(ProjectileKind.Arrow, ProjectileRules.Of(EntityKind.Monster, 22256, 0), "the last row");
            Assert.AreEqual(ProjectileKind.Arrow, ProjectileRules.Of(EntityKind.Mercenary, 6017, 0), "an archer mercenary");
            Assert.AreEqual(ProjectileKind.Other, ProjectileRules.Of(EntityKind.Monster, 1500, 0), "parasite");
            Assert.AreEqual(ProjectileKind.Other, ProjectileRules.Of(EntityKind.Monster, 1664, 0), "poton canon");
            Assert.AreEqual(ProjectileKind.None, ProjectileRules.Of(EntityKind.Monster, 1002, 0), "poring");
            Assert.AreEqual(ProjectileKind.None, ProjectileRules.Of(EntityKind.Monster, 1020, 0), "mandragora: an effect at the target, not a projectile");
            Assert.AreEqual(ProjectileKind.None, ProjectileRules.Of(EntityKind.Npc, 1016, 0), "an NPC");
            Assert.AreEqual(ProjectileKind.None, ProjectileRules.Of(EntityKind.Monster, 70000, 0));
        }

        [Test]
        public void ProjectileRules_FlightTimes_2_5_9()
        {
            Assert.AreEqual(48, ProjectileRules.FlightMs(2));
            Assert.AreEqual(120, ProjectileRules.FlightMs(5));
            Assert.AreEqual(192, ProjectileRules.FlightMs(9), "8 cells or more: the whole flight");
            Assert.AreEqual(1, ProjectileRules.FlightMs(0), "at least 1 ms");

            ProjectileRules.Times(Hit(1, 2, 864), true, 5, 6, out uint launches, out uint arrives, out bool waits);
            Assert.AreEqual((T + 288, T + 408, true), (launches, arrives, waits), "player: 6 frames of 24 ms at twice 432 ms");
            ProjectileRules.Times(Hit(1, 2, 1000), false, 9, 0, out launches, out arrives, out waits);
            Assert.AreEqual((T + 1000, T + 1192, false), (launches, arrives, waits), "monster: launched at the hit moment");
        }

        [Test]
        public void ProjectileRules_NumberWaits()
        {
            var world = ScriptableObject.CreateInstance<WorldSettings>();
            var settings = ScriptableObject.CreateInstance<CombatSettings>();
            try
            {
                var model = new WorldModel(new Grid(), world, 64);
                model.Upsert(1, EntityKind.Player, new EntityLook { Job = 3, Weapon = 11 }, 150, 10, 10, UninarokDirection.South, HeadDirection.Straight, EntityState.Standing);
                model.Upsert(2, EntityKind.Player, new EntityLook { Job = 0 }, 150, 10, 20, UninarokDirection.South, HeadDirection.Straight, EntityState.Standing);
                model.Upsert(3, EntityKind.Monster, new EntityLook { Job = 1016 }, 200, 15, 10, UninarokDirection.West, HeadDirection.Straight, EntityState.Standing);
                model.Upsert(4, EntityKind.Monster, new EntityLook { Job = 1500 }, 200, 10, 15, UninarokDirection.West, HeadDirection.Straight, EntityState.Standing);
                model.Upsert(5, EntityKind.Monster, new EntityLook { Job = 1002 }, 200, 11, 10, UninarokDirection.West, HeadDirection.Straight, EntityState.Standing);
                var timeline = new CombatTimeline(settings, model.Capacity) { Weapons = new SameTypes() };
                timeline.Use(model);

                // A bow at 5 cells: launched at 6 × 24 ms, the number at its arrival 120 ms later.
                timeline.Add(Hit(1, 3));
                Assert.AreEqual(1, timeline.Projectiles.Length);
                var arrow = timeline.Projectiles[0];
                Assert.AreEqual((ProjectileKind.Arrow, T + 144, T + 264), (arrow.Kind, arrow.Launches, arrow.Arrives));
                Assert.AreEqual((10f, 10f, 15f, 10f), (arrow.FromX, arrow.FromY, arrow.ToX, arrow.ToY));
                timeline.Advance(T + 263);
                Assert.AreEqual(0, timeline.Numbers.Length, "the number waits for the arrow");
                timeline.Advance(T + 264);
                Assert.AreEqual(1, timeline.Numbers.Length);
                Assert.AreEqual(0, timeline.Projectiles.Length, "arrived");

                // An archer monster: launched at the hit moment, the number at once.
                timeline.Clear();
                timeline.Add(Hit(3, 1, 1000));
                Assert.AreEqual((ProjectileKind.Arrow, T + 1000), (timeline.Projectiles[0].Kind, timeline.Projectiles[0].Launches));
                timeline.Advance(T + 1000);
                Assert.AreEqual(1, timeline.Numbers.Length, "the number does not wait");
                Assert.AreEqual(1, timeline.Projectiles.Length, "the arrow still flies");

                // A projectile of its own: timed, kept for SPEC-0013.
                timeline.Clear();
                timeline.Add(Hit(4, 1));
                Assert.AreEqual(ProjectileKind.Other, timeline.Projectiles[0].Kind);

                // None: a poring, a player without a bow.
                timeline.Clear();
                timeline.Add(Hit(5, 1));
                timeline.Add(Hit(2, 5));
                Assert.AreEqual(0, timeline.Projectiles.Length);
            }
            finally
            {
                Object.DestroyImmediate(settings);
                Object.DestroyImmediate(world);
            }
        }
    }
}
