using System;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Transport;
using Uninarok.Networking.Core.Wire;

namespace Uninarok.Networking.Packets.Out.Map
{
    /// <summary>Client to map server, 0x0437 (CZ_REQUEST_ACT2), 7 bytes. Sits (2) or stands (3), or attacks a unit (combat spec).</summary>
    [OutPacket(PacketIds.REQUEST_ACT, Size)]
    public readonly struct REQUEST_ACT : IOutgoingPacket
    {
        public const int Size = 7;

        public const byte Sit = 2;
        public const byte Stand = 3;

        /// <summary>0 for sitting and standing.</summary>
        public readonly uint TargetId;
        public readonly byte Action;

        public REQUEST_ACT(uint targetId, byte action)
        {
            TargetId = targetId;
            Action = action;
        }

        public int WireSize => Size;

        public void Serialize(Span<byte> destination)
        {
            var w = new PacketWriter(destination);
            w.WriteUInt16(PacketIds.REQUEST_ACT);
            w.WriteUInt32(TargetId);
            w.WriteByte(Action);
        }
    }
}
