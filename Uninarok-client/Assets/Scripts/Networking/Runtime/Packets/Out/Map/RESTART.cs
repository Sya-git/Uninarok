using System;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Transport;
using Uninarok.Networking.Core.Wire;

namespace Uninarok.Networking.Packets.Out.Map
{
    /// <summary>
    /// Client to map server, 0x00B2 (CZ_RESTART), 3 bytes. After death: back to the save point (0). Character select (1) is not
    /// sent by SPEC-0011 (FR-8).
    /// </summary>
    [OutPacket(PacketIds.RESTART, Size)]
    public readonly struct RESTART : IOutgoingPacket
    {
        public const int Size = 3;

        public const byte SavePoint = 0;

        public readonly byte Type;

        public RESTART(byte type) => Type = type;

        public int WireSize => Size;

        public void Serialize(Span<byte> destination)
        {
            var w = new PacketWriter(destination);
            w.WriteUInt16(PacketIds.RESTART);
            w.WriteByte(Type);
        }
    }
}
