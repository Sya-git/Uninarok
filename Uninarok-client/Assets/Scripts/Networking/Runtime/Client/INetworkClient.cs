using System;
using Uninarok.Networking.Core.Dispatch;
using Uninarok.Networking.Core.Transport;

namespace Uninarok.Networking.Client
{
    public enum ConnectionStatus
    {
        Idle,
        Connecting,
        Connected,
    }

    /// <summary>
    /// The single connection point the game uses to talk to the servers (SPEC-0001 FR-20; plan 4.7). Managers and the flow
    /// depend on this interface, never on <c>TcpConnection</c>, so they can be tested with a simulated connection.
    /// Main thread only.
    /// </summary>
    public interface INetworkClient
    {
        ConnectionStatus Status { get; }

        /// <summary>Role of the current or pending connection; meaningful unless <see cref="Status"/> is <c>Idle</c>.</summary>
        ConnectionRole Role { get; }

        /// <summary>Drops any current or pending connection silently, then starts connecting.</summary>
        void Connect(string host, ushort port, ConnectionRole role);

        /// <summary>Drops the current or pending connection. Raises no event and logs nothing: it is intentional (FR-7).</summary>
        void Disconnect();

        /// <summary>True when the packet was queued; false unless connected and queued (FR-8).</summary>
        bool Send<T>(in T packet) where T : struct, IOutgoingPacket;

        /// <summary>The next <paramref name="count"/> bytes received are raw data, not packets (FR-3). Call BEFORE sending the request that provokes them.</summary>
        void SkipNextBytes(int count);

        void Subscribe<T>(PacketHandler<T> handler) where T : struct;
        void Unsubscribe<T>(PacketHandler<T> handler) where T : struct;

        /// <summary>The connection is up. Main thread.</summary>
        event Action<ConnectionRole> Connected;

        /// <summary>The connection ended, after every packet received before the close was delivered. Connect failures are included. Main thread.</summary>
        event Action<ConnectionRole, CloseInfo> Disconnected;
    }
}
