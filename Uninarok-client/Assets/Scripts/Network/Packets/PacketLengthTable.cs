using System;

namespace Uninarok.Networking.Packets
{
    public static class PacketLengthTable
    {
        private static readonly short[] Lengths = Create();

        private static short[] Create()
        {
            var lengths = new short[ushort.MaxValue + 1];
            Register(ref lengths, CA.LOGIN.PacketId, CA.LOGIN.Size);
            Register(ref lengths, AC.ACCEPT_LOGIN.PacketId, AC.ACCEPT_LOGIN.Size);
            Register(ref lengths, AC.REFUSE_LOGIN.PacketId, AC.REFUSE_LOGIN.Size);
            Register(ref lengths, SC.NOTIFY_BAN.PacketId, SC.NOTIFY_BAN.Size);
            Register(ref lengths, SC.REFUSE_LOGIN_R2.PacketId, SC.REFUSE_LOGIN_R2.Size);
            Register(ref lengths, SC.REFUSE_LOGIN_R3.PacketId, SC.REFUSE_LOGIN_R3.Size);
            Register(ref lengths, SC.ACK_HASH.PacketId, SC.ACK_HASH.Size);
            Register(ref lengths, CH.ENTER.PacketId, CH.ENTER.Size);
            Register(ref lengths, CH.SELECT_CHAR.PacketId, CH.SELECT_CHAR.Size);
            Register(ref lengths, CZ.ENTER.PacketId, CZ.ENTER.Size);
            Register(ref lengths, CZ.SELECT_CHAR.PacketId, CZ.SELECT_CHAR.Size);
            Register(ref lengths, CH.MAKE_CHAR.PacketId, CH.MAKE_CHAR.Size);
            Register(ref lengths, CZ.MAKE_CHAR.PacketId, CZ.MAKE_CHAR.Size);
            Register(ref lengths, HC.ACK_CHARINFO_PER_PAGE.PacketId, HC.ACK_CHARINFO_PER_PAGE.Size);
            Register(ref lengths, HC.ACCEPT_MAKECHAR.PacketId, HC.ACCEPT_MAKECHAR.Size);
            Register(ref lengths, HC.REFUSE_MAKECHAR.PacketId, HC.REFUSE_MAKECHAR.Size);
            Register(ref lengths, HC.NOTIFY_ZONESVR.PacketId, HC.NOTIFY_ZONESVR.Size);
            Register(ref lengths, ZC.ACCEPT_ENTER.PacketId, ZC.ACCEPT_ENTER.Size);
            Register(ref lengths, ZC.REFUSE_ENTER.PacketId, ZC.REFUSE_ENTER.Size);
            return lengths;
        }

        private static void Register(ref short[] lengths, ushort packetId, short size)
        {
            if (lengths[packetId] != 0 && lengths[packetId] != size)
                throw new InvalidOperationException($"Conflicting packet definitions for 0x{packetId:X4}.");
            lengths[packetId] = size;
        }

        public static int GetLength(ushort packetId) => Lengths[packetId];
    }
}
