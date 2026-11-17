using System;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Wire;

namespace Uninarok.Networking.Packets.In.Character
{
    /// <summary>Character server to client, 0x006E, 3 bytes. Character creation failed; <see cref="ErrorCode"/> says why.</summary>
    [InPacket(PacketIds.CHARACTER_CREATION_FAILED, Size)]
    public readonly struct CHARACTER_CREATION_FAILED
    {
        public const int Size = 3;

        // Codes Hercules sends (char_creation_failed).
        public const byte NameAlreadyExists = 0x00;
        public const byte Underaged = 0x01;
        public const byte SymbolsInNameForbidden = 0x02;
        public const byte NotEligibleForSlot = 0x03;
        public const byte Denied = 0xFF;

        public readonly byte ErrorCode;

        public CHARACTER_CREATION_FAILED(ReadOnlySpan<byte> payload)
        {
            var r = new PacketReader(payload);
            ErrorCode = r.ReadByte();
            r.EnsureConsumed();
        }
    }
}
