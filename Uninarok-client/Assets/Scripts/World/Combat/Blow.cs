using System;
using Uninarok.Networking.Client;
using Uninarok.Networking.Packets.In.Map;

namespace Uninarok.World
{
    /// <summary>What a blow shows (SPEC-0011 FR-5); a miss is a blow with no damage.</summary>
    public enum BlowKind : byte
    {
        Normal,
        Critical,
        LuckyDodge,
        MultiHit,
    }

    /// <summary>One blow as the server sent it (SPEC-0011 FR-1, FR-4, FR-5; PLAN-0011 4.2): <c>DAMAGE</c> in the client's words.</summary>
    public readonly struct Blow
    {
        public readonly uint AttackerId, TargetId;
        /// <summary>The server clock's time when the packet was handled: the blow starts then (no lag compensation).</summary>
        public readonly uint At;
        /// <summary>The attacker's motion time (<c>attackMT</c>): the hit lands this long after <see cref="At"/>.</summary>
        public readonly int AttackMs;
        /// <summary>The target's motion time (<c>attackedMT</c>).</summary>
        public readonly int HurtMs;
        public readonly int Damage, LeftDamage;
        /// <summary>At least 1.</summary>
        public readonly short Hits;
        public readonly BlowKind Kind;
        /// <summary>The target takes it without flinching (wire types 4 and 9).</summary>
        public readonly bool Endured;
        public readonly byte WireType;

        public Blow(uint attackerId, uint targetId, uint at, int attackMs, int hurtMs, int damage, int leftDamage, short hits,
                    BlowKind kind, bool endured, byte wireType)
        {
            AttackerId = attackerId;
            TargetId = targetId;
            At = at;
            AttackMs = attackMs;
            HurtMs = hurtMs;
            Damage = damage;
            LeftDamage = leftDamage;
            Hits = hits;
            Kind = kind;
            Endured = endured;
            WireType = wireType;
        }

        /// <summary>No damage on either hand, and not a lucky dodge.</summary>
        public bool IsMiss => Damage == 0 && LeftDamage == 0 && Kind != BlowKind.LuckyDodge;

        /// <summary>The packet's blow, starting <paramref name="now"/> (server clock). Unknown wire types are normal blows.</summary>
        public static Blow From(in DAMAGE p, uint now)
        {
            BlowKind kind;
            bool endured = false;
            switch (p.Type)
            {
                case 0: // normal
                case 5: // splash
                case 6: // skill
                    kind = BlowKind.Normal;
                    break;
                case 4:
                    kind = BlowKind.Normal;
                    endured = true;
                    break;
                case 8:
                case 13: // multi-hit critical: never sent for a basic attack by Hercules 2026.09 (D12)
                    kind = BlowKind.MultiHit;
                    break;
                case 9:
                    kind = BlowKind.MultiHit;
                    endured = true;
                    break;
                case 10:
                    kind = BlowKind.Critical;
                    break;
                case 11:
                    kind = BlowKind.LuckyDodge;
                    break;
                default:
                    NetLog.Trace("damage of unknown type " + p.Type);
                    kind = BlowKind.Normal;
                    break;
            }
            return new Blow(p.SourceId, p.TargetId, now, p.AttackMt, p.AttackedMt, p.Damage, p.LeftDamage, (short)Math.Max(1, (int)p.Count),
                            kind, endured, p.Type);
        }
    }
}
