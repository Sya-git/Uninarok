using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Wire;
using Uninarok.Networking.Data;
using Uninarok.Networking.Flow;

namespace Uninarok.Networking.Packets.In.Login
{
    /// <summary>
    /// Login server to client, 0xAC4 (AC_ACCEPT_LOGIN2), variable. Confirms the login and lists the character servers.
    /// Cold packet: it allocates the server array (and one host string per server) while decoding.
    /// </summary>
    [InPacket(PacketIds.ACCEPT_LOGIN, Size)]
    public readonly struct ACCEPT_LOGIN
    {
        public const int Size = -1;

        // After the 4-byte header: 60 bytes of fixed fields, then 160 bytes per server entry.
        private const int LastLoginTimeWireBytes = 26;

        public readonly int AuthCode;
        public readonly uint AccountId;
        public readonly uint UserLevel;
        public readonly uint LastLoginIp;
        public readonly AccountSex Sex;
        public readonly FixedBytes16 AuthToken;
        public readonly byte TwitterFlag;
        public readonly ServerEndpoint[] Servers;

        public ACCEPT_LOGIN(ReadOnlySpan<byte> payload)
        {
            var r = new PacketReader(payload);
            AuthCode = r.ReadInt32();
            AccountId = r.ReadUInt32();
            UserLevel = r.ReadUInt32();
            LastLoginIp = r.ReadUInt32();
            r.Skip(LastLoginTimeWireBytes);                  // unused by Hercules (zero-filled)
            Sex = (AccountSex)r.ReadByte();

            var token = new FixedBytes16();
            r.ReadBytes(MemoryMarshal.CreateSpan(ref token.byte0000, 16));   // FixedBytes16 is 16 contiguous bytes
            AuthToken = token;

            TwitterFlag = r.ReadByte();

            if (r.Remaining % ServerEndpoint.WireSize != 0)
                throw new PacketFormatException(
                    $"ACCEPT_LOGIN server list of {r.Remaining} byte(s) is not a multiple of {ServerEndpoint.WireSize}");

            int count = r.Remaining / ServerEndpoint.WireSize;
            if (count == 0)
            {
                Servers = Array.Empty<ServerEndpoint>();
            }
            else
            {
                var servers = new ServerEndpoint[count];
                for (int i = 0; i < count; i++) servers[i] = ServerEndpoint.Read(ref r);
                Servers = servers;
            }
            r.EnsureConsumed();
        }
    }
}
