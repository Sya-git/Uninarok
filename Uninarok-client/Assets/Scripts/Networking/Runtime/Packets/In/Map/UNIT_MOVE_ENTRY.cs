using System;
using Unity.Collections;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Wire;
using Uninarok.Networking.Data;
using Uninarok.Networking.Text;

namespace Uninarok.Networking.Packets.In.Map
{
    /// <summary>Map server to client, 0x09FD (<c>packet_unit_walking</c>), variable, 114 bytes sent. A walking unit enters view, or a known one changed its walk speed.</summary>
    [InPacket(PacketIds.UNIT_MOVE_ENTRY, Size)]
    public readonly struct UNIT_MOVE_ENTRY
    {
        public const int Size = -1;

        /// <summary>Unit kind (<c>enum clif_unittype</c>): 0 player, 5 monster, 6 NPC, see UNIT_MOVE_ENTRY.md.</summary>
        public readonly byte ObjectType;
        /// <summary>Object id; the account id of players.</summary>
        public readonly uint UnitId;
        /// <summary>Character id of a player, 0 for every other unit.</summary>
        public readonly uint CharacterId;
        /// <summary>Walk speed, ms per straight cell.</summary>
        public readonly short Speed;
        public readonly short BodyState;
        public readonly short HealthState;
        public readonly int EffectState;
        /// <summary>Job id or sprite class.</summary>
        public readonly short Job;
        public readonly ushort HairStyle;
        /// <summary>View id, or item id for items without one.</summary>
        public readonly uint Weapon;
        /// <summary>View id, or item id for items without one.</summary>
        public readonly uint Shield;
        public readonly ushort HeadBottom;
        /// <summary>Server tick when the packet was sent; the walk goes on from the source cell at this time.</summary>
        public readonly uint MoveStartTime;
        public readonly ushort HeadTop;
        public readonly ushort HeadMid;
        public readonly short HairColour;
        public readonly short ClothColour;
        /// <summary>0 straight, 1 right, 2 left.</summary>
        public readonly short HeadDirection;
        public readonly ushort Robe;
        public readonly uint GuildId;
        public readonly short EmblemVersion;
        public readonly short Honor;
        public readonly int Virtue;
        public readonly byte IsPkModeOn;
        public readonly byte Sex;
        public readonly UninarokMoveData Move;
        public readonly byte XSize;
        public readonly byte YSize;
        public readonly short Level;
        public readonly short Font;
        public readonly int MaxHp;
        public readonly int Hp;
        public readonly byte IsBoss;
        public readonly ushort BodyStyle;
        public readonly FixedString32Bytes Name;

        public UNIT_MOVE_ENTRY(ReadOnlySpan<byte> payload)
        {
            var r = new PacketReader(payload);
            ObjectType = r.ReadByte();
            UnitId = r.ReadUInt32();
            CharacterId = r.ReadUInt32();
            Speed = r.ReadInt16();
            BodyState = r.ReadInt16();
            HealthState = r.ReadInt16();
            EffectState = r.ReadInt32();
            Job = r.ReadInt16();
            HairStyle = r.ReadUInt16();
            Weapon = r.ReadUInt32();
            Shield = r.ReadUInt32();
            HeadBottom = r.ReadUInt16();
            MoveStartTime = r.ReadUInt32();
            HeadTop = r.ReadUInt16();
            HeadMid = r.ReadUInt16();
            HairColour = r.ReadInt16();
            ClothColour = r.ReadInt16();
            HeadDirection = r.ReadInt16();
            Robe = r.ReadUInt16();
            GuildId = r.ReadUInt32();
            EmblemVersion = r.ReadInt16();
            Honor = r.ReadInt16();
            Virtue = r.ReadInt32();
            IsPkModeOn = r.ReadByte();
            Sex = r.ReadByte();
            Move = UninarokMoveData.Read(ref r);
            XSize = r.ReadByte();
            YSize = r.ReadByte();
            Level = r.ReadInt16();
            Font = r.ReadInt16();
            MaxHp = r.ReadInt32();
            Hp = r.ReadInt32();
            IsBoss = r.ReadByte();
            BodyStyle = r.ReadUInt16();
            Name = default;
            WireText.Read(ref r, 24, ref Name);
            r.EnsureConsumed();
        }
    }
}
