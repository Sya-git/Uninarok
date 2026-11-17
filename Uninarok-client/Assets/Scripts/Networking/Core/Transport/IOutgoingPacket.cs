using System;

namespace Uninarok.Networking.Core.Transport
{
    /// <summary>
    /// Implemented by every outgoing packet, which is a <c>readonly struct</c>. Only used as a constrained generic
    /// (<c>Send&lt;T&gt;</c>), so it is never boxed (SPEC-0001 FR-6, FR-8; plan 4.4).
    /// </summary>
    public interface IOutgoingPacket
    {
        /// <summary>Exact total bytes: ID, length field (if variable) and payload.</summary>
        int WireSize { get; }

        /// <summary>Writes exactly <see cref="WireSize"/> bytes into <paramref name="destination"/>. Must not allocate.</summary>
        void Serialize(Span<byte> destination);
    }
}
