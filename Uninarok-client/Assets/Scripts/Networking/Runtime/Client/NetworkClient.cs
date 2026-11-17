using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Uninarok.Networking.Core.Dispatch;
using Uninarok.Networking.Core.Framing;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Transport;
using Uninarok.Networking.Flow;
using Uninarok.Networking.Packets;

namespace Uninarok.Networking.Client
{
    /// <summary>
    /// The single connection point (SPEC-0001 FR-5, FR-7, FR-8, FR-16, FR-20; plan 4.7). The only <c>MonoBehaviour</c> of the
    /// networking layer and the only owner of a <see cref="TcpConnection"/>: pure transport plus scheduling. It connects,
    /// pumps incoming packets once per frame on the main thread, sends, and reports how a connection ended.
    /// Initialization is lazy, so it also works on a component in EditMode tests where <c>Awake</c> does not run.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class NetworkClient : MonoBehaviour, INetworkClient
    {
        [SerializeField] private string loginHost = "127.0.0.1";
        [SerializeField, Range(1, 65535)] private int loginPort = 6900;
        [SerializeField, Min(1)] private int maxPacketsPerFrame = 256;
        [SerializeField] private NetworkSettings settings = new NetworkSettings();

        private PacketDispatcher _dispatcher;
        private GeneratedPacketRouter _router;

        private TcpConnection _connection;
        private PacketFramer _framer;
        private Task<TcpConnection> _pending;
        private CancellationTokenSource _pendingCts;
        private ConnectionRole _role;
        private ConnectionStatus _status = ConnectionStatus.Idle;
#if UNINAROK_NET_TRACE
        private CaptureTee _capture;
#endif

        public static NetworkClient Instance { get; private set; }

        private NetworkFlow _flow;

        /// <summary>The login, character and map flow. The game talks to the servers through it. Built on first use.</summary>
        public NetworkFlow Flow
        {
            get
            {
                EnsureInitialized();
                return _flow;
            }
        }

        public ConnectionStatus Status => _status;
        public ConnectionRole Role => _role;

        public NetworkSettings Settings => settings;
        public string LoginHost => loginHost;
        public ushort LoginPort => (ushort)Mathf.Clamp(loginPort, 1, 65535);

        /// <summary>Most packets framed per <see cref="Pump"/> call (bounded work per frame, FR-5).</summary>
        internal int MaxPacketsPerFrame
        {
            get => maxPacketsPerFrame;
            set => maxPacketsPerFrame = Math.Max(1, value);
        }

        public event Action<ConnectionRole> Connected;
        public event Action<ConnectionRole, CloseInfo> Disconnected;

        // ---- lifecycle ----

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[Net] A second NetworkClient was created; destroying it.");
                Destroy(this);
                return;
            }
            Instance = this;
            if (Application.isPlaying) DontDestroyOnLoad(gameObject);
            EnsureInitialized();
        }

        // Pump first, so replies received this frame are seen before the flow checks its deadlines.
        private void Update()
        {
            Pump();
            _flow?.Tick();
        }

        private void OnDestroy()
        {
            _flow?.Dispose();
            Disconnect();
            if (Instance == this) Instance = null;
        }

        private void EnsureInitialized()
        {
            if (_dispatcher != null) return;
            NetProfiling.Install();                             // socket threads show up in the Profiler (T8.1)
            _dispatcher = new PacketDispatcher(NetLog.HandlerError);
            _router = new GeneratedPacketRouter(_dispatcher, NetLog.DecodeError);
            _flow = new NetworkFlow(this, new UnityClock(), settings,
                                   new ServerEndpoint(loginHost, LoginPort, default, 0, 0, 0));
        }

        // ---- INetworkClient ----

        public void Connect(string host, ushort port, ConnectionRole role)
        {
            EnsureInitialized();
            DropConnection();                                   // silent: the previous connection is replaced

            _role = role;
            _status = ConnectionStatus.Connecting;
            _framer = new PacketFramer(HerculesPacketTable.Instance, _router);
            var cts = new CancellationTokenSource();
            _pendingCts = cts;
            IInboundSink sink = _framer;
#if UNINAROK_NET_TRACE
            // Record the received bytes for replay fixtures: persistentDataPath/captures/{role}-{timestamp}.bin
            string capturePath = System.IO.Path.Combine(Application.persistentDataPath, "captures", CaptureTee.FileName(role.ToString(), DateTime.Now));
            _capture = new CaptureTee(sink, capturePath);
            sink = _capture;
            NetLog.Trace("capturing to " + capturePath);
#endif
            CancellationToken token = cts.Token;
            // Off the main thread: resolving a host name can block.
            _pending = Task.Run(() => TcpConnection.ConnectAsync(host, port, role, sink, token), token);
            NetLog.Trace($"connecting {role} {host}:{port}");
        }

        public void Disconnect() => DropConnection();

        public bool Send<T>(in T packet) where T : struct, IOutgoingPacket
        {
            if (_status != ConnectionStatus.Connected || _connection == null) return false;
            return _connection.Send(in packet);
        }

        public void SkipNextBytes(int count)
        {
            if (_framer != null) _framer.SkipNextBytes(count);
        }

        public void Subscribe<T>(PacketHandler<T> handler) where T : struct
        {
            EnsureInitialized();
            _dispatcher.Subscribe(handler);
        }

        public void Unsubscribe<T>(PacketHandler<T> handler) where T : struct
        {
            EnsureInitialized();
            _dispatcher.Unsubscribe(handler);
        }

        // ---- the per-frame pump (main thread) ----

        /// <summary>Called by <see cref="Update"/>; tests call it directly.</summary>
        internal void Pump()
        {
            if (_pending != null && _pending.IsCompleted && !CompleteConnect()) return;
            if (_connection == null) return;

            PacketFramer framer = _framer;
            TcpConnection connection = _connection;

            // Read this BEFORE processing: if the connection was already closed, every byte it ever received is in the
            // framer, so processing everything left delivers the packets that preceded the close (FR-7).
            bool wasClosed = connection.IsClosed;

            int framed = 0;
            try
            {
                framed = framer.Process(maxPacketsPerFrame);
            }
            catch (Exception e)
            {
                Debug.LogException(e);                          // defensive: Process leaves the framer consistent
            }
            if (_framer != framer) return;                      // a handler replaced or dropped the connection

            if (framer.IsFaulted)
            {
                connection.Close(CloseReason.ProtocolFault, framer.FaultReason);   // first close wins
                FinishClosed(connection);
                return;
            }

            if (wasClosed && framed < maxPacketsPerFrame)
                FinishClosed(connection);                       // drained: nothing more can arrive
        }

        // Observes the finished connect task. Returns true when a connection is up and the pump should go on.
        private bool CompleteConnect()
        {
            Task<TcpConnection> task = _pending;
            CancellationTokenSource cts = _pendingCts;
            _pending = null;
            _pendingCts = null;
            cts?.Dispose();

            if (task.Status == TaskStatus.RanToCompletion)
            {
                _connection = task.Result;
                _status = ConnectionStatus.Connected;
                Raise(() => Connected?.Invoke(_role));
                return _connection != null && _framer != null;
            }

            string message = task.IsCanceled ? "Connect was canceled" : task.Exception?.GetBaseException().Message;
            _framer?.Stop();
            _framer = null;
            _status = ConnectionStatus.Idle;
#if UNINAROK_NET_TRACE
            _capture?.Dispose();
            _capture = null;
#endif
            var info = new CloseInfo(CloseReason.ConnectFailed, message);
            ConnectionRole role = _role;
            NetLog.ReportClosure(role, info);
            Raise(() => Disconnected?.Invoke(role, info));
            return false;
        }

        private void FinishClosed(TcpConnection connection)
        {
            ConnectionRole role = _role;
            CloseInfo info = connection.Closure;
            DropConnection();
            NetLog.ReportClosure(role, info);
            Raise(() => Disconnected?.Invoke(role, info));
        }

        // Stops and releases the current or pending connection. Silent.
        private void DropConnection()
        {
            _framer?.Stop();                                    // a handler that reconnects mid-pump ends the old stream at once
            _framer = null;

            if (_connection != null)
            {
                _connection.Dispose();
                _connection = null;
            }

            if (_pending != null)
            {
                Task<TcpConnection> task = _pending;
                CancellationTokenSource cts = _pendingCts;
                _pending = null;
                _pendingCts = null;
                try { cts?.Cancel(); } catch (Exception) { }
                // A connect that completed anyway must not leak its socket and threads.
                task.ContinueWith(t =>
                {
                    try
                    {
                        if (t.Status == TaskStatus.RanToCompletion) t.Result.Dispose();
                        else _ = t.Exception;                  // observe, so the exception is not reported as unobserved
                    }
                    finally
                    {
                        cts?.Dispose();
                    }
                }, TaskScheduler.Default);
            }

            _status = ConnectionStatus.Idle;
#if UNINAROK_NET_TRACE
            _capture?.Dispose();
            _capture = null;
#endif
        }

        // User event handlers must not break the pump.
        private static void Raise(Action raise)
        {
            try { raise(); }
            catch (Exception e) { NetLog.HandlerError(e); }
        }
    }
}
