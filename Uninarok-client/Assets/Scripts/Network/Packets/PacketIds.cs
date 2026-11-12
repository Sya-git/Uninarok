namespace Uninarok.Networking.Packets
{
    public enum PacketIds : ushort
    {
        // IN
        // AC
        ACCEPT_LOGIN = 0x0AC4,
        REFUSE_LOGIN = 0x0081,
        REFUSE_LOGIN_R3 = 0x0B02,

        // HC
        HC_SLOTS_INFO = 0x082D,
        HC_CHAR_DATA = 0x006B,
        HC_REFUSE_ENTER = 0x006C,
        PINCODE_LOGINSTATE = 0x08B9,
        PINCODE_LOGINSTATE2 = 0x0AE9,
        NOTIFY_ZONESVR = 0x0AC5,
        CHAR_CREATION_FAILED = 0x006E,
        CHAR_CREATION_SUCCESS = 0x006D,

        // SC
        NOTIFY_BAN = 0x0AC6,
        ACK_HASH = 0x01DC,

        // ZC
        ACCEPT_ENTER = 0x0283,
        INVENTORY_EXPANSION_INFO = 0x0B18,
        OVERWEIGHT_PERCENT = 0x0ADE,
        AUTH_OK = 0x02EB,
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
        CZ_ENTER = 0x0923,
        CZ_ENTER2 = 0x0436,
        CZ_PACKET_DB = 0x0368,
        CZ_REQUEST_TIME2 = 0x0187,
    }
}
