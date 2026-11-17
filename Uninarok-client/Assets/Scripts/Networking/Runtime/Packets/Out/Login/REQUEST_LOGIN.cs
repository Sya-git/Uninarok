using System;
using Unity.Collections;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Transport;
using Uninarok.Networking.Core.Wire;
using Uninarok.Networking.Text;

namespace Uninarok.Networking.Packets.Out.Login
{
    /// <summary>Client to login server, 0x0064 (CA_LOGIN), 55 bytes. Starts a login with account credentials.</summary>
    [OutPacket(PacketIds.REQUEST_LOGIN, Size)]
    public readonly struct REQUEST_LOGIN : IOutgoingPacket
    {
        public const int Size = 55;
        private const int AccountNameWireBytes = 24;
        private const int PasswordWireBytes = 24;

        public readonly uint PacketVersion;
        public readonly FixedString32Bytes AccountName;
        public readonly FixedString32Bytes AccountPassword;
        public readonly byte ClientType;

        public REQUEST_LOGIN(uint packetVersion, in FixedString32Bytes accountName, in FixedString32Bytes accountPassword,
                             byte clientType = ProtocolInfo.DefaultClientType)
        {
            PacketVersion = packetVersion;
            AccountName = accountName;
            AccountPassword = accountPassword;
            ClientType = clientType;
        }

        public int WireSize => Size;

        public void Serialize(Span<byte> destination)
        {
            var w = new PacketWriter(destination);
            w.WriteUInt16(PacketIds.REQUEST_LOGIN);
            w.WriteUInt32(PacketVersion);
            WireText.Write(ref w, AccountNameWireBytes, in AccountName);
            WireText.Write(ref w, PasswordWireBytes, in AccountPassword);
            w.WriteByte(ClientType);
        }
    }
}
