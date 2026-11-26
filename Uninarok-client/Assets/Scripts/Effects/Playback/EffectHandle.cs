using System;

namespace Uninarok.Effects
{
    /// <summary>
    /// A playing effect instance: a pooled slot and the generation of that slot's current play (SPEC-0012 FR-6; plan 4.5,
    /// D29). Generations start at 1 and step on every release, so <c>default</c> is never active and a stale handle never
    /// reaches the play that reused its slot.
    /// </summary>
    public readonly struct EffectHandle : IEquatable<EffectHandle>
    {
        public readonly int Slot, Generation;

        internal EffectHandle(int slot, int generation) { Slot = slot; Generation = generation; }

        /// <summary>True for a handle a play returned (not <c>default</c>); it may still have ended: see <see cref="EffectRuntime.IsActive"/>.</summary>
        public bool IsValid => Generation != 0;

        public bool Equals(EffectHandle other) => Slot == other.Slot && Generation == other.Generation;
        public override bool Equals(object obj) => obj is EffectHandle h && Equals(h);
        public override int GetHashCode() => HashCode.Combine(Slot, Generation);
        public static bool operator ==(EffectHandle a, EffectHandle b) => a.Equals(b);
        public static bool operator !=(EffectHandle a, EffectHandle b) => !a.Equals(b);
        public override string ToString() => $"Effect({Slot}:{Generation})";
    }

    /// <summary>Where an instance is placed (plan 4.5).</summary>
    public enum EffectPlace : byte { Point, Cell, Unit }
}
