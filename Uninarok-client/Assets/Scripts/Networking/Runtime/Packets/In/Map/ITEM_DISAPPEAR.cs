using System;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Wire;

namespace Uninarok.Networking.Packets.In.Map
{
    /// <summary>Map server to client, 0x00A1 (ZC_ITEM_DISAPPEAR), 6 bytes. An item leaves the ground.</summary>
    [InPacket(PacketIds.ITEM_DISAPPEAR, Size)]
    public readonly struct ITEM_DISAPPEAR
    {
        public const int Size = 6;

        public readonly uint ItemObjectId;

        public ITEM_DISAPPEAR(ReadOnlySpan<byte> payload)
        {
            var r = new PacketReader(payload);
            ItemObjectId = r.ReadUInt32();
            r.EnsureConsumed();
        }
    }
}
