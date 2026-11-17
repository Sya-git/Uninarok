using System;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Wire;
using Uninarok.Networking.Data;

namespace Uninarok.Networking.Packets.In.Map
{
    /// <summary>
    /// Map server to client, 0x02EB (ZC_ACCEPT_ENTER2), 13 bytes. Initial server tick, player position and facing, view size, font.
    /// Moves the flow from ConnectingToMap into LoadingMap.
    /// </summary>
    [InPacket(PacketIds.AUTHENTICATION_ACCEPTED, Size)]
    public readonly struct AUTHENTICATION_ACCEPTED
    {
        public const int Size = 13;

        public readonly uint StartTimestamp;
        public readonly UninarokPosition Position;
        public readonly byte XSize;
        public readonly byte YSize;
        public readonly short Font;

        public AUTHENTICATION_ACCEPTED(ReadOnlySpan<byte> payload)
        {
            var r = new PacketReader(payload);
            StartTimestamp = r.ReadUInt32();
            Position = UninarokPosition.Read(ref r);
            XSize = r.ReadByte();
            YSize = r.ReadByte();
            Font = r.ReadInt16();
            r.EnsureConsumed();
        }
    }
}
