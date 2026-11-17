using System;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Wire;

namespace Uninarok.Networking.Packets.In.Character
{
    /// <summary>
    /// Character server to client, 0x082D, variable (25 payload bytes). Character-slot counts.
    /// The trailing 20 bytes have no defined meaning and are skipped, not stored.
    /// </summary>
    [InPacket(PacketIds.CHARACTER_SLOTS_INFO, Size)]
    public readonly struct CHARACTER_SLOTS_INFO
    {
        public const int Size = -1;
        private const int UnusedWireBytes = 20;

        public readonly byte CharSlots;
        public readonly byte AvailableCharSlots;
        public readonly byte Unknown;
        public readonly byte CharSlots2;
        public readonly byte CharSlots3;

        public CHARACTER_SLOTS_INFO(ReadOnlySpan<byte> payload)
        {
            var r = new PacketReader(payload);
            CharSlots = r.ReadByte();
            AvailableCharSlots = r.ReadByte();
            Unknown = r.ReadByte();
            CharSlots2 = r.ReadByte();
            CharSlots3 = r.ReadByte();
            r.Skip(UnusedWireBytes);
            r.EnsureConsumed();
        }
    }
}
