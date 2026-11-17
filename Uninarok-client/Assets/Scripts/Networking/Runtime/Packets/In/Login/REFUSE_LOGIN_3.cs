using System;
using Unity.Collections;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Wire;
using Uninarok.Networking.Text;

namespace Uninarok.Networking.Packets.In.Login
{
    /// <summary>Login server to client, 0x0B02 (AC_REFUSE_LOGIN_R3), 26 bytes. Refuses a login with a 4-byte error code.</summary>
    [InPacket(PacketIds.REFUSE_LOGIN_3, Size)]
    public readonly struct REFUSE_LOGIN_3
    {
        public const int Size = 26;
        private const int BlockDateWireBytes = 20;

        public readonly int ErrorCode;

        /// <summary>Block date or related message as text; may be empty.</summary>
        public readonly FixedString64Bytes BlockDate;

        public REFUSE_LOGIN_3(ReadOnlySpan<byte> payload)
        {
            var r = new PacketReader(payload);
            ErrorCode = r.ReadInt32();
            var date = new FixedString64Bytes();
            WireText.Read(ref r, BlockDateWireBytes, ref date);
            BlockDate = date;
            r.EnsureConsumed();
        }
    }
}
