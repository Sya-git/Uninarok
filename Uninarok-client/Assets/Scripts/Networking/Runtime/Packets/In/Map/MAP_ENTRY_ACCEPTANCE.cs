using System;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Wire;

namespace Uninarok.Networking.Packets.In.Map
{
    /// <summary>Map server to client, 0x0283 (ZC_NOTIFY_PLAYERID), 6 bytes. Confirms the connection and gives the character object ID.</summary>
    [InPacket(PacketIds.MAP_ENTRY_ACCEPTANCE, Size)]
    public readonly struct MAP_ENTRY_ACCEPTANCE
    {
        public const int Size = 6;

        public readonly uint CharacterObjectId;

        public MAP_ENTRY_ACCEPTANCE(ReadOnlySpan<byte> payload)
        {
            var r = new PacketReader(payload);
            CharacterObjectId = r.ReadUInt32();
            r.EnsureConsumed();
        }
    }
}
