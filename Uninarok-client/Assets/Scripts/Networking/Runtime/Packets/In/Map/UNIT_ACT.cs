using System;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Wire;

namespace Uninarok.Networking.Packets.In.Map
{
    /// <summary>Map server to client, 0x008A (ZC_NOTIFY_ACT), 29 bytes. A unit picked up an item (1), sat down (2) or stood up (3).</summary>
    [InPacket(PacketIds.UNIT_ACT, Size)]
    public readonly struct UNIT_ACT
    {
        public const int Size = 29;

        public const byte PickUp = 1;
        public const byte Sit = 2;
        public const byte Stand = 3;

        public readonly uint UnitId;
        /// <summary>The picked-up item's object ID; valid for <see cref="PickUp"/> only (uninitialised bytes for sit and stand).</summary>
        public readonly uint TargetId;
        public readonly byte Action;

        public UNIT_ACT(ReadOnlySpan<byte> payload)
        {
            var r = new PacketReader(payload);
            UnitId = r.ReadUInt32();
            TargetId = r.ReadUInt32();
            r.Skip(16); // uninitialised bytes in Hercules
            Action = r.ReadByte();
            r.Skip(2); // uninitialised bytes in Hercules
            r.EnsureConsumed();
        }
    }
}
