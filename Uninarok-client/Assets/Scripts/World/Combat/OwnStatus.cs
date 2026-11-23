using System;
using Uninarok.Networking.Data;

namespace Uninarok.World
{
    /// <summary>
    /// The own character's status as the server last said it (SPEC-0011 FR-1; PLAN-0011 4.2): from the selected character at
    /// entry, then from every status packet, also before the map is loaded. It lives in <c>WorldSession</c> and is kept across
    /// maps. Only the session writes it; views read it by <see cref="Version"/>. Death is never taken from it.
    /// </summary>
    public sealed class OwnStatus
    {
        public long Hp, MaxHp, Sp, MaxSp, BaseExp, JobExp, NextBaseExp, NextJobExp;
        /// <summary><see cref="AttackRange"/> is 1 until the server says.</summary>
        public int BaseLevel, JobLevel, AttackRange = 1;

        /// <summary>Changes when any value changes.</summary>
        public int Version { get; private set; }

        /// <summary>The selected character at the first entry; the next-level values and the range come from the server.</summary>
        internal void From(in CharInfo c) => throw new NotImplementedException();

        /// <summary>A <c>PARAMETER_CHANGE</c>; false when the variable is not one of the status or did not change.</summary>
        internal bool SetParameter(ushort varId, int value) => throw new NotImplementedException();

        /// <summary>A <c>LONG_PARAMETER_CHANGE</c>; false when the variable is not one of the status or did not change.</summary>
        internal bool SetLongParameter(ushort varId, long value) => throw new NotImplementedException();

        internal void SetAttackRange(int range) => throw new NotImplementedException();

        /// <summary>Another character or login: nothing of the old one counts.</summary>
        internal void Reset() => throw new NotImplementedException();
    }
}
