using System;
using Uninarok.Login;
using Uninarok.Networking.Packets.Attributes;
using Unity.Collections;

namespace Uninarok.Networking.Packets.Out
{
    public static partial class LoginServer
    {
        [OutPacket(PacketIds.LOGIN, REQUEST_LOGIN.Size)]
        public struct REQUEST_LOGIN
        {
            private const byte AccountNameSize = 24;
            private const byte AccountNamePasswordSize = 24;
            public const short Size = 55;

            public FixedString32Bytes AccountName; // 24 bytes
            public FixedString32Bytes AccountPassword; // 24 bytes
            //TODO: Procurar o que client type faz
            public char ClientType; // 1 bytes

            public REQUEST_LOGIN(string accountName, string accountPassword, char clientType)
            {
                AccountName = new FixedString32Bytes(accountName);
                AccountPassword = new FixedString32Bytes(accountPassword);
                ClientType = clientType;

                if (AccountName.Length < AccountNameSize)
                {
                    AccountName.Append('\0');
                }
                if (AccountPassword.Length < AccountNamePasswordSize)
                {
                    AccountPassword.Append('\0');
                }
            }
            public Memory<byte> Serialize()
            {
                var buffer = new Memory<byte>(new byte[Size]);
                var writer = new PacketWriter(buffer.Span);

                writer.Write((ushort)PacketIds.LOGIN);
                writer.Write((uint)LoginManager.PacketVersion);

                // Serialize AccountName
                for (int i = 0; i < AccountNameSize; i++)
                {
                    writer.Write((i < AccountName.Length) ? (byte)AccountName[i] : (byte)'\0');
                }
                // Serialize AccountPassword
                for (int i = 0; i < AccountNameSize; i++)
                {
                    writer.Write((i < AccountPassword.Length) ? (byte)AccountPassword[i] : (byte)'\0');
                }
                writer.Write((byte)ClientType);
                
                return buffer;
            }
        }
    }
}