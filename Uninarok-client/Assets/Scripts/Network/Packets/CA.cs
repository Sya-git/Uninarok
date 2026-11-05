using System;
using System.Buffers.Binary;
using Unity.Collections;

namespace Uninarok.Networking.Packets
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
            public void Serialize(Span<byte> buffer)
            {
                if (buffer.Length < Size)
                    throw new ArgumentException($"Buffer size is too small. Expected at least {Size} bytes.");
                
                BinaryPrimitives.WriteUInt16LittleEndian(buffer, PacketId);
                BinaryPrimitives.WriteUInt32LittleEndian(buffer[sizeof(uint)..], (uint)RagnarokPacketCodec.PacketVersion);
                var offset = sizeof(uint) + sizeof(ushort);

                // Serialize AccountName
                for (int i = 0; i < 24; i++)
                {
                    buffer[offset + i] = (i < AccountName.Length) ? (byte)AccountName[i] : (byte)'\0';
                }
                // Serialize AccountPassword
                for (int i = 0; i < 24; i++)
                {
                    buffer[offset + 24 + i] = (i < AccountPassword.Length) ? (byte)AccountPassword[i] : (byte)'\0';
                }
                buffer[offset + 48] = (byte)ClientType;
            }
        }
    }
}