using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Uninarok.Networking.Data;

namespace Uninarok.World.Tests
{
    // SPEC-0011 FR-4, FR-5, FR-9, FR-10 (PLAN-0011 4.6): what is shown of each blow, and when, on a model with no scene.
    // The own player P stands at (10, 10), a monster M at (11, 10), another player Q at (10, 12); times start at 10,000.
    public class CombatTimelineTests
    {
        sealed class Grid : IWalkGrid
        {
            public int Width => 64;
            public int Height => 64;
            public bool IsPassable(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
            public bool BlocksSight(int x, int y) => !IsPassable(x, y);
        }

        const uint P = 2000000, Q = 2000003, M = 0x068EB5EC, N = 0x068EB5ED;
        const uint T = 10_000;

        WorldSettings _world;
        CombatSettings _settings;
        WorldModel _model;
        CombatTimeline _timeline;
        int _p, _q, _m;

        [SetUp]
        public void SetUp()
        {
            _world = ScriptableObject.CreateInstance<WorldSettings>();
            _settings = ScriptableObject.CreateInstance<CombatSettings>();
            _model = new WorldModel(new Grid(), _world, 64);
            _p = _model.Upsert(P, EntityKind.Player, default, 150, 10, 10, UninarokDirection.South, HeadDirection.Straight, EntityState.Standing);
            _q = _model.Upsert(Q, EntityKind.Player, default, 150, 10, 12, UninarokDirection.South, HeadDirection.Straight, EntityState.Standing);
            _m = _model.Upsert(M, EntityKind.Monster, new EntityLook { Job = 1002 }, 400, 11, 10, UninarokDirection.West, HeadDirection.Straight, EntityState.Standing);
            _model.OwnId = P;
            _timeline = new CombatTimeline(_settings, _model.Capacity);
            _timeline.Use(_model);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_settings);
            Object.DestroyImmediate(_world);
        }

        static Blow Hit(uint attacker, uint target, uint at, int damage, int attackMs = 500, int hurtMs = 300, BlowKind kind = BlowKind.Normal,
                        short hits = 1, int left = 0, bool endured = false) =>
            new Blow(attacker, target, at, attackMs, hurtMs, damage, left, hits, kind, endured, 0);

        void Add(in Blow blow) => _timeline.Add(blow);

        void At(uint now)
        {
            _model.Advance(now);
            _timeline.Advance(now);
        }

        MotionKind KindOf(int slot) => _timeline.MotionOf(slot).Kind;

        List<DamageNumber> NumbersNow()
        {
            var list = new List<DamageNumber>();
            foreach (var n in _timeline.Numbers) list.Add(n);
            return list;
        }

        // ---- FR-4: motions and hit moments ----

        [Test]
        public void CombatTimeline_AttackLasts500_HitAt500_Hurt300()
        {
            Add(Hit(P, M, T, 10));
            At(T);
            var attack = _timeline.MotionOf(_p);
            Assert.AreEqual((MotionKind.Attack, T, 500), (attack.Kind, attack.Starts, attack.DurationMs));
            Assert.That(attack.AttackNumber, Is.InRange(1, 3));
            Assert.AreEqual(MotionKind.None, KindOf(_m));
            At(T + 499);
            Assert.AreEqual(0, NumbersNow().Count, "nothing before the hit moment");
            Assert.AreEqual(MotionKind.None, KindOf(_m));

            At(T + 500);
            var numbers = NumbersNow();
            Assert.AreEqual(1, numbers.Count);
            Assert.AreEqual((T + 500, M, 10, NumberKind.Normal, false), (numbers[0].Starts, numbers[0].TargetId, numbers[0].Value, numbers[0].Kind, numbers[0].OnPlayer));
            Assert.AreEqual((11f, 10f), (numbers[0].X, numbers[0].Y), "where the target is shown at the hit");
            var hurt = _timeline.MotionOf(_m);
            Assert.AreEqual((MotionKind.Hurt, T + 500, 300), (hurt.Kind, hurt.Starts, hurt.DurationMs));
            Assert.AreEqual(MotionKind.Standby, KindOf(_p), "a player stands by after an attack (D11)");

            At(T + 800);
            Assert.AreEqual(MotionKind.None, KindOf(_m), "the hurt is over");
            Assert.AreEqual(MotionKind.Standby, KindOf(_p), "until it does something else");
        }

        [Test]
        public void CombatTimeline_EnduredHasNumberNoHurt()
        {
            Add(Hit(P, M, T, 25, endured: true));
            At(T + 500);
            Assert.AreEqual(1, NumbersNow().Count);
            Assert.AreEqual(MotionKind.None, KindOf(_m));
        }

        [Test]
        public void CombatTimeline_MissNoHurt()
        {
            Add(Hit(M, P, T, 0));
            At(T);
            Assert.AreEqual(MotionKind.Attack, KindOf(_m), "a monster's attack plays though it misses");
            At(T + 500);
            var numbers = NumbersNow();
            Assert.AreEqual(1, numbers.Count);
            Assert.AreEqual((NumberKind.Miss, P, true), (numbers[0].Kind, numbers[0].TargetId, numbers[0].OnPlayer));
            Assert.AreEqual(MotionKind.None, KindOf(_p));
        }

        [Test]
        public void CombatTimeline_WalkKeepsPosition()
        {
            _model.Walk(M, 11, 10, 16, 10, T, T);
            Add(Hit(P, M, T, 10));
            At(T + 500);
            Assert.AreEqual(MotionKind.Hurt, KindOf(_m));
            float x = _model[_m].PosX;
            At(T + 700);
            Assert.IsTrue(_model[_m].Walking);
            Assert.Greater(_model[_m].PosX, x, "the walk goes on underneath the hurt");
            Assert.AreEqual(MotionKind.Hurt, KindOf(_m), "a walk does not end a hurt");
            Assert.AreEqual(x, NumbersNow()[0].X, "the number stays where the target was at the hit");

            Add(Hit(Q, M, T + 1000, 10));
            At(T + 1000);
            Assert.AreEqual(MotionKind.Attack, KindOf(_q));
            _model.Walk(Q, 10, 12, 10, 18, T + 1100, T + 1100);
            At(T + 1150);
            Assert.AreEqual(MotionKind.None, KindOf(_q), "a walk that starts ends an attack motion");
        }

        [Test]
        public void CombatTimeline_NewBlowRestarts()
        {
            Add(Hit(P, M, T, 10));
            At(T);
            int version = _timeline.MotionOf(_p).Version;
            Add(Hit(P, M, T + 300, 10));
            At(T + 300);
            var motion = _timeline.MotionOf(_p);
            Assert.AreEqual((MotionKind.Attack, T + 300), (motion.Kind, motion.Starts));
            Assert.AreNotEqual(version, motion.Version);
            At(T + 800);
            Assert.AreEqual(2, NumbersNow().Count, "both blows hit");
        }

        [Test]
        public void CombatTimeline_SlowMonster1320_HitAt1320()
        {
            Add(Hit(M, P, T, 40, attackMs: 1320, hurtMs: 288));
            At(T);
            Assert.AreEqual((MotionKind.Attack, 1320), (KindOf(_m), _timeline.MotionOf(_m).DurationMs));
            At(T + 1319);
            Assert.AreEqual(0, NumbersNow().Count, "not at the server's 1,000 ms cap (D3)");
            At(T + 1320);
            Assert.AreEqual(1, NumbersNow().Count);
            Assert.AreEqual((MotionKind.Hurt, T + 1320), (KindOf(_p), _timeline.MotionOf(_p).Starts));
        }

        [Test]
        public void CombatTimeline_SelfDamage()
        {
            Add(Hit(P, P, T, 5, attackMs: 0, hurtMs: 0));
            At(T);
            var numbers = NumbersNow();
            Assert.AreEqual(1, numbers.Count, "at once (H11)");
            Assert.AreEqual((5, true), (numbers[0].Value, numbers[0].OnPlayer));
            Assert.AreEqual(MotionKind.None, KindOf(_p), "no attack on oneself, no hurt with a hurt time of 0");

            Add(Hit(P, P, T + 100, -20, attackMs: 0, hurtMs: 0));
            At(T + 100);
            Assert.AreEqual(1, NumbersNow().Count, "a heal shows nothing here");
        }

        // ---- FR-5: which numbers ----

        [Test]
        public void CombatTimeline_Kinds()
        {
            var rows = new (Blow blow, NumberKind? kind)[]
            {
                (Hit(P, M, T, 12), NumberKind.Normal),
                (Hit(P, M, T, 30, kind: BlowKind.Critical), NumberKind.Critical),
                (Hit(P, M, T, 0), NumberKind.Miss),
                (Hit(M, P, T, 0, kind: BlowKind.LuckyDodge), NumberKind.Lucky),
                (Hit(P, M, T, -5), null),
            };
            foreach (var r in rows)
            {
                _timeline.Clear();
                Add(r.blow);
                At(T + 500);
                var numbers = NumbersNow();
                if (r.kind == null)
                {
                    Assert.AreEqual(0, numbers.Count, "negative damage");
                    continue;
                }
                Assert.AreEqual(1, numbers.Count, r.kind.ToString());
                Assert.AreEqual(r.kind.Value, numbers[0].Kind);
                Assert.AreEqual(r.blow.Damage > 0 ? r.blow.Damage : 0, numbers[0].Value);
                bool hurt = r.kind == NumberKind.Normal || r.kind == NumberKind.Critical;
                Assert.AreEqual(hurt, KindOf(r.blow.TargetId == M ? _m : _p) == MotionKind.Hurt, r.kind + ": hurt");
            }
        }

        [Test]
        public void CombatTimeline_DoubleAttack300_150_150_300()
        {
            Add(Hit(P, M, T, 300, kind: BlowKind.MultiHit, hits: 2));
            At(T + 500);
            var numbers = NumbersNow();
            Assert.AreEqual(1, numbers.Count);
            Assert.AreEqual((150, NumberKind.Normal), (numbers[0].Value, numbers[0].Kind));
            At(T + 699);
            Assert.AreEqual(1, NumbersNow().Count);
            At(T + 700);
            numbers = NumbersNow();
            Assert.AreEqual(3, numbers.Count, "the second part and the total, 200 ms later");
            Assert.That(numbers.Exists(n => n.Kind == NumberKind.Normal && n.Value == 150 && n.Starts == T + 700));
            Assert.That(numbers.Exists(n => n.Kind == NumberKind.Total && n.Value == 300 && n.Starts == T + 700));
            Assert.AreEqual((MotionKind.Hurt, T + 700), (KindOf(_m), _timeline.MotionOf(_m).Starts), "each part starts its own hurt");
        }

        [Test]
        public void CombatTimeline_DualWield_TwoNumbers()
        {
            Add(Hit(P, M, T, 100, left: 40));
            At(T + 500);
            Assert.AreEqual(1, NumbersNow().Count);
            At(T + 849);
            Assert.AreEqual(1, NumbersNow().Count);
            At(T + 850);
            var numbers = NumbersNow();
            Assert.AreEqual(2, numbers.Count);
            Assert.That(numbers.Exists(n => n.Value == 40 && n.Starts == T + 850), "the second hand 350 ms later");
        }

        [Test]
        public void CombatTimeline_SplitAlwaysSums()
        {
            _settings.MultiHitMs = 0;
            foreach (int damage in new[] { 1, 7, 97, 1000, 12345 })
            {
                for (short hits = 1; hits <= 100; hits++)
                {
                    _timeline.Clear();
                    Add(Hit(P, M, T, damage, kind: BlowKind.MultiHit, hits: hits));
                    At(T + 500);
                    int sum = 0, parts = 0, total = -1;
                    foreach (var n in _timeline.Numbers)
                    {
                        if (n.Kind == NumberKind.Total) total = n.Value;
                        else
                        {
                            sum += n.Value;
                            parts++;
                        }
                    }
                    Assert.AreEqual(damage, sum, $"{damage} in {hits}");
                    Assert.AreEqual(damage < hits ? 1 : hits, parts, $"{damage} in {hits}: a total smaller than the hits is one number");
                    if (parts > 1) Assert.AreEqual(damage, total, $"{damage} in {hits}");
                }
            }
        }

        // ---- FR-9, FR-10: death after the hit, pickup ----

        [Test]
        public void CombatTimeline_DeathHeldUntilHit()
        {
            Add(Hit(P, M, T, 50));
            At(T);
            _model.Vanish(M, VanishReason.Died, T + 200);
            At(T + 300);
            Assert.AreEqual(EntityState.Dead, _model[_m].State, "the model changes at once (D2)");
            Assert.IsTrue(_timeline.HoldsDeath(_m, T + 300), "a hit is pending");
            At(T + 500);
            Assert.AreEqual(MotionKind.Hurt, KindOf(_m));
            Assert.IsTrue(_timeline.HoldsDeath(_m, T + 749));
            Assert.IsFalse(_timeline.HoldsDeath(_m, T + 750), "250 ms after the last hit moment (H9)");

            // A slow monster's notice can come before its hit moment (H8): a hit at 1,320, the notice at 1,000.
            int n = _model.Upsert(N, EntityKind.Monster, new EntityLook { Job = 1037 }, 400, 12, 12, UninarokDirection.West, HeadDirection.Straight, EntityState.Standing);
            Add(Hit(M, N, T + 2000, 99, attackMs: 1320));
            Add(Hit(P, N, T + 2000, 99, attackMs: 1320));
            _model.Vanish(N, VanishReason.Died, T + 3000);
            At(T + 3000);
            Assert.IsTrue(_timeline.HoldsDeath(n, T + 3319));
            At(T + 3320);
            Assert.AreEqual(MotionKind.Hurt, KindOf(n));
            Assert.IsTrue(_timeline.HoldsDeath(n, T + 3569));
            Assert.IsFalse(_timeline.HoldsDeath(n, T + 3570));
        }

        [Test]
        public void CombatTimeline_MonsterHurt250()
        {
            Add(Hit(P, M, T, 50, hurtMs: 400));
            At(T + 500);
            _model.Vanish(M, VanishReason.Died, T + 750); // as the server sends it
            At(T + 750);
            Assert.IsFalse(_timeline.HoldsDeath(_m, T + 750), "the hurt had its 250 ms");
            Assert.IsFalse(_timeline.HoldsDeath(_q, T + 750), "not dead");
        }

        [Test]
        public void CombatTimeline_OwnDeath()
        {
            Add(Hit(M, P, T, 500, attackMs: 600));
            At(T);
            _model.Vanish(P, VanishReason.Died, T + 300);
            At(T + 300);
            Assert.IsTrue(_timeline.HoldsDeath(_p, T + 300), "the hit is pending");
            At(T + 600);
            Assert.IsFalse(_timeline.HoldsDeath(_p, T + 600), "a player dies at its hit (0 ms)");
            Assert.AreEqual(MotionKind.None, KindOf(_p), "death ends every motion");
        }

        [Test]
        public void CombatTimeline_PickUpMotion()
        {
            _model.AddItem(9001, 501, true, 11, 11, 6, 6, 1);
            _timeline.AddPickUp(Q, 9001, T);
            At(T);
            var motion = _timeline.MotionOf(_q);
            Assert.AreEqual((MotionKind.PickUp, T, 0), (motion.Kind, motion.Starts, motion.DurationMs), "once, at its own speed");
            _timeline.AddPickUp(M, 9001, T);
            At(T);
            Assert.AreEqual(MotionKind.None, KindOf(_m), "monsters that loot have no motion");
            _model.Walk(Q, 10, 12, 10, 18, T + 100, T + 100);
            At(T + 150);
            Assert.AreEqual(MotionKind.None, KindOf(_q), "a walk ends it");
        }
    }
}
