using System;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Wire;
using Uninarok.Networking.Data;

namespace Uninarok.Networking.Packets.In.Map
{
    /// <summary>Map server to client, 0x0087 (ZC_NOTIFY_PLAYERMOVE), 12 bytes. The own character walks.</summary>
    [InPacket(PacketIds.PLAYER_MOVE, Size)]
    public readonly struct PLAYER_MOVE
    {
        public const int Size = 12;

        /// <summary>Server tick of the start of the walk.</summary>
        public readonly uint StartTick;
        public readonly UninarokMoveData Move;

        public PLAYER_MOVE(ReadOnlySpan<byte> payload)
        {
            var r = new PacketReader(payload);
            StartTick = r.ReadUInt32();
            Move = UninarokMoveData.Read(ref r);
            r.EnsureConsumed();
        }
    }
}
