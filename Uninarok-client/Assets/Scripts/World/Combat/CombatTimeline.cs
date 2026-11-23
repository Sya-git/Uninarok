using System;

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
        /// <summary>The sum of a multi-hit, with its last part.</summary>
        Total,
    }

    /// <summary>A number or a message shown over a unit (FR-5, FR-6): from when, over whom, where the unit was shown then.</summary>
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

    /// <summary>The server's weapon value of a look to its base weapon type, 0 (bare hand) to 30 (PLAN-0011 4.7).</summary>
    public interface IWeaponTypes
    {
        int TypeOf(int weapon);
    }

    /// <summary>
    /// What is shown of each blow, and when (SPEC-0011 FR-4 to FR-6, FR-9, FR-10, FR-14; PLAN-0011 4.6): motions, numbers and
    /// projectiles from the blows of the model, later than the packets (D2). It reads the model and never changes it. Arrays are
    /// allocated once; nothing allocates per blow or per frame (FR-12). Main thread.
    /// </summary>
    public sealed class CombatTimeline
    {
        readonly CombatSettings _settings;

        public CombatTimeline(CombatSettings settings, int slots, int hits = 1024, int numbers = 512, int projectiles = 128)
        {
            _settings = settings != null ? settings : throw new ArgumentNullException(nameof(settings));
            if (slots <= 0) throw new ArgumentOutOfRangeException(nameof(slots));
        }

        /// <summary>Until the catalog is loaded every weapon type is 0.</summary>
        public IWeaponTypes Weapons { get; set; }

        /// <summary>The world whose units the blows are of; a new map's model forgets everything.</summary>
        public void Use(WorldModel model) => throw new NotImplementedException("T4.3");

        public void Add(in Blow blow) => throw new NotImplementedException("T4.3");

        /// <summary>A unit picked up an item (UNIT_ACT 1): a player bends down once (FR-10).</summary>
        public void AddPickUp(uint id, uint itemId, uint now) => throw new NotImplementedException("T4.5");

        public void Advance(uint now) => throw new NotImplementedException("T4.3");

        public ref readonly UnitMotion MotionOf(int slot) => throw new NotImplementedException("T4.3");

        /// <summary>FR-9: the unit is dead in the model but a hit that came before its death notice is not shown yet.</summary>
        public bool HoldsDeath(int slot, uint now) => throw new NotImplementedException("T4.5");

        /// <summary>The numbers shown now.</summary>
        public ReadOnlySpan<DamageNumber> Numbers => throw new NotImplementedException("T4.3");

        /// <summary>The projectiles in flight now.</summary>
        public ReadOnlySpan<Projectile> Projectiles => throw new NotImplementedException("T4.8");

        public void Clear() => throw new NotImplementedException("T4.3");
    }
}
