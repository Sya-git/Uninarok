using System;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Transport;
using Uninarok.Networking.Core.Wire;
using Uninarok.Networking.Data;

namespace Uninarok.Networking.Packets.Out.Character
{
    /// <summary>Client to character server, 0x0065, 17 bytes. Requests entry with the values the login server returned.</summary>
    [OutPacket(PacketIds.CHARACTER_SERVER_REQUEST_ENTRY, Size)]
    public readonly struct CHARACTER_SERVER_REQUEST_ENTRY : IOutgoingPacket
    {
        public const int Size = 17;
        private const int ReservedWireBytes = 2;

        public readonly uint AccountId;
        public readonly int AuthCode;
        public readonly uint UserLevel;
        public readonly AccountSex Sex;

        public CHARACTER_SERVER_REQUEST_ENTRY(uint accountId, int authCode, uint userLevel, AccountSex sex)
        {
            AccountId = accountId;
            AuthCode = authCode;
            UserLevel = userLevel;
            Sex = sex;
        }

        public int WireSize => Size;

        public void Serialize(Span<byte> destination)
        {
            var w = new PacketWriter(destination);
            w.WriteUInt16(PacketIds.CHARACTER_SERVER_REQUEST_ENTRY);
            w.WriteUInt32(AccountId);
            w.WriteInt32(AuthCode);
            w.WriteUInt32(UserLevel);
            w.Skip(ReservedWireBytes);                        // zeros
            w.WriteByte((byte)Sex);
        }
    }
}
