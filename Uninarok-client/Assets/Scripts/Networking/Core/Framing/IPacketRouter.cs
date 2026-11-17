using System;

namespace Uninarok.Networking.Core.Framing
{
    /// <summary>Receives complete packets from the framer, on the main thread (SPEC-0001 FR-4, FR-19; plan 4.3).</summary>
    public interface IPacketRouter
    {
        /// <summary>
        /// Handles one packet. <paramref name="payload"/> excludes the ID and the length field and is valid only during the call.
        /// Returns false when the ID has no handler: the framer skips the packet by its known length.
        /// Must not call back into <c>PacketFramer.Process</c>.
        /// </summary>
        bool Route(ushort packetId, ReadOnlySpan<byte> payload);
    }
}
