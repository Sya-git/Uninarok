using System;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Wire;

namespace Uninarok.Networking.Packets.In.Character
{
    /// <summary>Character server to client, 0x08B9, 12 bytes. PIN-code login state with its seed and account ID.</summary>
    [InPacket(PacketIds.PINCODE_LOGINSTATE, Size)]
    public readonly struct PINCODE_LOGINSTATE
    {
        public const int Size = 12;

        public readonly int PincodeSeed;
        public readonly int AccountId;

        /// <summary>PIN-code state; the meanings are not documented yet. Unknown until this packet arrives (FR-13).</summary>
        public readonly short State;

        public PINCODE_LOGINSTATE(ReadOnlySpan<byte> payload)
        {
            var r = new PacketReader(payload);
            PincodeSeed = r.ReadInt32();
            AccountId = r.ReadInt32();
            State = r.ReadInt16();
            r.EnsureConsumed();
        }
    }
}
