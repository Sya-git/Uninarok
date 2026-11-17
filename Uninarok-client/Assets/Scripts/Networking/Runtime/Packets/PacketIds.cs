namespace Uninarok.Networking.Packets
{
    /// <summary>
    /// Packet IDs for PACKETVER 20220406 (Hercules). One constant per implemented packet, named as its doc in
    /// docs/specs/0001-networking-core/Packets. IDs are unique per direction; the generator and a test enforce it.
    /// </summary>
    public static class PacketIds
    {
        // Login server
        public const ushort REQUEST_LOGIN = 0x0064;          // out
        public const ushort LOGIN_REFUSAL = 0x006A;          // in
        public const ushort LOGIN_SERVER_NOTICE = 0x0081;    // in
        public const ushort ACCEPT_LOGIN = 0x0AC4;           // in
        public const ushort REFUSE_LOGIN_3 = 0x0B02;         // in
    }
}
