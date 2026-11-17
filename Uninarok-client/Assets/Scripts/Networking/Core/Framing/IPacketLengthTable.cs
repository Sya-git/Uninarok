namespace Uninarok.Networking.Core.Framing
{
    /// <summary>Packet length lookup used by the framer (SPEC-0001 FR-1, FR-19; plan 4.3).</summary>
    public interface IPacketLengthTable
    {
        /// <summary>-1 variable (u16 total length follows the ID), -2 unknown ID, otherwise the fixed total length including the 2-byte ID (>= 2).</summary>
        int GetLength(ushort packetId);
    }
}
