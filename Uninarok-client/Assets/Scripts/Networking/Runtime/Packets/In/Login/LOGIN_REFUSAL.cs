using System;
using Unity.Collections;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Wire;
using Uninarok.Networking.Text;

namespace Uninarok.Networking.Packets.In.Login
{
    /// <summary>Login server to client, 0x006A (AC_REFUSE_LOGIN), 23 bytes. Refuses a login; the block date is set for bans.</summary>
    [InPacket(PacketIds.LOGIN_REFUSAL, Size)]
    public readonly struct LOGIN_REFUSAL
    {
        public const int Size = 23;
        private const int BlockDateWireBytes = 20;

        public readonly byte ErrorCode;

        /// <summary>Ban expiration date as text; empty when the refusal is not a ban. Fits the worst case of 19 characters.</summary>
        public readonly FixedString64Bytes BlockDate;

        public LOGIN_REFUSAL(ReadOnlySpan<byte> payload)
        {
            var r = new PacketReader(payload);
            ErrorCode = r.ReadByte();
            var date = new FixedString64Bytes();
            WireText.Read(ref r, BlockDateWireBytes, ref date);
            BlockDate = date;
            r.EnsureConsumed();
        }
    }
}
