using System;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Transport;
using Uninarok.Networking.Core.Wire;
using Uninarok.Networking.Data;

namespace Uninarok.Networking.Packets.Out.Map
{
    /// <summary>
    /// Client to map server, 0x0436 (CZ_ENTER2), 23 bytes. Requests entry with the account, character and authentication values.
    /// The 4 reserved bytes at offsets 14 to 17 are required: omitting them desynchronizes the server.
    /// </summary>
    [OutPacket(PacketIds.REQUEST_ENTER_MAP, Size)]
    public readonly struct REQUEST_ENTER_MAP : IOutgoingPacket
    {
        public const int Size = 23;
        private const int ReservedWireBytes = 4;

        public readonly uint AccountId;
        public readonly int CharId;
        public readonly int AuthCode;

        /// <summary>Client tick in milliseconds.</summary>
        public readonly uint Timestamp;
        public readonly AccountSex Sex;

        public REQUEST_ENTER_MAP(uint accountId, int charId, int authCode, uint timestamp, AccountSex sex)
        {
            AccountId = accountId;
            CharId = charId;
            AuthCode = authCode;
            Timestamp = timestamp;
            Sex = sex;
        }

        public int WireSize => Size;

        public void Serialize(Span<byte> destination)
        {
            var w = new PacketWriter(destination);
            w.WriteUInt16(PacketIds.REQUEST_ENTER_MAP);
            w.WriteUInt32(AccountId);
            w.WriteInt32(CharId);
            w.WriteInt32(AuthCode);
            w.Skip(ReservedWireBytes);                       // zeros, offsets 14 to 17
            w.WriteUInt32(Timestamp);
            w.WriteByte((byte)Sex);
        }
    }
}
