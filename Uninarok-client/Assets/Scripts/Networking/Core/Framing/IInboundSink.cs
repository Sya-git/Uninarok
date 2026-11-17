using System;

namespace Uninarok.Networking.Core.Framing
{
    /// <summary>Where the socket reader thread delivers received bytes (SPEC-0001 FR-1; plan 4.3).</summary>
    public interface IInboundSink
    {
        /// <summary>Callable from any thread. Returns false when the buffer cap would be exceeded (the bytes are not stored).</summary>
        bool Append(ReadOnlySpan<byte> data);
    }
}
