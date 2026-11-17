namespace Uninarok.Networking.Managers
{
    /// <summary>
    /// The protocol logic of one server (plan 4.9). A manager knows only the packets of its own server: what to send,
    /// what to handle, what to write into the session. It never changes flow state, never opens or closes connections and
    /// never touches a socket; it depends on <c>INetworkClient</c> and reports outcomes through events.
    /// </summary>
    public interface IServerManager
    {
        /// <summary>Subscribes the handlers (through <c>Subscriptions</c>) and resets per-server state.</summary>
        void Enter();

        /// <summary>Releases every subscription and stops the keepalive.</summary>
        void Exit();

        /// <summary>The connection to this server is up: send the opening request.</summary>
        void OnConnected();

        /// <summary>Called every frame with the monotonic clock: drives the keepalive.</summary>
        void Tick(long nowMs);
    }
}
