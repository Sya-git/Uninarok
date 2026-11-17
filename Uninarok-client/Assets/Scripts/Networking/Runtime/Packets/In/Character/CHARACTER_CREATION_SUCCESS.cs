using System;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Wire;
using Uninarok.Networking.Data;

namespace Uninarok.Networking.Packets.In.Character
{
    /// <summary>Character server to client, 0x006D (HC_ACCEPT_MAKECHAR), 157 bytes. Returns the new character's record.</summary>
    [InPacket(PacketIds.CHARACTER_CREATION_SUCCESS, Size)]
    public readonly struct CHARACTER_CREATION_SUCCESS
    {
        public const int Size = 157;

        public readonly CharInfo Character;

        public CHARACTER_CREATION_SUCCESS(ReadOnlySpan<byte> payload)
        {
            var r = new PacketReader(payload);
            Character = CharInfo.Read(ref r);
            r.EnsureConsumed();
        }
    }
}
