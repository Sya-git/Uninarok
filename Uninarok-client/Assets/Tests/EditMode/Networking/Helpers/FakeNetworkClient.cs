using System;
using System.Collections.Generic;
using Uninarok.Networking.Client;
using Uninarok.Networking.Core.Dispatch;
using Uninarok.Networking.Core.Transport;

namespace Uninarok.Networking.Tests
{
    /// <summary>
    /// A simulated connection for manager and flow tests (SPEC-0001 FR-20, plan 6). It never opens a socket:
    /// it records what the code under test did (connects, sends as bytes, skips) in call order, lets the test raise
    /// <c>Connected</c>/<c>Disconnected</c>, and delivers incoming packets through a real <see cref="PacketDispatcher"/>.
    /// </summary>
    public sealed class FakeNetworkClient : INetworkClient
    {
        public readonly struct ConnectCall
        {
            public readonly string Host;
            public readonly ushort Port;
            public readonly ConnectionRole Role;
            public ConnectCall(string host, ushort port, ConnectionRole role) { Host = host; Port = port; Role = role; }
        }

        public readonly struct SentPacket
        {
            public readonly ushort Id;
            public readonly byte[] Bytes;
            public SentPacket(ushort id, byte[] bytes) { Id = id; Bytes = bytes; }
        }

        public readonly PacketDispatcher Dispatcher;
        public readonly List<Exception> HandlerErrors = new List<Exception>();

        public readonly List<ConnectCall> Connects = new List<ConnectCall>();
        public readonly List<SentPacket> Sent = new List<SentPacket>();
        public readonly List<int> Skips = new List<int>();

        /// <summary>Every call in order, for example "Connect Login 127.0.0.1:6900", "Skip 4", "Send 0x0065", "Disconnect".</summary>
        public readonly List<string> Actions = new List<string>();

        public int DisconnectCalls { get; private set; }

        /// <summary>When false, <see cref="Send{T}"/> reports failure like a full backlog or a closed connection.</summary>
        public bool AcceptSends = true;

        /// <summary>
        /// When false, sends are only counted (<see cref="UncountedSends"/>): nothing is serialized or recorded, so a test can
        /// check that the code under test allocates nothing when it sends. The default records the bytes.
        /// </summary>
        public bool RecordSends = true;

        public int UncountedSends { get; private set; }

        public FakeNetworkClient()
        {
            Dispatcher = new PacketDispatcher(e => HandlerErrors.Add(e));
        }

        public ConnectionStatus Status { get; set; } = ConnectionStatus.Idle;
        public ConnectionRole Role { get; set; } = ConnectionRole.Login;

        public event Action<ConnectionRole> Connected;
        public event Action<ConnectionRole, CloseInfo> Disconnected;

        public void Connect(string host, ushort port, ConnectionRole role)
        {
            Connects.Add(new ConnectCall(host, port, role));
            Actions.Add($"Connect {role} {host}:{port}");
            Role = role;
            Status = ConnectionStatus.Connecting;
        }

        public void Disconnect()
        {
            DisconnectCalls++;
            Actions.Add("Disconnect");
            Status = ConnectionStatus.Idle;
        }

        public bool Send<T>(in T packet) where T : struct, IOutgoingPacket
        {
            if (!AcceptSends || Status != ConnectionStatus.Connected)
            {
                Actions.Add("SendFailed " + typeof(T).Name);
                return false;
            }
            if (!RecordSends)
            {
                UncountedSends++;
                return true;
            }
            int size = packet.WireSize;
            var bytes = new byte[size];
            if (size > 0) packet.Serialize(bytes);
            ushort id = size >= 2 ? (ushort)(bytes[0] | (bytes[1] << 8)) : (ushort)0;
            Sent.Add(new SentPacket(id, bytes));
            Actions.Add("Send 0x" + id.ToString("X4"));
            return true;
        }

        public void SkipNextBytes(int count)
        {
            Skips.Add(count);
            Actions.Add("Skip " + count);
        }

        public void Subscribe<T>(PacketHandler<T> handler) where T : struct => Dispatcher.Subscribe(handler);
        public void Unsubscribe<T>(PacketHandler<T> handler) where T : struct => Dispatcher.Unsubscribe(handler);

        // ---- test controls ----

        /// <summary>The pending connection is established.</summary>
        public void RaiseConnected(ConnectionRole? role = null)
        {
            if (role.HasValue) Role = role.Value;
            Status = ConnectionStatus.Connected;
            Connected?.Invoke(Role);
        }

        /// <summary>The connection ended (or never came up).</summary>
        public void RaiseDisconnected(CloseInfo info, ConnectionRole? role = null)
        {
            if (role.HasValue) Role = role.Value;
            Status = ConnectionStatus.Idle;
            Disconnected?.Invoke(Role, info);
        }

        /// <summary>Delivers an incoming packet to the subscribed handlers, as the router would.</summary>
        public void Deliver<T>(in T packet) where T : struct => Dispatcher.Dispatch(in packet);

        public int SentCount(ushort id)
        {
            int n = 0;
            foreach (var s in Sent) if (s.Id == id) n++;
            return n;
        }

        public void ClearRecords()
        {
            Connects.Clear();
            Sent.Clear();
            Skips.Clear();
            Actions.Clear();
            HandlerErrors.Clear();
            DisconnectCalls = 0;
            UncountedSends = 0;
        }
    }
}
