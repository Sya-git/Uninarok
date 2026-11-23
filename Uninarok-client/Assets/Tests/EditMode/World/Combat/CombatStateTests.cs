using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Uninarok.Networking.Core.Wire;
using Uninarok.Networking.Data;
using Uninarok.Networking.Packets.In.Map;
using Uninarok.Networking.Tests;
using static Uninarok.World.Tests.WorldSessionTests;

namespace Uninarok.World.Tests
{
    // SPEC-0011 FR-1, FR-4, FR-5, FR-9 (PLAN-0011 4.2, 4.3): the combat state of the model and the own status, with no scene.
    public class CombatStateTests
    {
        sealed class Grid : IWalkGrid
        {
            public int Width => 300;
            public int Height => 300;
            public bool IsPassable(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
        }

        const uint Poring = 0x068EB5EC, Player = 2000002, Other = 2000003;
        const int TypeNormal = 0, TypeEndure = 4, TypeMultiHit = 8, TypeMultiEndure = 9, TypeCritical = 10, TypeLuckyDodge = 11;

        WorldSettings _settings;
        WorldModel _world;

        [SetUp]
        public void SetUp()
        {
            _settings = ScriptableObject.CreateInstance<WorldSettings>();
            _world = new WorldModel(new Grid(), _settings, 64);
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(_settings);

        static DAMAGE Damage(uint source, uint target, int damage, int type = TypeNormal, short count = 1, int attackMt = 500, int attackedMt = 300, int left = 0, uint tick = 1234) =>
            new DAMAGE(Hex(Le(source) + Le(target) + Le(tick) + Le((uint)attackMt) + Le((uint)attackedMt) + Le((uint)damage) + "00" + Le16(count) + $"{type:X2}" + Le((uint)left)));

        int Monster(uint id = Poring, int x = 50, int y = 50) =>
            _world.Upsert(id, EntityKind.Monster, new EntityLook { Job = 1002 }, 200, x, y, UninarokDirection.South, HeadDirection.Straight, EntityState.Standing);

        int Human(uint id = Player, int x = 53, int y = 50) =>
            _world.Upsert(id, EntityKind.Player, new EntityLook { Job = 1 }, 150, x, y, UninarokDirection.South, HeadDirection.Left, EntityState.Standing);

        Entity Get(uint id)
        {
            Assert.IsTrue(_world.TryGetSlot(id, out int slot), $"no entity {id}");
            return _world[slot];
        }

        // ---- Blow (FR-5) ----

        [Test, Ignore("pending T2.2")]
        public void Blow_From_EachWireType()
        {
            var rows = new (int type, BlowKind kind, bool endured)[]
            {
                (TypeNormal, BlowKind.Normal, false), (TypeEndure, BlowKind.Normal, true), (5, BlowKind.Normal, false), (6, BlowKind.Normal, false),
                (TypeMultiHit, BlowKind.MultiHit, false), (TypeMultiEndure, BlowKind.MultiHit, true), (TypeCritical, BlowKind.Critical, false),
                (TypeLuckyDodge, BlowKind.LuckyDodge, false), (13, BlowKind.MultiHit, false), (99, BlowKind.Normal, false),
            };
            foreach (var row in rows)
            {
                var b = Blow.From(Damage(Poring, Player, 40, row.type, 2, 500, 300, 7), 9000);
                Assert.AreEqual((row.kind, row.endured, (byte)row.type), (b.Kind, b.Endured, b.WireType), "type " + row.type);
                Assert.AreEqual((Poring, Player, 9000u), (b.AttackerId, b.TargetId, b.At), "type " + row.type);
                Assert.AreEqual((500, 300, 40, 7, (short)2), (b.AttackMs, b.HurtMs, b.Damage, b.LeftDamage, b.Hits), "type " + row.type);
            }
        }

        [Test, Ignore("pending T2.2")]
        public void Blow_From_CountZeroIsOneHit()
        {
            Assert.AreEqual(1, Blow.From(Damage(Player, Player, 12, count: 0, attackMt: 0, attackedMt: 0), 0).Hits);
            Assert.AreEqual(1, Blow.From(Damage(Player, Player, 12, count: -3), 0).Hits);
            Assert.IsTrue(Blow.From(Damage(Poring, Player, 0), 0).IsMiss);
            Assert.IsFalse(Blow.From(Damage(Poring, Player, 0, TypeLuckyDodge), 0).IsMiss, "a lucky dodge is shown as one");
            Assert.IsFalse(Blow.From(Damage(Poring, Player, 0, left: 5), 0).IsMiss, "the second hand hit");
            Assert.AreEqual(-100, Blow.From(Damage(Player, Player, -100, count: 0), 0).Damage, "a heal is kept as it came");
        }

        // ---- the model (FR-1, FR-4, FR-9) ----

        [Test, Ignore("pending T2.2")]
        public void WorldModel_Strike_TargetFacingLastBlow()
        {
            Monster();
            Human();
            int version = Get(Poring).Version;
            _world.Strike(Blow.From(Damage(Poring, Player, 25), 100));
            var m = Get(Poring);
            Assert.AreEqual(Player, m.TargetId);
            Assert.AreEqual((25, 100u), (m.LastBlow.Damage, m.LastBlow.At));
            Assert.AreEqual(UninarokDirection.East, m.Facing, "turns to its target");
            Assert.AreEqual(version + 1, m.Version, "one version bump");
            Assert.AreEqual(UninarokDirection.South, Get(Player).Facing, "the target does not turn");

            // The player hits back while walking: it turns and goes on walking (the walk faces its next step at the next one).
            _world.Walk(Player, 53, 50, 53, 55, 100, 100);
            Assert.IsTrue(Get(Player).Walking);
            _world.Strike(Blow.From(Damage(Player, Poring, 10), 120));
            Assert.IsTrue(Get(Player).Walking, "a blow stops no walk");
            Assert.AreEqual(Poring, Get(Player).TargetId);
            Assert.AreEqual(HeadDirection.Straight, Get(Player).Head);

            // An unknown target: the attacker's part only; an unknown attacker: nothing.
            _world.Strike(Blow.From(Damage(Poring, 999, 3), 130));
            Assert.AreEqual((999u, UninarokDirection.East), (Get(Poring).TargetId, Get(Poring).Facing));
            Assert.DoesNotThrow(() => _world.Strike(Blow.From(Damage(998, Poring, 3), 140)));
        }

        [Test, Ignore("pending T2.2")]
        public void WorldModel_SetHp_FromEachEntry()
        {
            Monster();
            Assert.AreEqual((-1, -1), (Get(Poring).Hp, Get(Poring).MaxHp), "not known until the server says");
            _world.SetHp(Poring, 30, 50);
            Assert.AreEqual((30, 50), (Get(Poring).Hp, Get(Poring).MaxHp));
            // A re-sent stand entry, then a walking one: each is followed by its HP.
            Monster();
            _world.SetHp(Poring, 20, 50);
            _world.UpsertWalking(Poring, EntityKind.Monster, new EntityLook { Job = 1002 }, 200, 50, 50, 52, 50, HeadDirection.Straight, 0, 0);
            _world.SetHp(Poring, 10, 50);
            Assert.AreEqual((10, 50), (Get(Poring).Hp, Get(Poring).MaxHp));
            int version = Get(Poring).Version;
            _world.SetHp(Poring, -1, -1);
            Assert.AreEqual((-1, -1), (Get(Poring).Hp, Get(Poring).MaxHp), "−1 clears it");
            Assert.Greater(Get(Poring).Version, version);
            Assert.DoesNotThrow(() => _world.SetHp(999, 1, 1));
        }

        [Test, Ignore("pending T2.2")]
        public void WorldModel_VanishClearsTargets()
        {
            Monster();
            Monster(Poring + 1, 51, 50);
            Human();
            Human(Other, 54, 50);
            _world.Strike(Blow.From(Damage(Poring, Player, 1), 0));
            _world.Strike(Blow.From(Damage(Poring + 1, Player, 1), 0));
            _world.Strike(Blow.From(Damage(Other, Poring, 1), 0));
            _world.Vanish(Player, VanishReason.OutOfSight, 10);
            Assert.AreEqual((0u, 0u), (Get(Poring).TargetId, Get(Poring + 1).TargetId), "nobody attacks a unit that left");
            Assert.AreEqual(Poring, Get(Other).TargetId);

            // A unit that dies is attacked by nobody and attacks nobody.
            _world.Strike(Blow.From(Damage(Poring, Other, 1), 20));
            _world.Vanish(Poring, VanishReason.Died, 30);
            Assert.AreEqual((0u, 0u), (Get(Other).TargetId, Get(Poring).TargetId));
        }

        [Test, Ignore("pending T2.2")]
        public void WorldModel_FeignedDeathIsNotDeath()
        {
            Human();
            _world.Vanish(Player, VanishReason.FeignedDeath, 0);
            Assert.AreEqual((EntityState.Dead, true), (Get(Player).State, Get(Player).Feigning));
            _world.SetState(Player, EntityState.Standing);
            Assert.AreEqual((EntityState.Standing, false), (Get(Player).State, Get(Player).Feigning), "standing up ends it");

            _world.Vanish(Player, VanishReason.Died, 0);
            Assert.AreEqual((EntityState.Dead, false), (Get(Player).State, Get(Player).Feigning));
            // RESURRECTION in the model: it stands where it lies.
            _world.SetState(Player, EntityState.Standing);
            Assert.AreEqual(EntityState.Standing, Get(Player).State);
        }

        [Test, Ignore("pending T2.2")]
        public void WorldModel_ZeroHpDoesNotKill()
        {
            Monster();
            uint before = _world.Checksum();
            _world.SetHp(Poring, 0, 50);
            Assert.AreEqual(EntityState.Standing, Get(Poring).State, "only the death notice kills (FR-1)");
            Assert.AreNotEqual(before, _world.Checksum(), "the checksum sees HP");
        }

        // ---- the own status, through the session (FR-1) ----

        static byte[] CharacterWith(long hp, long maxHp, long sp, long maxSp, short baseLevel, int jobLevel, ulong baseExp, ulong jobExp)
        {
            var c = new CharInfo
            {
                CharId = 140593, Name = new Unity.Collections.FixedString32Bytes("One"), MapName = new Unity.Collections.FixedString32Bytes("prontera"),
                Slot = 0, Sex = AccountSex.SEX_MALE, Hp = hp, MaxHp = maxHp, Sp = sp, MaxSp = maxSp, BaseLevel = baseLevel, JobLevel = jobLevel,
                BaseExp = baseExp, JobExp = jobExp,
            };
            var buf = new byte[CharInfo.WireSize];
            var w = new PacketWriter(buf);
            c.Write(ref w);
            return buf;
        }

        static LONG_PARAMETER_CHANGE LongParameter(int var, long value) =>
            new LONG_PARAMETER_CHANGE(Hex(Le16(var) + Le((uint)value) + Le((uint)(value >> 32))));

        static ATTACK_RANGE Range(int range) => new ATTACK_RANGE(Hex(Le16(range)));

        static ATTACK_TOO_FAR TooFar(uint target, int range) => new ATTACK_TOO_FAR(Hex(Le(target) + "A000BE00" + "9800BE00" + Le16(range)));

        static RESURRECTION Raise(uint id) => new RESURRECTION(Hex(Le(id) + "0000"));

        [Test, Ignore("pending T2.3")]
        public void OwnStatus_FromCharInfoThenUpdates()
        {
            using var rig = new Rig().ToInGame(CharacterWith(40, 50, 10, 12, 5, 3, 7, 2));
            var s = rig.Session.Status;
            Assert.AreEqual((40L, 50L, 10L, 12L), (s.Hp, s.MaxHp, s.Sp, s.MaxSp));
            Assert.AreEqual((5, 3, 7L, 2L, 1), (s.BaseLevel, s.JobLevel, s.BaseExp, s.JobExp, s.AttackRange));
            Assert.AreEqual((40, 50), (rig.Get(rig.Model.OwnId).Hp, rig.Get(rig.Model.OwnId).MaxHp), "the own entity has the same HP");

            int version = s.Version;
            rig.Client.Deliver(Parameter(5, 30));
            rig.Client.Deliver(Parameter(6, 60));
            rig.Client.Deliver(Parameter(7, 8));
            rig.Client.Deliver(Parameter(8, 20));
            rig.Client.Deliver(Parameter(11, 6));
            rig.Client.Deliver(Parameter(55, 4));
            rig.Client.Deliver(Parameter(53, 999)); // ASPD: not part of it
            rig.Client.Deliver(LongParameter(1, 5_000_000_000L));
            rig.Client.Deliver(LongParameter(2, 9));
            rig.Client.Deliver(LongParameter(22, 350));
            rig.Client.Deliver(LongParameter(23, 10));
            rig.Client.Deliver(LongParameter(20, 1234)); // zeny: ignored
            rig.Client.Deliver(Range(9));
            Assert.AreEqual((30L, 60L, 8L, 20L, 6, 4), (s.Hp, s.MaxHp, s.Sp, s.MaxSp, s.BaseLevel, s.JobLevel));
            Assert.AreEqual((5_000_000_000L, 9L, 350L, 10L, 9), (s.BaseExp, s.JobExp, s.NextBaseExp, s.NextJobExp, s.AttackRange));
            Assert.AreEqual((30, 60), (rig.Get(rig.Model.OwnId).Hp, rig.Get(rig.Model.OwnId).MaxHp));
            Assert.Greater(s.Version, version);
            version = s.Version;
            rig.Client.Deliver(Parameter(5, 30));
            Assert.AreEqual(version, s.Version, "the same value changes nothing");
            rig.Client.Deliver(TooFar(Poring, 5));
            Assert.AreEqual(5, s.AttackRange, "the range of a too-far answer counts too");
        }

        [Test, Ignore("pending T2.3")]
        public void OwnStatus_BeforeTheMapIsLoaded()
        {
            using var rig = new Rig().ToLoadingMap(CharacterWith(40, 50, 10, 12, 5, 3, 7, 2));
            rig.Client.Deliver(Parameter(5, 33));
            rig.Client.Deliver(Range(2));
            rig.Session.BeginMap(rig.Grid);
            Assert.AreEqual((33L, 2), (rig.Session.Status.Hp, rig.Session.Status.AttackRange), "the character data do not overwrite what the server said since");
            Assert.AreEqual(33, rig.Get(rig.Model.OwnId).Hp);
        }

        [Test, Ignore("pending T2.3")]
        public void OwnStatus_FakeHpWhileDead_StaysDead()
        {
            using var rig = new Rig().ToInGame(CharacterWith(40, 50, 10, 12, 5, 3, 7, 2));
            uint own = rig.Model.OwnId;
            rig.Client.Deliver(VanishPacket(own, (byte)VanishReason.Died));
            rig.Client.Deliver(Parameter(5, 25)); // display_fake_hp_when_dead
            Assert.AreEqual(EntityState.Dead, rig.Get(own).State);
            Assert.AreEqual(25L, rig.Session.Status.Hp);
        }

        [Test, Ignore("pending T2.3")]
        public void OwnStatus_KeptAcrossMaps()
        {
            using var rig = new Rig().ToInGame(CharacterWith(40, 50, 10, 12, 5, 3, 7, 2));
            rig.Client.Deliver(Parameter(5, 12));
            rig.Client.Deliver(LongParameter(1, 77));
            rig.Client.Deliver(Range(9));
            rig.Session.BeginMap(rig.Grid); // another map
            var s = rig.Session.Status;
            Assert.AreEqual((12L, 77L, 9), (s.Hp, s.BaseExp, s.AttackRange));
            Assert.AreEqual(12, rig.Get(rig.Model.OwnId).Hp);
        }

        // ---- the session's handlers (FR-1, FR-9) ----

        [Test, Ignore("pending T2.3")]
        public void WorldSession_Hp_FromEachEntry()
        {
            using var rig = new Rig().ToInGame();
            rig.Client.Deliver(Stand(Poring, 50, 50));
            Assert.AreEqual((1000, 10000), (rig.Get(Poring).Hp, rig.Get(Poring).MaxHp));
            rig.Client.Deliver(New(Poring + 1, 51, 50));
            Assert.AreEqual((1000, 10000), (rig.Get(Poring + 1).Hp, rig.Get(Poring + 1).MaxHp));
            rig.Client.Deliver(MoveEntry(Poring + 2, 52, 50, 55, 50, rig.Clock.Now));
            Assert.AreEqual((1000, 10000), (rig.Get(Poring + 2).Hp, rig.Get(Poring + 2).MaxHp));
        }

        [Test, Ignore("pending T2.3")]
        public void WorldSession_Damage_StrikesAndRaises()
        {
            using var rig = new Rig().ToInGame();
            uint own = rig.Model.OwnId;
            rig.Client.Deliver(Stand(Poring, 160, 190));
            Blow seen = default;
            int count = 0;
            rig.Session.BlowReceived += (in Blow b) => { seen = b; count++; };
            rig.Client.Deliver(Damage(Poring, own, 25, TypeCritical, 1, 912, 288));
            Assert.AreEqual(1, count);
            Assert.AreEqual((Poring, own, 25, BlowKind.Critical, rig.Clock.Now), (seen.AttackerId, seen.TargetId, seen.Damage, seen.Kind, seen.At));
            Assert.AreEqual(own, rig.Get(Poring).TargetId);

            long gained = 0;
            rig.Session.ExpGained += (var, amount) => gained += amount;
            rig.Client.Deliver(new EXP_GAIN(Hex(Le(own) + Le(17) + "00000000" + "0100" + "0000")));
            Assert.AreEqual(17L, gained);
        }

        [Test, Ignore("pending T2.3")]
        public void WorldSession_Resurrection_StandsUp()
        {
            using var rig = new Rig().ToInGame();
            uint own = rig.Model.OwnId;
            rig.Client.Deliver(VanishPacket(own, (byte)VanishReason.Died));
            rig.Client.Deliver(Raise(own));
            Assert.AreEqual(EntityState.Standing, rig.Get(own).State);
        }

        [Test, Ignore("pending T2.3")]
        public void WorldSession_ThousandDamageAndStatusPackets_AllocateNothing()
        {
            using var rig = new Rig().ToInGame();
            uint own = rig.Model.OwnId;
            rig.Client.Deliver(Stand(Poring, 160, 190));
            var hit = Damage(Poring, own, 25);
            var back = Damage(own, Poring, 30, TypeCritical);
            var hp = Parameter(5, 40);
            var exp = LongParameter(1, 99);
            var entry = Stand(Poring, 160, 190);
            Action run = () =>
            {
                for (int i = 0; i < 1000; i++)
                {
                    rig.Client.Deliver(hit);
                    rig.Client.Deliver(back);
                    rig.Client.Deliver(hp);
                    rig.Client.Deliver(exp);
                    rig.Client.Deliver(entry);
                }
            };
            run();
            Assert.That(() => run(), UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory());
        }
    }
}
