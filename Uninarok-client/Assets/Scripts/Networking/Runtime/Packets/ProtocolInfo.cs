namespace Uninarok.Networking.Packets
{
    /// <summary>Protocol constants of the target server version (SPEC-0001 spec section 6).</summary>
    public static class ProtocolInfo
    {
        /// <summary>The client packet version sent in <c>REQUEST_LOGIN</c>. Must match the server's PACKETVER.</summary>
        public const uint PacketVersion = 20220406;

        /// <summary>
        /// Client type sent in <c>REQUEST_LOGIN</c>. Hercules stores it without interpreting it; the prototype client sent
        /// the character '0', so that stays the default.
        /// </summary>
        public const byte DefaultClientType = (byte)'0';
    }
}
