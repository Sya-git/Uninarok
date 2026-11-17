using System;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Wire;

namespace Uninarok.Networking.Packets.In.Map
{
    /// <summary>Map server to client, 0x0074 (ZC_REFUSE_ENTER), 3 bytes. The map server refused the connection.</summary>
    [InPacket(PacketIds.MAP_ENTRY_REFUSAL, Size)]
    public readonly struct MAP_ENTRY_REFUSAL
    {
        public const int Size = 3;

        /// <summary>Hercules documents 0 to 4 (client mismatch, ID mismatch, mobile-client states).</summary>
        public readonly byte ErrorCode;

        public MAP_ENTRY_REFUSAL(ReadOnlySpan<byte> payload)
        {
            var r = new PacketReader(payload);
            ErrorCode = r.ReadByte();
            r.EnsureConsumed();
        }
    }
}
