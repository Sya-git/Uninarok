using System;
using Uninarok.Networking.Client;

namespace Uninarok.World
{
    /// <summary>A motion a unit shows over its base animation (PLAN-0011 4.6).</summary>
    public enum MotionKind : byte
    {
        None,
        Attack,
        Hurt,
        PickUp,
        Standby,
    }

    /// <summary>The motion of a model slot: what, from when, for how long (0: at its own speed; −1: until it ends).</summary>
    public struct UnitMotion
    {
        public MotionKind Kind;
        public uint Starts;
        public int DurationMs;
        /// <summary>Changes with each new motion, so the presenter plays it once.</summary>
        public int Version;
        /// <summary>1 to 3: the player's attack animation (PLAN-0011 4.7).</summary>
        public byte AttackNumber;
    }

    public enum NumberKind : byte
    {
        Normal,
        Critical,
        Miss,
        Lucky,
        /// <summary>The running sum of a multi-hit, from its first part, replaced by each later number of the blow (D32).</summary>
        Total,
    }

    /// <summary>
    /// A number or a message shown over a unit (FR-5, FR-6): from when, over whom (the blow's target; for a miss its attacker,
    /// D31), where the unit was shown then.
    /// </summary>
    public struct DamageNumber
    {
        public uint Starts;
        public uint TargetId;
        public int Value;
        public NumberKind Kind;
        /// <summary>Over a player (red), else a monster (white).</summary>
        public bool OnPlayer;
        public float X, Y;
    }

    public enum ProjectileKind : byte
    {
        None,
        Arrow,
        /// <summary>Launched and timed, drawn by SPEC-0013.</summary>
        Other,
    }

    /// <summary>A projectile of a blow (FR-14): when it is launched and arrives, from and to the shown positions at the launch.</summary>
    public struct Projectile
    {
        public uint Launches, Arrives;
        public uint AttackerId, TargetId;
        public float FromX, FromY, ToX, ToY;
        public ProjectileKind Kind;
    }

    /// <summary>
    /// Both hands the server sends (<c>Look.Weapon</c>, <c>Look.Shield</c>) of a job to one weapon type, 0 (bare hand) to 30
    /// (PLAN-0011 4.7, D21): the hands rule of PLAN-0009 4.11.
    /// </summary>
    public interface IWeaponTypes
    {
        int TypeOf(int job, int right, int left);
    }

    /// <summary>The job and sex of the attack row a job uses (PLAN-0011 4.7, D20, D24): the family table of FR-15.</summary>
    public interface IJobRows
    {
        int RowJobOf(int job);
        byte RowSexOf(int job, byte sex);
    }

    /// <summary>
    /// What is shown of each blow, and when (SPEC-0011 FR-4 to FR-6, FR-9, FR-10, FR-14; PLAN-0011 4.6): motions, numbers and
    /// projectiles from the blows of the model, later than the packets (D2). It reads the model and never changes it. Arrays are
    /// allocated once; nothing allocates per blow or per frame (FR-12). Main thread.
    /// </summary>
    public sealed class CombatTimeline
    {
        // One number to show at a hit moment, and whether it starts a hurt there.
        struct PendingHit
        {
            public uint At;
            public uint TargetId;
            public int Value;
            public NumberKind Kind;
            public bool Hurts;
            public int HurtMs;
        }

        readonly CombatSettings _settings;
        readonly PendingHit[] _hits;
        readonly DamageNumber[] _numbers;
        readonly Projectile[] _projectiles;
        readonly UnitMotion[] _motions;
        // Per slot: the entity generation the motion belongs to, and whether the unit walked when its motion started.
        readonly int[] _generations;
        readonly bool[] _walkedAtStart;
        // Per slot: the last hit moment shown on the unit, and the generation it was shown on (FR-9).
        readonly uint[] _lastHitAt;
        readonly int[] _lastHitGeneration;
        readonly bool[] _hasLastHit;
        // Per slot: the death is shown (the hold is over), for that generation; a later hit neither hurts nor holds it again.
        readonly bool[] _deathShown;
        readonly int[] _deathGeneration;
        int _hitCount, _numberCount, _projectileCount;
        int _version;
        bool _warnedFull;
        WorldModel _model;

        public CombatTimeline(CombatSettings settings, int slots, int hits = 1024, int numbers = 512, int projectiles = 128)
        {
            _settings = settings != null ? settings : throw new ArgumentNullException(nameof(settings));
            if (slots <= 0) throw new ArgumentOutOfRangeException(nameof(slots));
            _hits = new PendingHit[Math.Max(1, hits)];
            _numbers = new DamageNumber[Math.Max(1, numbers)];
            _projectiles = new Projectile[Math.Max(1, projectiles)];
            _motions = new UnitMotion[slots];
            _generations = new int[slots];
            _walkedAtStart = new bool[slots];
            _lastHitAt = new uint[slots];
            _lastHitGeneration = new int[slots];
            _hasLastHit = new bool[slots];
            _deathShown = new bool[slots];
            _deathGeneration = new int[slots];
        }

        /// <summary>Until the catalog is loaded every weapon type is 0.</summary>
        public IWeaponTypes Weapons { get; set; }

        /// <summary>Until the catalog is loaded every job stands for itself.</summary>
        public IJobRows Jobs { get; set; }

        /// <summary>The world whose units the blows are of; a new map's model forgets everything.</summary>
        public void Use(WorldModel model)
        {
            Clear();
            _model = model;
        }

        /// <summary>A blow as the model received it (FR-4): the attacker's motion now, the numbers and hurts at the hit moment.</summary>
        public void Add(in Blow blow)
        {
            var model = _model;
            if (model == null) return;

            // D23: one both-hands type per blow, for the motion, the hit frame and the projectile.
            bool known = model.TryGetSlot(blow.AttackerId, out int attacker);
            int weapon = known ? WeaponTypeOf(model[attacker]) : 0;

            if (blow.AttackerId != blow.TargetId && blow.AttackMs > 0 && known &&
                attacker < _motions.Length && model[attacker].State != EntityState.Dead)
                Start(model, attacker, MotionKind.Attack, blow.At, blow.AttackMs, AttackNumber(model[attacker], weapon));

            // H11: an attack time of 0 or less is at once.
            uint hit = unchecked(blow.At + (uint)Math.Max(0, blow.AttackMs));
            // 4.7, D33: a player's number waits launch + FlightMs.
            if (AddProjectile(model, blow, weapon, out uint numberAt)) hit = numberAt;
            AddNumbers(blow, hit);
        }

        // FR-14: one projectile per blow, also a multi-hit, between the shown positions (taken again at the launch).
        // weapon: the both-hands type of the attacker (D23).
        bool AddProjectile(WorldModel model, in Blow blow, int weapon, out uint numberAt)
        {
            numberAt = 0;
            if (blow.AttackerId == blow.TargetId || !model.TryGetSlot(blow.AttackerId, out int attacker) || !model.TryGetSlot(blow.TargetId, out int target))
                return false;
            ref readonly var a = ref model[attacker];
            ref readonly var t = ref model[target];
            var kind = ProjectileRules.Of(a.Kind, a.Look.Job, weapon);
            if (kind == ProjectileKind.None) return false;

            bool player = a.Kind == EntityKind.Player;
            float dx = t.PosX - a.PosX, dy = t.PosY - a.PosY;
            float hitFrame = player ? AttackMotions.HitFrameOf(RowJobOf(a.Look.Job), RowSexOf(a.Look.Job, a.Look.Sex), weapon) : 0f;
            ProjectileRules.Times(blow, player, (float)Math.Sqrt(dx * dx + dy * dy), hitFrame, _settings.FlightMs, _settings.FlightFullCells,
                                  out uint launches, out uint lands, out bool waits);
            if (_projectileCount == _projectiles.Length) DropOldest(_projectiles, ref _projectileCount, "projectiles");
            _projectiles[_projectileCount++] = new Projectile
            {
                Launches = launches, Arrives = lands, AttackerId = blow.AttackerId, TargetId = blow.TargetId,
                FromX = a.PosX, FromY = a.PosY, ToX = t.PosX, ToY = t.PosY, Kind = kind,
            };
            // D33 (footage B): the number at launch + the full flight, whatever the distance; the arrow is gone at `lands`.
            if (waits) numberAt = ProjectileRules.NumberAt(launches, _settings.FlightMs);
            return waits;
        }

        // Until its launch a projectile takes the shown positions again; it is gone when it arrives.
        void AdvanceProjectiles(WorldModel model, uint now)
        {
            for (int i = 0; i < _projectileCount;)
            {
                ref var p = ref _projectiles[i];
                if (ServerClock.Diff(now, p.Arrives) >= 0)
                {
                    _projectiles[i] = _projectiles[--_projectileCount];
                    continue;
                }
                if (ServerClock.Diff(now, p.Launches) < 0)
                {
                    if (model.TryGetSlot(p.AttackerId, out int a))
                    {
                        p.FromX = model[a].PosX;
                        p.FromY = model[a].PosY;
                    }
                    if (model.TryGetSlot(p.TargetId, out int t))
                    {
                        p.ToX = model[t].PosX;
                        p.ToY = model[t].PosY;
                    }
                }
                i++;
            }
        }

        // FR-5, the table of 4.6.
        void AddNumbers(in Blow blow, uint hit)
        {
            if (blow.Damage < 0) return; // heals are SPEC-0012's
            bool hurts = Hurts(blow);
            // Lucky over the dodging unit (the target); a miss over the unit that missed, the attacker (D31, footage A).
            if (blow.Kind == BlowKind.LuckyDodge) Pend(hit, blow.TargetId, 0, NumberKind.Lucky, false, 0);
            else if (blow.IsMiss) Pend(hit, blow.AttackerId, 0, NumberKind.Miss, false, 0);
            // D28, D32: a running total for a split multi-hit over a target that is not a player, the second hand included.
            bool totals = false;
            int sum = 0;
            if (!blow.IsMiss && blow.Kind != BlowKind.LuckyDodge && blow.Damage > 0)
            {
                var kind = blow.Kind == BlowKind.Critical ? NumberKind.Critical : NumberKind.Normal;
                int hits = blow.Kind == BlowKind.MultiHit ? blow.Hits : 1;
                if (hits <= 1 || blow.Damage < hits) Pend(hit, blow.TargetId, blow.Damage, kind, hurts, blow.HurtMs);
                else
                {
                    totals = !TargetIsPlayer(blow.TargetId);
                    // Each part damage / n, the remainder on the last: they always sum to the blow's damage.
                    int part = blow.Damage / hits;
                    for (int i = 0; i < hits; i++)
                    {
                        uint at = unchecked(hit + (uint)(i * Math.Max(0, _settings.MultiHitMs)));
                        int value = i == hits - 1 ? blow.Damage - part * (hits - 1) : part;
                        sum += value;
                        Pend(at, blow.TargetId, value, kind, hurts, blow.HurtMs);
                        if (totals) Pend(at, blow.TargetId, sum, NumberKind.Total, false, 0);
                    }
                }
            }
            if (blow.LeftDamage > 0)
            {
                uint at = unchecked(hit + (uint)Math.Max(0, _settings.LeftHandMs));
                Pend(at, blow.TargetId, blow.LeftDamage, NumberKind.Normal, !blow.Endured && blow.HurtMs > 0, blow.HurtMs);
                if (totals) Pend(at, blow.TargetId, (int)Math.Min(int.MaxValue, (long)sum + blow.LeftDamage), NumberKind.Total, false, 0);
            }
        }

        bool TargetIsPlayer(uint id) => _model != null && _model.TryGetSlot(id, out int slot) && _model[slot].Kind == EntityKind.Player;

        /// <summary>A unit picked up an item (UNIT_ACT 1): a player bends down once (FR-10).</summary>
        public void AddPickUp(uint id, uint itemId, uint now)
        {
            var model = _model;
            if (model == null || !model.TryGetSlot(id, out int slot) || slot >= _motions.Length) return;
            ref readonly var e = ref model[slot];
            if (e.Kind != EntityKind.Player || e.State != EntityState.Standing) return; // monsters that loot have no such animation
            Start(model, slot, MotionKind.PickUp, now, 0, 0);
        }

        /// <summary>Shows what is due at this server time and ends what is over.</summary>
        /// <summary>The server time of the last <see cref="Advance"/>.</summary>
        public uint Now { get; private set; }

        public void Advance(uint now)
        {
            Now = now;
            var model = _model;
            if (model == null) return;

            for (int i = 0; i < _hitCount;)
            {
                if (ServerClock.Diff(now, _hits[i].At) < 0)
                {
                    i++;
                    continue;
                }
                Show(model, _hits[i]);
                _hits[i] = _hits[--_hitCount];
            }

            for (int i = 0; i < _numberCount;)
            {
                if (ServerClock.Diff(now, _numbers[i].Starts) >= LifeOf(_numbers[i].Kind)) _numbers[i] = _numbers[--_numberCount];
                else i++;
            }

            AdvanceProjectiles(model, now);

            int limit = Math.Min(model.SlotLimit, _motions.Length);
            for (int slot = 0; slot < limit; slot++) Keep(model, slot, now);
        }

        public ref readonly UnitMotion MotionOf(int slot) => ref _motions[slot];

        /// <summary>FR-9: the unit is dead in the model but a hit that came before its death notice is not shown yet.</summary>
        /// <remarks>
        /// True while a hit on it is pending, and until <c>HurtBeforeDeathMs</c> (players: <c>PlayerHurtBeforeDeathMs</c>) after its
        /// last hit moment (H9). It delays neither the model nor the removal.
        /// </remarks>
        public bool HoldsDeath(int slot, uint now)
        {
            var model = _model;
            if (model == null || slot < 0 || slot >= _motions.Length) return false;
            ref readonly var e = ref model[slot];
            if (e.Id == 0 || e.State != EntityState.Dead || DeathShown(slot, e)) return false;
            return PendingOn(e.Id) > 0 || InHold(slot, e, now);
        }

        bool DeathShown(int slot, in Entity e) => _deathShown[slot] && _deathGeneration[slot] == e.Generation;

        int PendingOn(uint id)
        {
            int count = 0;
            for (int i = 0; i < _hitCount; i++)
                if (_hits[i].TargetId == id && _hits[i].Kind != NumberKind.Miss) count++; // a miss is over its attacker (D31)
            return count;
        }

        bool InHold(int slot, in Entity e, uint now)
        {
            if (!_hasLastHit[slot] || _lastHitGeneration[slot] != e.Generation) return false;
            int hold = e.Kind == EntityKind.Player ? _settings.PlayerHurtBeforeDeathMs : _settings.HurtBeforeDeathMs;
            return ServerClock.Diff(now, _lastHitAt[slot]) < hold;
        }

        /// <summary>The numbers shown now.</summary>
        public ReadOnlySpan<DamageNumber> Numbers => new ReadOnlySpan<DamageNumber>(_numbers, 0, _numberCount);

        /// <summary>The projectiles not yet arrived: drawn from their launch (<c>Launches</c> ≤ now) to their arrival.</summary>
        public ReadOnlySpan<Projectile> Projectiles => new ReadOnlySpan<Projectile>(_projectiles, 0, _projectileCount);

        public void Clear()
        {
            _hitCount = _numberCount = _projectileCount = 0;
            Array.Clear(_motions, 0, _motions.Length);
            Array.Clear(_generations, 0, _generations.Length);
            Array.Clear(_walkedAtStart, 0, _walkedAtStart.Length);
            Array.Clear(_hasLastHit, 0, _hasLastHit.Length);
            Array.Clear(_deathShown, 0, _deathShown.Length);
        }

        // ---- rules ----

        // FR-4: a hurt unless no damage, endured, a lucky dodge or no hurt time.
        static bool Hurts(in Blow blow) => blow.Damage > 0 && !blow.Endured && blow.Kind != BlowKind.LuckyDodge && blow.HurtMs > 0;

        // 4.7, D21: a player's type of both hands (the server's job, Look.Weapon, Look.Shield); others and no catalog: 0.
        int WeaponTypeOf(in Entity e)
        {
            var weapons = Weapons;
            return e.Kind == EntityKind.Player && weapons != null ? weapons.TypeOf(e.Look.Job, e.Look.Weapon, e.Look.Shield) : 0;
        }

        // FR-15 (D20, D24): the row job and row sex; without the catalog a job stands for itself.
        int RowJobOf(int job)
        {
            var jobs = Jobs;
            return jobs != null ? jobs.RowJobOf(job) : job;
        }

        byte RowSexOf(int job, byte sex)
        {
            var jobs = Jobs;
            return jobs != null ? jobs.RowSexOf(job, sex) : sex;
        }

        // 4.7: players by the row job, row sex and the blow's type of both hands (D23); others always Attack.
        byte AttackNumber(in Entity e, int weapon)
        {
            if (e.Kind != EntityKind.Player) return 1;
            return (byte)AttackMotions.NumberOf(RowJobOf(e.Look.Job), RowSexOf(e.Look.Job, e.Look.Sex), weapon);
        }

        int LifeOf(NumberKind kind) => NumberMotion.LifeMs(kind, _settings);

        void Pend(uint at, uint targetId, int value, NumberKind kind, bool hurts, int hurtMs)
        {
            if (_hitCount == _hits.Length) DropOldest(_hits, ref _hitCount, "pending hits");
            _hits[_hitCount++] = new PendingHit { At = at, TargetId = targetId, Value = value, Kind = kind, Hurts = hurts, HurtMs = hurtMs };
        }

        // The hit moment: the number where its unit (the target; for a miss the attacker, D31) is shown now, and the target's
        // hurt. No number without a known unit.
        void Show(WorldModel model, in PendingHit hit)
        {
            if (!model.TryGetSlot(hit.TargetId, out int target)) return;
            ref readonly var t = ref model[target];
            if (hit.Kind == NumberKind.Total && !ReplaceTotal(hit)) return;
            if (_numberCount == _numbers.Length) DropOldest(_numbers, ref _numberCount, "numbers");
            _numbers[_numberCount++] = new DamageNumber
            {
                Starts = hit.At, TargetId = hit.TargetId, Value = hit.Value, Kind = hit.Kind, OnPlayer = t.Kind == EntityKind.Player, X = t.PosX, Y = t.PosY,
            };
            if (target >= _motions.Length || hit.Kind == NumberKind.Miss) return; // a miss is no hit on the unit it is over
            bool deathShown = t.State == EntityState.Dead && DeathShown(target, t);
            _lastHitAt[target] = hit.At;
            _lastHitGeneration[target] = t.Generation;
            _hasLastHit[target] = true;
            if (!hit.Hurts || deathShown) return;
            if (_generations[target] == t.Generation && ServerClock.Diff(hit.At, _motions[target].Starts) < 0 && _motions[target].Kind != MotionKind.None) return;
            Start(model, target, MotionKind.Hurt, hit.At, hit.HurtMs, 0);
        }

        // D32: one total per unit on screen. A newer one (or, at the same moment, the larger running sum) replaces it in place;
        // an older one that comes late in the same Advance is dropped. False: drop the new one.
        bool ReplaceTotal(in PendingHit hit)
        {
            for (int i = 0; i < _numberCount; i++)
            {
                ref readonly var n = ref _numbers[i];
                if (n.Kind != NumberKind.Total || n.TargetId != hit.TargetId) continue;
                int diff = ServerClock.Diff(hit.At, n.Starts);
                if (diff < 0 || (diff == 0 && hit.Value < n.Value)) return false;
                _numbers[i] = _numbers[--_numberCount];
                return true;
            }
            return true;
        }

        void Start(WorldModel model, int slot, MotionKind kind, uint starts, int durationMs, byte attackNumber)
        {
            ref readonly var e = ref model[slot];
            _generations[slot] = e.Generation;
            _walkedAtStart[slot] = e.Walking;
            _motions[slot] = new UnitMotion { Kind = kind, Starts = starts, DurationMs = durationMs, Version = ++_version, AttackNumber = attackNumber };
        }

        void End(int slot)
        {
            if (_motions[slot].Kind == MotionKind.None) return;
            _motions[slot] = new UnitMotion { Kind = MotionKind.None, Starts = _motions[slot].Starts, Version = ++_version };
        }

        // What ends a motion (4.6): its time, a walk that starts (not under a hurt), sitting, death, another unit in the slot.
        void Keep(WorldModel model, int slot, uint now)
        {
            ref readonly var e = ref model[slot];
            if (e.State != EntityState.Dead) _deathShown[slot] = false;
            else if (!DeathShown(slot, e) && !HoldsDeath(slot, now))
            {
                _deathShown[slot] = true;
                _deathGeneration[slot] = e.Generation;
            }
            ref readonly var m = ref _motions[slot];
            if (m.Kind == MotionKind.None) return;
            if (e.Id == 0 || e.Generation != _generations[slot] || e.State == EntityState.Sitting ||
                (e.State == EntityState.Dead && !HoldsDeath(slot, now)))
            {
                End(slot);
                return;
            }
            if (m.Kind != MotionKind.Hurt && e.Walking && !_walkedAtStart[slot])
            {
                End(slot);
                return;
            }
            if (!e.Walking) _walkedAtStart[slot] = false;
            if (m.DurationMs <= 0 || ServerClock.Diff(now, m.Starts) < m.DurationMs) return;

            // D11: after an attack or a hurt a player stands by.
            uint ends = unchecked(m.Starts + (uint)m.DurationMs);
            if ((m.Kind == MotionKind.Attack || m.Kind == MotionKind.Hurt) && e.Kind == EntityKind.Player && _settings.StandbyMs != 0)
                Start(model, slot, MotionKind.Standby, ends, _settings.StandbyMs, 0);
            else End(slot);
        }

        void DropOldest<T>(T[] items, ref int count, string what) where T : struct
        {
            // Rare (the arrays are large): the first entry is among the oldest, as entries are appended in time order.
            items[0] = items[--count];
            if (_warnedFull) return;
            _warnedFull = true;
            NetLog.Warn("combat timeline full: the oldest " + what + " are dropped");
        }
    }
}
