using System;
using Unity.Collections;

namespace Uninarok.Networking.Packets.Out
{
    public static class CA
    {
        // 0x0064
        public struct LOGIN
        {
            public const ushort PacketId = 0x0064;
            public const short Size = 55;

            public FixedString32Bytes AccountName; // 24 bytes
            public FixedString32Bytes AccountPassword; // 24 bytes
            //TODO: O que client type faz
            public char ClientType; // 1 bytes

            public LOGIN(string accountName, string accountPassword, char clientType)
            {
                AccountName = new FixedString32Bytes(accountName);
                AccountPassword = new FixedString32Bytes(accountPassword);
                ClientType = clientType;
            }
            public Memory<byte> Serialize()
            {
                var buffer = new Memory<byte>(new byte[Size]);
                var writer = new PacketWriter(buffer.Span);

                writer.WriteUInt16(PacketId);
                writer.WriteUInt32((uint)RagnarokPacketCodec.PacketVersion);

                // Serialize AccountName
                for (int i = 0; i < 24; i++)
                {
                    writer.WriteByte((i < AccountName.Length) ? (byte)AccountName[i] : (byte)'\0');
                }
                // Serialize AccountPassword
                for (int i = 0; i < 24; i++)
                {
                    writer.WriteByte((i < AccountPassword.Length) ? (byte)AccountPassword[i] : (byte)'\0');
                }
                writer.WriteByte((byte)ClientType);
                
                return buffer;
            }
        }
    }
}