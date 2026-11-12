namespace Uninarok.Networking.Packets
{
    public enum PacketIds : ushort
    {
        // IN
        // AC
        ACCEPT_LOGIN = 0x0AC4,
        REFUSE_LOGIN = 0x0081,
        REFUSE_LOGIN_3 = 0x0B02,

        // HC
        CHARACTER_SLOTS_INFO = 0x082D,
        CHARACTER_DATA = 0x006B,
        MAP_ENTER_REQUEST_REFUSED = 0x006C,
        PINCODE_LOGINSTATE = 0x08B9,
        MAP_ENTER_REQUEST_ACCEPTED = 0x0AC5,
        CHARACTER_CREATION_FAILED = 0x006E,
        CHARACTER_CREATION_SUCCESS = 0x006D,

        // ZC
        MAP_ACCEPT_ENTER = 0x0283,
        MAP_REFUSE_ENTER = 0x0074,
        INVENTORY_EXPANSION_INFO = 0x0B18,
        OVERWEIGHT_PERCENT = 0x0ADE,
        AUTH_OK = 0x02EB,
        HEARTBEAT_RESPONSE = 0x0188,

        // OUT
        // CA
        LOGIN = 0x0064,
        
        // CH
        CHARACTER_SERVER_REQUEST_ENTRY = 0x0065,
        SELECT_CHARACTER = 0x0066,
        CREATE_CHARACTER = 0x0A39,

        // CZ
        REQUEST_ENTER_MAP = 0x0436,
        SEND_PACKET_VER = 0x0368,
        MAP_HEARTBEAT = 0x0187,
    }
}
