using System;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Wire;

namespace Uninarok.Networking.Packets.In.Map
{
    /// <summary>Map server to client, 0x008A (ZC_NOTIFY_ACT), 29 bytes. A unit sat down (2) or stood up (3).</summary>
    [InPacket(PacketIds.UNIT_ACT, Size)]
    public readonly struct UNIT_ACT
    {
        public const int Size = 29;

        public readonly uint UnitId;
        /// <summary>2 sit, 3 stand; others belong to the combat spec.</summary>
        public readonly byte Action;

        public UNIT_ACT(ReadOnlySpan<byte> payload)
        {
            var r = new PacketReader(payload);
            UnitId = r.ReadUInt32();
            r.Skip(20); // uninitialised bytes in Hercules
            Action = r.ReadByte();
            r.Skip(2); // uninitialised bytes in Hercules
            r.EnsureConsumed();
        }
    }
}
