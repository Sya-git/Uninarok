namespace Uninarok.Networking.Packets
{
    public enum PacketIds : ushort
    {
        // IN
        // AC
        ACCEPT_LOGIN = 0x0AC4, // 0x006A
        REFUSE_LOGIN = 0x0081, //0x006B, // 0x006A?
        //REFUSE_LOGIN_R2 = 0x083E,
        REFUSE_LOGIN_R3 = 0x0B02,

        // HC
        SKIPPABLE_BYTES_UNKNOWN = 0x8481,
        HC_SLOTS_INFO = 0x082D,
        HC_CHAR_DATA = 0x006B,
        HC_REFUSE_ENTER = 0x006C,
        PINCODE_LOGINSTATE = 0x08B9,
        PINCODE_LOGINSTATE2 = 0x0AE9,
        //ACK_CHARINFO_PER_PAGE = 0x099D,
        NOTIFY_ZONESVR = 0x0AC5,
        CHAR_CREATION_FAILED = 0x006E, // WILL CHANGE IN LATER PACKET VERS
        CHAR_CREATION_SUCCESS = 0x006D, // WILL CHANGE IN LATER PACKET VERS, 0x0B6F

        // SC
        NOTIFY_BAN = 0x0AC6, // 0x0081?
        ACK_HASH = 0x01DC,

        // ZC
        ACCEPT_ENTER = 0x02EB,
        REFUSE_ENTER = 0x0074,
        ZC_ACK_PING = 0x0188,

        // OUT
        // CA
        LOGIN = 0x0064,
        
        // CH
        CH_ENTER = 0x0065,
        SELECT_CHAR = 0x0066,
        MAKE_CHAR = 0x0A39,

        // CZ
        CZ_ENTER = 0x0923, //TODO: cagado
        CZ_PACKET_DB = 0x0368,
        CZ_REQUEST_TIME2 = 0x0187,
    }
}
