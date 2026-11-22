using System;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Wire;

namespace Uninarok.Networking.Packets.In.Map
{
    /// <summary>Map server to client, 0x01D7 (ZC_SPRITE_CHANGE2), 15 bytes. One part of a unit's look changed.</summary>
    [InPacket(PacketIds.UNIT_LOOK_CHANGE, Size)]
    public readonly struct UNIT_LOOK_CHANGE
    {
        public const int Size = 15;

        public readonly uint UnitId;
        /// <summary><c>enum look</c>, see UNIT_LOOK_CHANGE.md.</summary>
        public readonly byte Type;
        public readonly uint Value;
        /// <summary>The shield when the type is weapon.</summary>
        public readonly uint Value2;

        public UNIT_LOOK_CHANGE(ReadOnlySpan<byte> payload)
        {
            var r = new PacketReader(payload);
            UnitId = r.ReadUInt32();
            Type = r.ReadByte();
            Value = r.ReadUInt32();
            Value2 = r.ReadUInt32();
            r.EnsureConsumed();
        }
    }
}
