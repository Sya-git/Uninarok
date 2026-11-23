using System;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Wire;

namespace Uninarok.Networking.Packets.In.Map
{
    /// <summary>Map server to client, 0x0080 (ZC_NOTIFY_VANISH), 7 bytes. A unit disappears.</summary>
    [InPacket(PacketIds.UNIT_VANISH, Size)]
    public readonly struct UNIT_VANISH
    {
        public const int Size = 7;

        public readonly uint UnitId;
        /// <summary>0 out of sight, 1 died, 2 logged out, 3 teleported, 4 feigned death.</summary>
        public readonly byte Type;

        public UNIT_VANISH(ReadOnlySpan<byte> payload)
        {
            var r = new PacketReader(payload);
            UnitId = r.ReadUInt32();
            Type = r.ReadByte();
            r.EnsureConsumed();
        }
    }
}
