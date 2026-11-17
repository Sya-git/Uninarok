using System;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Wire;

namespace Uninarok.Networking.Packets.In.Login
{
    /// <summary>Login server to client, 0x0081 (SC_NOTIFY_BAN), 3 bytes. A connection error or status; handled as a login refusal.</summary>
    [InPacket(PacketIds.LOGIN_SERVER_NOTICE, Size)]
    public readonly struct LOGIN_SERVER_NOTICE
    {
        public const int Size = 3;

        /// <summary>Hercules uses 1 for "server closed" and 8 for "the server still recognizes the previous login".</summary>
        public readonly byte ErrorCode;

        public LOGIN_SERVER_NOTICE(ReadOnlySpan<byte> payload)
        {
            var r = new PacketReader(payload);
            ErrorCode = r.ReadByte();
            r.EnsureConsumed();
        }
    }
}
