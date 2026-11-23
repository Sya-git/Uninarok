namespace Uninarok.Networking.Packets
{
    /// <summary>
    /// Packet IDs for PACKETVER 20220406 (Hercules). One constant per implemented packet, named as its doc in
    /// docs/specs/0001-networking-core/Packets. IDs are unique per direction; the generator and a test enforce it.
    /// </summary>
    public static class PacketIds
    {
        // Login server
        public const ushort REQUEST_LOGIN = 0x0064;                  // out
        public const ushort LOGIN_REFUSAL = 0x006A;                  // in
        public const ushort LOGIN_SERVER_NOTICE = 0x0081;            // in
        public const ushort ACCEPT_LOGIN = 0x0AC4;                   // in
        public const ushort REFUSE_LOGIN_3 = 0x0B02;                 // in

        // Character server
        public const ushort CHARACTER_SERVER_REQUEST_ENTRY = 0x0065; // out
        public const ushort SELECT_CHARACTER = 0x0066;               // out
        public const ushort CHARACTER_DATA = 0x006B;                 // in
        public const ushort MAP_ENTER_REQUEST_REFUSED = 0x006C;      // in
        public const ushort CHARACTER_CREATION_SUCCESS = 0x0B6F;     // in (0x006D before PACKETVER_MAIN_NUM 20201007)
        public const ushort CHARACTER_CREATION_FAILED = 0x006E;      // in
        public const ushort CHARACTER_SERVER_PING = 0x0187;          // out
        public const ushort CHARACTER_SLOTS_INFO = 0x082D;           // in
        public const ushort PINCODE_LOGINSTATE = 0x08B9;             // in
        public const ushort CREATE_CHARACTER = 0x0A39;               // out
        public const ushort MAP_ENTER_REQUEST_ACCEPTED = 0x0AC5;     // in

        // Map server
        public const ushort MAP_ENTRY_REFUSAL = 0x0074;              // in
        public const ushort MAP_LOADED = 0x007D;                     // out
        public const ushort SERVER_HEARTBEAT_RESPONSE = 0x007F;      // in
        public const ushort UNIT_VANISH = 0x0080;                    // in
        public const ushort UNIT_MOVE = 0x0086;                      // in
        public const ushort PLAYER_MOVE = 0x0087;                    // in
        public const ushort UNIT_STOP_MOVE = 0x0088;                 // in
        public const ushort UNIT_ACT = 0x008A;                       // in
        public const ushort OWN_MESSAGE = 0x008E;                    // in
        public const ushort MAP_CHANGE = 0x0091;                     // in
        public const ushort UNIT_DIRECTION = 0x009C;                 // in
        public const ushort ITEM_ENTRY = 0x009D;                     // in
        public const ushort ITEM_DISAPPEAR = 0x00A1;                 // in
        public const ushort DROP_ITEM_ACK = 0x00AF;                  // in
        public const ushort PARAMETER_CHANGE = 0x00B0;               // in
        public const ushort GLOBAL_CHAT = 0x00F3;                    // out
        public const ushort UNIT_LOOK_CHANGE = 0x01D7;               // in
        public const ushort UNIT_SLIDE = 0x01FF;                     // in
        public const ushort MAP_ENTRY_ACCEPTANCE = 0x0283;           // in
        public const ushort AUTHENTICATION_ACCEPTED = 0x02EB;        // in
        public const ushort REQUEST_MOVE = 0x035F;                   // out
        public const ushort HEARTBEAT_REQUEST = 0x0360;              // out
        public const ushort CHANGE_DIRECTION = 0x0361;               // out
        public const ushort DROP_ITEM = 0x0363;                      // out
        public const ushort REQUEST_ENTER_MAP = 0x0436;              // out
        public const ushort REQUEST_ACT = 0x0437;                    // out
        public const ushort UNIT_MOVE_ENTRY = 0x09FD;                // in
        public const ushort UNIT_NEW_ENTRY = 0x09FE;                 // in
        public const ushort UNIT_STAND_ENTRY = 0x09FF;               // in
        public const ushort MAP_SERVER_CHANGE = 0x0AC7;              // in
        public const ushort ITEM_FALL_ENTRY = 0x0ADD;                // in
        public const ushort INVENTORY_LIST_NORMAL = 0x0B09;          // in
        public const ushort INVENTORY_LIST_EQUIP = 0x0B39;           // in
        public const ushort ITEM_PICKUP_ACK = 0x0B41;                // in
    }
}
