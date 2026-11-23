using System;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Wire;

namespace Uninarok.Networking.Packets.In.Map
{
    /// <summary>
    /// Map server to client, 0x08C8 (ZC_NOTIFY_ACT3, struct packet_damage), 34 bytes. One blow, sent when it starts: the
    /// attacker's and the target's motion times, the damage and its type (SPEC-0011 FR-4, FR-5).
    /// </summary>
    [InPacket(PacketIds.DAMAGE, Size)]
    public readonly struct DAMAGE
    {
        public const int Size = 34;

        public readonly uint SourceId;
        public readonly uint TargetId;
        /// <summary>Server tick when the blow started.</summary>
        public readonly uint Tick;
        /// <summary>The attacker's motion time in ms (amotion, uncapped); the hit lands this long after the start.</summary>
        public readonly int AttackMt;
        /// <summary>The target's motion time in ms (dmotion).</summary>
        public readonly int AttackedMt;
        /// <summary>Total damage; 0 a miss, negative a heal or an absorbed blow.</summary>
        public readonly int Damage;
        /// <summary>Always 0 from Hercules.</summary>
        public readonly byte IsSpDamage;
        /// <summary>Number of hits; 0 for some self damage.</summary>
        public readonly short Count;
        /// <summary>enum battle_dmg_type: 0 normal, 4 endure, 8 multi-hit, 9 endured multi-hit, 10 critical, 11 lucky dodge.</summary>
        public readonly byte Type;
        /// <summary>Second-hand damage of dual wielding.</summary>
        public readonly int LeftDamage;

        public DAMAGE(ReadOnlySpan<byte> payload)
        {
            var r = new PacketReader(payload);
            SourceId = r.ReadUInt32();
            TargetId = r.ReadUInt32();
            Tick = r.ReadUInt32();
            AttackMt = r.ReadInt32();
            AttackedMt = r.ReadInt32();
            Damage = r.ReadInt32();
            IsSpDamage = r.ReadByte();
            Count = r.ReadInt16();
            Type = r.ReadByte();
            LeftDamage = r.ReadInt32();
            r.EnsureConsumed();
        }
    }
}
