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
        // enum status_point_types (PARAMETER_CHANGE.md, LONG_PARAMETER_CHANGE.md)
        internal const ushort VarBaseExp = 1, VarJobExp = 2, VarHp = 5, VarMaxHp = 6, VarSp = 7, VarMaxSp = 8, VarBaseLevel = 11,
                              VarNextBaseExp = 22, VarNextJobExp = 23, VarJobLevel = 55;

        public long Hp, MaxHp, Sp, MaxSp, BaseExp, JobExp, NextBaseExp, NextJobExp;
        /// <summary><see cref="AttackRange"/> is 1 until the server says.</summary>
        public int BaseLevel, JobLevel, AttackRange = 1;

        /// <summary>Changes when any value changes.</summary>
        public int Version { get; private set; }

        /// <summary>The selected character at the first entry; the next-level values and the range come from the server.</summary>
        internal void From(in CharInfo c)
        {
            Reset();
            Hp = c.Hp;
            MaxHp = c.MaxHp;
            Sp = c.Sp;
            MaxSp = c.MaxSp;
            BaseLevel = c.BaseLevel;
            JobLevel = c.JobLevel;
            BaseExp = unchecked((long)c.BaseExp);
            JobExp = unchecked((long)c.JobExp);
        }

        /// <summary>A <c>PARAMETER_CHANGE</c>; false when the variable is not one of the status or did not change.</summary>
        internal bool SetParameter(ushort varId, int value)
        {
            switch (varId)
            {
                case VarHp: return Set(ref Hp, value);
                case VarMaxHp: return Set(ref MaxHp, value);
                case VarSp: return Set(ref Sp, value);
                case VarMaxSp: return Set(ref MaxSp, value);
                case VarBaseLevel: return Set(ref BaseLevel, value);
                case VarJobLevel: return Set(ref JobLevel, value);
                default: return false;
            }
        }

        /// <summary>A <c>LONG_PARAMETER_CHANGE</c>; false when the variable is not one of the status or did not change.</summary>
        internal bool SetLongParameter(ushort varId, long value)
        {
            switch (varId)
            {
                case VarBaseExp: return Set(ref BaseExp, value);
                case VarJobExp: return Set(ref JobExp, value);
                case VarNextBaseExp: return Set(ref NextBaseExp, value);
                case VarNextJobExp: return Set(ref NextJobExp, value);
                default: return false; // zeny (20) and others
            }
        }

        internal void SetAttackRange(int range) => Set(ref AttackRange, Math.Max(0, range));

        /// <summary>Another character or login: nothing of the old one counts.</summary>
        internal void Reset()
        {
            Hp = MaxHp = Sp = MaxSp = BaseExp = JobExp = NextBaseExp = NextJobExp = 0;
            BaseLevel = JobLevel = 0;
            AttackRange = 1;
            Version++;
        }

        bool Set(ref long field, long value)
        {
            if (field == value) return false;
            field = value;
            Version++;
            return true;
        }

        bool Set(ref int field, int value)
        {
            if (field == value) return false;
            field = value;
            Version++;
            return true;
        }
    }
}
