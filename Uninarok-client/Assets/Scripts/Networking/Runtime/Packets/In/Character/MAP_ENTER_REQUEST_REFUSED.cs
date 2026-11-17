using System;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Wire;

namespace Uninarok.Networking.Packets.In.Character
{
    /// <summary>Character server to client, 0x006C, 3 bytes. The request to enter the map was refused (the flow returns to character selection).</summary>
    [InPacket(PacketIds.MAP_ENTER_REQUEST_REFUSED, Size)]
    public readonly struct MAP_ENTER_REQUEST_REFUSED
    {
        public const int Size = 3;

        /// <summary>Hercules always sends 0 ("rejected from server").</summary>
        public readonly byte ErrorCode;

        public MAP_ENTER_REQUEST_REFUSED(ReadOnlySpan<byte> payload)
        {
            var r = new PacketReader(payload);
            ErrorCode = r.ReadByte();
            r.EnsureConsumed();
        }
    }
}
