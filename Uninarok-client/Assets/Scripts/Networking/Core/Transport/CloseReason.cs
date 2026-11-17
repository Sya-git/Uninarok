namespace Uninarok.Networking.Core.Transport
{
    /// <summary>Why a connection ended (SPEC-0001 FR-7).</summary>
    public enum CloseReason
    {
        /// <summary>We closed it on purpose (Dispose / Disconnect). Never logged as an error.</summary>
        LocalDispose,
        /// <summary>The server closed the socket.</summary>
        RemoteClosed,
        /// <summary>I/O error, or the send backlog limit was exceeded.</summary>
        Error,
        /// <summary>The stream is not valid (framing fault, receive buffer overflow).</summary>
        ProtocolFault,
        /// <summary>The connection could not be established.</summary>
        ConnectFailed,
    }
}
