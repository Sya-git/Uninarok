namespace Uninarok.Networking.Flow
{
    /// <summary>Why the flow ended in <see cref="FlowState.Failed"/> (plan 4.9).</summary>
    public enum FailureCode
    {
        ConnectFailed,
        ConnectTimeout,
        ReplyTimeout,
        LoginRefused,
        RemoteClosed,
        ProtocolFault,
        TransportError,
        MapRefused,
    }
}
