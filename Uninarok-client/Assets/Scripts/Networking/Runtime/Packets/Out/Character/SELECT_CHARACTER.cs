using System;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Transport;
using Uninarok.Networking.Core.Wire;

namespace Uninarok.Networking.Packets.Out.Character
{
    /// <summary>Client to character server, 0x0066, 3 bytes. Selects a character slot.</summary>
    [OutPacket(PacketIds.SELECT_CHARACTER, Size)]
    public readonly struct SELECT_CHARACTER : IOutgoingPacket
    {
        public const int Size = 3;

        public readonly byte Slot;

        public SELECT_CHARACTER(byte slot)
        {
            Slot = slot;
        }

        public int WireSize => Size;

        public void Serialize(Span<byte> destination)
        {
            var w = new PacketWriter(destination);
            w.WriteUInt16(PacketIds.SELECT_CHARACTER);
            w.WriteByte(Slot);
        }
    }
}
