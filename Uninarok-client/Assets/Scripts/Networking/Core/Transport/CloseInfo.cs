namespace Uninarok.Networking.Core.Transport
{
    /// <summary>Immutable description of how a connection closed (SPEC-0001 FR-7).</summary>
    public sealed class CloseInfo
    {
        public CloseInfo(CloseReason reason, string message)
        {
            Reason = reason;
            Message = message;
        }

        public CloseReason Reason { get; }

        /// <summary>Human-readable detail; may be null (for example for a local dispose).</summary>
        public string Message { get; }

        public override string ToString() => Message == null ? Reason.ToString() : Reason + ": " + Message;
    }
}
