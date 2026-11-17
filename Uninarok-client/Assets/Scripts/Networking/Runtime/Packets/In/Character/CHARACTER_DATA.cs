using System;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Wire;
using Uninarok.Networking.Data;

namespace Uninarok.Networking.Packets.In.Character
{
    /// <summary>
    /// Character server to client, 0x006B, variable. Character-list metadata followed by zero or more 155-byte
    /// <see cref="CharInfo"/> records. Cold packet: it allocates the record array while decoding.
    /// </summary>
    [InPacket(PacketIds.CHARACTER_DATA, Size)]
    public readonly struct CHARACTER_DATA
    {
        public const int Size = -1;
        private const int UnknownWireBytes = 20;

        public readonly byte MaxChars;
        public readonly byte AvailableSlots;
        public readonly byte PremiumSlots;
        public readonly CharInfo[] Characters;

        public CHARACTER_DATA(ReadOnlySpan<byte> payload)
        {
            var r = new PacketReader(payload);
            MaxChars = r.ReadByte();
            AvailableSlots = r.ReadByte();
            PremiumSlots = r.ReadByte();
            r.Skip(UnknownWireBytes);

            if (r.Remaining % CharInfo.WireSize != 0)
                throw new PacketFormatException(
                    $"CHARACTER_DATA record list of {r.Remaining} byte(s) is not a multiple of {CharInfo.WireSize}");

            int count = r.Remaining / CharInfo.WireSize;
            if (count == 0)
            {
                Characters = Array.Empty<CharInfo>();
            }
            else
            {
                var list = new CharInfo[count];
                for (int i = 0; i < count; i++) list[i] = CharInfo.Read(ref r);
                Characters = list;
            }
            r.EnsureConsumed();
        }
    }
}
