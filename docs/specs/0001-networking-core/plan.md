# PLAN-0001: Networking Core

| | |
|---|---|
| **Implements** | [spec.md](spec.md) (SPEC-0001) |
| **Status** | Draft |
| **Audience** | Architects, senior developers, AI agents |

> This file defines **how**. Requirement IDs (`FR-n`) refer to `spec.md`. Where a file already exists in the repo, the task updates it; where it does not, the task creates it. File paths are proposals: confirm them in task T0.2 and adjust here if they differ.

## 1. Constraints and assumptions

- Unity 6 (the code uses `Unity.Scripting.LifecycleManagement`), C# with target-typed `new()`, .NET Standard 2.1 profile.
- **Threading rule:** the socket thread never calls game code. Parsing, dispatch and all `UnityEngine` API use happen on the main thread.
- Hercules framing: little-endian `ushort` packet ID, then either a fixed total length or (variable) a `ushort` total length that includes the 4-byte header.
- `GetLength(id)` contract (generated): `-1` variable, `-2` unknown, `>= 2` fixed total length including header. T0.3 verifies the generator never emits `0`. If it does, fix the generator, not the framer.
- Payload passed to handlers **excludes** the ID and length fields (current behaviour, preserved).
- No new third-party libraries. Dependencies: Unity Test Framework (NUnit), `System.Buffers` (`ArrayPool`, rare variable-size cases only), `System.Buffers.Binary`.
- **GC policy (FR-6):** the main thread allocates nothing per packet in steady state, in either direction. Unity's Boehm GC is global and stop-the-world (incremental mode softens it), so allocations on socket threads also raise collection frequency. See §4.9.

## 2. Architecture

```
 socket threads (dedicated)             main thread (Unity)
┌───────────────┐  bytes   ┌────────────────────────────────────────────────────┐
│ TcpConnection │─────────▶│ NetworkRuntime.Update  [ExecutionOrder -100]       │
│  reader thread│ inbox    │   1. conn.ProcessPackages(max)                     │
│  (blocking    │ (locked) │        └▶ UninarokPacketsSerializer (Core asmdef)  │
│   Receive)    │          │              └▶ IPacketRouter.Route(id, span)      │
│               │          │                    └▶ GeneratedPacketRouter (try/catch)
│  writer thread│◀─────────│                          └▶ UninarokPackageDispatcher.Dispatch<T>
│  (blocking    │ outbox   │                                └▶ session handlers (per-callback try/catch)
│   Send)       │ (locked) │   2. if conn closed AND drained → NetworkFlow.OnConnectionClosed
└───────────────┘          │   3. NetworkFlow.Tick(now): timeouts, heartbeat    │
                           │   Sessions call conn.Send<T>(in packet): serialize │
 ┌───────────────┐         │   straight into the outbox (no intermediate array) │
 │  NetworkFlow  │◀────────│                                                    │
 │ state machine │         └────────────────────────────────────────────────────┘
 │ SessionContext│
 │ Login/Char/Map│ ◀── UninarokLoginBootstrap (dev only, reacts to StateChanged)
 └───────────────┘
```

One `NetworkRuntime` pumps the single active connection (the flow replaces login → char → map connections one at a time). Sessions are plain C# objects owned by `NetworkFlow`, not separate MonoBehaviours.

## 3. Assemblies and file layout

| Path | Contents | Asmdef |
|---|---|---|
| `Assets/Scripts/Networking/Core/` | `IPacketRouter`, `UninarokPacketsSerializer`, `PacketReader`, `PacketWriter`, `PacketFormatException` | `Uninarok.Networking.Core` (`noEngineReferences: true`) |
| `Assets/Scripts/Networking/` | `TcpConnection`, `IOutgoingPacket`, `CloseInfo`, `ConnectionRole`, `NetLog`, `GeneratedPacketRouter`, `Subscriptions`, `UninarokPackageDispatcher`, `NetworkRuntime` | `Uninarok.Networking` (references Core) |
| `Assets/Scripts/Networking/Flow/` | `NetworkFlow`, `FlowState`, `FlowFailure`, `SessionContext`, `ISession`, `LoginSession`, `CharSession`, `MapSession`, `NetworkTimeouts`, `IClock` | `Uninarok.Networking` |
| `Assets/Scripts/Networking/Dev/` | `UninarokLoginBootstrap` (wrapped in `#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD`) | `Uninarok.Networking` |
| `Assets/Tests/EditMode/Networking/` | tests, `Captures/*.bin` + `*.expected.txt` | `Uninarok.Networking.Tests` (EditMode, references Core + Networking) |

Removed at the end: `LoginManager.cs`, `GameConnectionManager.cs`, old `UninarokPacketsSerializer.cs` location.

## 4. Component contracts

### 4.1 Packet framing — `UninarokPacketsSerializer` (Core)
Satisfies FR-1, FR-2, FR-3, FR-5, FR-6, FR-17.

```csharp
namespace Uninarok.Networking.Core
{
    public interface IPacketRouter
    {
        int  GetLength(ushort packetId);                          // -1 variable, -2 unknown, >=2 fixed
        void Route(ushort packetId, ReadOnlySpan<byte> payload);  // span valid only during the call
    }

    public sealed class UninarokPacketsSerializer
    {
        public const int Variable = -1, Unknown = -2;
        public const int DefaultMaxBufferedBytes = 4 * 1024 * 1024;

        public UninarokPacketsSerializer(IPacketRouter router, int maxBufferedBytes = DefaultMaxBufferedBytes);

        public bool   IsFaulted   { get; }   // main thread
        public string FaultReason { get; }   // reason + up to 32 next bytes as hex

        public bool AppendData(byte[] data, int length);   // ANY thread. false => cap exceeded, caller closes connection
        public void SkipNextBytes(int amount);             // MAIN thread. Must be called BEFORE sending the request that provokes the bytes
        public int  ProcessPackages(int maxPackets = 256); // MAIN thread. Returns packets routed. 0 once faulted
    }
}
```

**Design (D1):** two buffers. The socket thread appends to a locked `_inbox`. `ProcessPackages` takes the lock only to move the inbox into a main-thread-only `_work` buffer (compacting first), then parses `_work` with **no lock held** (FR-5, no blocking of the socket thread during dispatch). Both buffers grow by doubling up to `maxBufferedBytes` each.

**Algorithm (per `ProcessPackages` call):**
1. If faulted, return 0. Drain inbox into `_work`.
2. Loop while `processed < maxPackets`:
   1. If `_skip > 0`: consume `min(_skip, available)`; if `_skip` still > 0, break; else continue. *(Runs before any header parsing: FR-3.)*
   2. If `available < 2`, break. Read `id` (LE). If `id == 0`, fault `"Packet ID 0"`.
   3. `len = router.GetLength(id)`. If `-2`, fault `"Unknown packet 0x{id:X4}"`. If `len` is not `-1` and `len < 2`, fault `"Invalid table length"`.
   4. If `-1` (variable): if `available < 4`, break; read `len = u16 @ +2`; if `len < 4`, fault `"Bad length"`. Header size is 4. Otherwise header size is 2.
   5. If `available < len`, break (wait for more data).
   6. `payload = work[read+header .. read+len]`. **Advance `_read += len` first**, then `router.Route(id, payload)`. `processed++`.
3. If `_read == _write`, reset both to 0.

A fault stores `FaultReason` and stops parsing. **No resync is attempted (D3):** with unknown lengths, a TCP stream cannot be re-aligned safely, so the connection is closed.

**Rules:** the serializer does no logging (Core has no engine reference). `Route` must not call `ProcessPackages`. If `Route` throws, state is already consistent. The exception propagates and the next call continues with the next packet (the Unity adapter catches, see 4.3).

### 4.2 Transport — `TcpConnection`
Satisfies FR-2, FR-6, FR-7, FR-8.

```csharp
public enum ConnectionRole { Login, Char, Map }
public enum CloseReason    { LocalDispose, RemoteClosed, Error, ProtocolFault }
public sealed class CloseInfo { public CloseReason Reason; public string Message; }   // immutable

// Emitted by the generator for every outgoing packet (prefer `readonly struct`).
// Only ever used as a constrained generic, so it is never boxed (Mono and IL2CPP).
public interface IOutgoingPacket
{
    int  WireSize { get; }                    // exact total bytes: header + length field + payload
    void Serialize(Span<byte> destination);   // writes exactly WireSize bytes; must not allocate
}

internal sealed class TcpConnection : IDisposable
{
    public static Task<TcpConnection> ConnectAsync(string host, int port, ConnectionRole role,
                                                   IPacketRouter router, CancellationToken token); // one-shot; may allocate
    public ConnectionRole Role { get; }
    public bool IsClosed { get; }            // _closure != null
    public bool IsConnected => !IsClosed;    // kept for compatibility
    public CloseInfo Closure { get; }        // null until closed
    public bool ClosureReported { get; set; }// main thread, set by NetworkRuntime

    public void SkipNextBytes(int amount);   // delegates to serializer
    public int  ProcessPackages(int maxPackets); // delegates; closes with ProtocolFault if serializer faults

    // Main thread. Never blocks on I/O. Never throws because of connection state.
    // true  = queued (NOT "sent"). false = not queued (closed, or backlog limit hit).
    public bool Send<T>(in T packet) where T : struct, IOutgoingPacket;
    public bool Send(ReadOnlySpan<byte> rawPacket);   // pre-serialized bytes; copied into the outbox

    public void Dispose();                   // idempotent
}
```

**Send path (D7, D9, D10).** Mirror image of the receive path: two buffers and one short lock.

```csharp
private readonly object _outLock = new();
private byte[] _outbox   = new byte[16 * 1024];   // appended to under _outLock (any thread; main thread in practice)
private byte[] _writeBuf = new byte[16 * 1024];   // owned by the writer thread
private int  _outLength;
private bool _writerIdle = true;                  // guarded by _outLock
private readonly AutoResetEvent _outSignal = new(false);
private const int MaxSendBacklogBytes = 1024 * 1024;

public bool Send<T>(in T packet) where T : struct, IOutgoingPacket
{
    int size = packet.WireSize;
    if (size <= 0) return true;                               // empty: no-op
    bool overflow = false;
    lock (_outLock)
    {
        if (_closure != null) return false;
        if (_outLength + size > MaxSendBacklogBytes) overflow = true;
        else
        {
            if (_outLength + size > _outbox.Length) Array.Resize(ref _outbox, Grow(_outLength + size)); // rare, amortized
            packet.Serialize(new Span<byte>(_outbox, _outLength, size));  // if this throws, _outLength is not advanced
            _outLength += size;
            if (_writerIdle) { _writerIdle = false; _outSignal.Set(); }
        }
    }
    if (overflow) { TryClose(CloseReason.Error, "Send backlog exceeded"); return false; }
    return true;
}

private void WriteLoop()                                      // dedicated thread "Uninarok.Net.{role}.Write"
{
    try
    {
        while (true)
        {
            _outSignal.WaitOne();
            if (_closure != null) return;
            while (true)
            {
                int n;
                lock (_outLock)
                {
                    if (_outLength == 0) { _writerIdle = true; break; }
                    (_outbox, _writeBuf) = (_writeBuf, _outbox);   // swap; no lock held during I/O
                    n = _outLength; _outLength = 0;
                }
                for (int sent = 0; sent < n; )
                    sent += _socket.Send(_writeBuf, sent, n - sent, SocketFlags.None);
            }
        }
    }
    catch (Exception) when (_closure != null) { }                 // our own shutdown
    catch (Exception e) { TryClose(CloseReason.Error, e.ToString()); }
}

private void ReadLoop()                                       // dedicated thread "Uninarok.Net.{role}.Read"
{
    var buf = new byte[32 * 1024];                            // allocated once per connection
    try
    {
        while (true)
        {
            int n = _socket.Receive(buf, 0, buf.Length, SocketFlags.None);
            if (n == 0) { TryClose(CloseReason.RemoteClosed, "Server closed the connection"); return; }
            if (!_serializer.AppendData(buf, n)) { TryClose(CloseReason.ProtocolFault, "Receive buffer overflow"); return; }
        }
    }
    catch (Exception) when (_closure != null) { }
    catch (Exception e) { TryClose(CloseReason.Error, e.ToString()); }
}
```

- **Semantics of `Send`:** `true` means queued. Packets are committed whole or not at all: a packet is only counted after `Serialize` succeeds, so there are no partial packets, and order equals call order (also across threads, because of the lock). A `Serialize` that throws is a programmer error and propagates, leaving the outbox intact.
- **Backlog limit:** outgoing client traffic is tiny. Exceeding 1 MiB means the server stopped reading, so the connection is closed with `Error` rather than growing without bound.
- **Idle flag:** the signal is only set on the idle → busy transition, under the same lock as the buffer, so there is no lost wake-up and no stream of redundant signals.
- **Why dedicated threads with blocking I/O (D9):** `AutoResetEvent.Set()`, `Socket.Send` and `Socket.Receive` allocate nothing, so the steady-state send and receive paths allocate nothing at all. The `async` alternatives (`NetworkStream.ReadAsync/WriteAsync`, `SemaphoreSlim.WaitAsync`) create `Task` objects per call on at least some runtimes. One connection is active at a time, so two threads is negligible. This is isolated inside `TcpConnection`; if profiling (T0.6) shows no benefit it can be reverted without touching any contract.
- **Closure (D6):** `private CloseInfo _closure` set once with `Interlocked.CompareExchange(ref _closure, info, null)`. First close wins. Everything that closes (read of 0 bytes, I/O error, `AppendData == false`, serializer fault, backlog overflow, `Dispose`) goes through `TryClose(reason, message)`, which also wakes the writer with `_outSignal.Set()`.
- **Shutdown-quiet (FR-7):** `SocketException`, `ObjectDisposedException` and `IOException` raised while `_closure != null` are swallowed (the filters above). Any other exception calls `TryClose(Error, ex.ToString())`.
- **Waking blocked threads:** `Dispose()` calls `TryClose(LocalDispose)`, then `socket.Shutdown(SocketShutdown.Both)` (inside try/catch) **before** closing the socket, because closing alone does not reliably wake a blocked `Receive` on every platform. `_outSignal` is disposed by the last thread to exit (`Interlocked.Decrement` on a live-thread counter), never by `Dispose()`, so a waiting writer never hits a disposed handle. When the *server* closes first, the exiting reader disposes socket and client.
- **Allocation notes:** `Send<T>` is generic over a struct, so there is no boxing. A non-`readonly` struct passed by `in` may get a defensive copy, which lives on the stack and is not GC. Buffers are pre-sized to 16 KB, so growth is rare and amortized.
- **Awaiting rule:** only `ConnectAsync` is awaitable. It uses `ConfigureAwait(false)` internally and never touches Unity APIs (`Debug.Log` is allowed). Callers `await` it without `ConfigureAwait(false)` so continuations return to the main thread.
- **Trace capture (optional, under `UNINAROK_NET_TRACE`):** append received bytes to `Application.persistentDataPath/captures/{role}-{yyyyMMdd-HHmmss}.bin` for building replay fixtures.

### 4.3 Router adapter, dispatcher, subscriptions
Satisfies FR-4, FR-5, FR-9, FR-16.

```csharp
// Unity assembly. Wraps the generated static router.
internal sealed class GeneratedPacketRouter : IPacketRouter
{
    public static readonly GeneratedPacketRouter Instance = new();
    public int GetLength(ushort id) => UninarokNetworkingRouter.GetLength(id);
    public void Route(ushort id, ReadOnlySpan<byte> payload)
    {
        NetLog.Trace(id);   // [Conditional("UNINAROK_NET_TRACE")]
        try { UninarokNetworkingRouter.RoutePacket(id, payload); }
        catch (Exception e) { Debug.LogException(e); }
    }
}
```

- **Generated router signature (SER-12):** `RoutePacket(ushort id, ReadOnlySpan<byte> payload)`. Update the Roslyn generator template so deserializers read from the span. Generated deserializers must not retain the span. Structs that contain arrays or strings still allocate; see the per-packet rules in §4.9. Fallback if the generator change is large: adapter copies to `payload.ToArray()`. That keeps one allocation per packet, so it is a deferral of FR-6 and needs human approval (FR-6 is a Must).
- **Dispatcher (FR-4):** per-callback `try/catch` in `Dispatch<T>`, logging via `Debug.LogException` and continuing. Keep backward iteration. Document in XML comments: removing *itself* during dispatch is safe; removing a *different* handler may skip or repeat one callback in that dispatch.
- **`Subscriptions` helper (FR-9):**

```csharp
public sealed class Subscriptions : IDisposable
{
    private readonly List<Action> _undo = new();
    public void Add<T>(UninarokPackageDispatcher.InAction<T> cb) where T : struct
    {
        UninarokPackageDispatcher.Subscribe(cb);
        _undo.Add(() => UninarokPackageDispatcher.Unsubscribe(cb));
    }
    public void Dispose() { foreach (var u in _undo) u(); _undo.Clear(); }
}
```

- **`NetLog` (FR-16):** `[Conditional("UNINAROK_NET_TRACE")] Trace(...)`; `ReportClosure(ConnectionRole, CloseInfo)` logs once at `LogWarning` (`RemoteClosed`) or `LogError` (`Error`, `ProtocolFault`); nothing for `LocalDispose`. State transitions log at `Log`. No informational `LogError`.

### 4.4 Pump — `NetworkRuntime`
Satisfies FR-5, FR-7, FR-10.

```csharp
[DefaultExecutionOrder(-100)]
public sealed class NetworkRuntime : MonoBehaviour
{
    public static NetworkRuntime Instance { get; }
    [SerializeField] int maxPacketsPerFrame = 256;
    [SerializeField] NetworkTimeouts timeouts;
    public NetworkFlow Flow { get; }

    void Update()
    {
        var c = Flow.ActiveConnection;
        if (c != null)
        {
            int n = c.ProcessPackages(maxPacketsPerFrame);
            if (c.IsClosed && !c.ClosureReported && n < maxPacketsPerFrame)  // drained
            {
                c.ClosureReported = true;
                NetLog.ReportClosure(c.Role, c.Closure);
                Flow.OnConnectionClosed(c);       // ignores connections that are no longer active
            }
        }
        Flow.Tick(Time.realtimeSinceStartup);
    }
}
```

**Drain-before-close rule:** closure is only delivered after a `ProcessPackages` call that returned below the cap. Nothing more can arrive after close, so this guarantees that packets sent just before the server closed the socket (for example a login refusal) are handled first. No other class may call `ProcessPackages` (grep check in tests).

### 4.5 Flow and sessions
Satisfies FR-10, FR-11, FR-12, FR-13.

```csharp
public enum FlowState
{
    Idle, LoggingIn, ServerSelection, ConnectingToChar, CharSelection,
    CreatingCharacter, SelectingCharacter, ConnectingToMap, LoadingMap, InGame,
    Failed, Disconnected
}
public enum FailureCode { ConnectFailed, ConnectTimeout, ReplyTimeout, LoginRefused,
                          RemoteClosed, ProtocolFault, MapRefused, Internal }
public readonly struct FlowFailure { public FailureCode Code; public string Message; }

public sealed class NetworkFlow
{
    public FlowState State { get; }
    public FlowFailure? Failure { get; }
    public SessionContext Context { get; }
    internal TcpConnection ActiveConnection { get; }
    public event Action<FlowState> StateChanged;               // main thread only

    // Commands (main thread). Each validates the current state and warns/ignores otherwise.
    public void StartLogin(string user, string password);
    public void SelectServer(int index);
    public void CreateCharacter(CharacterDraft draft);
    public void SelectCharacter(byte slot);
    public void Reset();                                       // Failed/Disconnected -> Idle, disposes connection

    internal void OnConnectionClosed(TcpConnection c);
    internal void Tick(float now);
}
```

**Transition table (anything else is rejected with a warning, FR-10):**

| From | Event | To |
|---|---|---|
| Idle | `StartLogin` | LoggingIn |
| LoggingIn | `ACCEPT_LOGIN` (login connection disposed by flow) | ServerSelection |
| LoggingIn | `LOGIN_REFUSAL` (0x006A), `REFUSE_LOGIN_3` (0x0B02) or `LOGIN_SERVER_NOTICE` (0x0081, ban notice) | Failed(`LoginRefused`) |
| ServerSelection | `SelectServer` | ConnectingToChar |
| ConnectingToChar | `CHARACTER_DATA` | CharSelection |
| CharSelection | `CreateCharacter` | CreatingCharacter |
| CharSelection | `SelectCharacter` | SelectingCharacter |
| CreatingCharacter | `CHARACTER_CREATION_SUCCESS` (add to `Context.Characters`) | CharSelection |
| CreatingCharacter | `CHARACTER_CREATION_FAILED` (store reason) | CharSelection |
| SelectingCharacter | `MAP_ENTER_REQUEST_ACCEPTED` | ConnectingToMap (flow connects automatically) |
| SelectingCharacter | `MAP_ENTER_REQUEST_REFUSED` (store reason) | CharSelection |
| ConnectingToMap | connected, enter-map request sent | LoadingMap |
| LoadingMap | `AUTHENTICATION_ACCEPTED` (stores start tick, `UninarokPosition` and map view size in `Context.Entry`, a `MapEntry`), then `Flow.MapLoaded()` called by the game when the map is ready, which sends the map-loaded packet (**OQ-1**) | InGame |
| any non-terminal | connect error / timeout / reply timeout / protocol fault | Failed(code) |
| any non-terminal | closure of the active connection | LoggingIn..LoadingMap: Failed(`RemoteClosed`); InGame: Disconnected |
| Failed, Disconnected | `Reset` | Idle |

**Sessions (D6):** `ISession { void Enter(); void Exit(); void Tick(float now); }`. `LoginSession`, `CharSession` and `MapSession` each own a `Subscriptions` instance created in `Enter` and disposed in `Exit`. A session subscribes only to packets valid in the stages it serves (FR-9). `CharSession` owns `PinState` (initialised to `None`, never default-zero = `PinCorrect`) and raises `PinStateChanged`. Connect flow:

```csharp
// inside NetworkFlow; fire-and-forget but never async void
private async Task ConnectAsync(string host, int port, ConnectionRole role, Action<TcpConnection> onConnected)
{
    int gen = _generation;                       // guards against stale completions after Reset
    try
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_timeouts.ConnectSeconds));
        var c = await TcpConnection.ConnectAsync(host, port, role, GeneratedPacketRouter.Instance, cts.Token);
        if (gen != _generation) { c.Dispose(); return; }
        ActiveConnection?.Dispose();             // replace previous server's connection
        ActiveConnection = c;
        onConnected(c);                          // e.g. char: c.SkipNextBytes(4); then Send(enter request)
    }
    catch (OperationCanceledException) when (gen == _generation) { Fail(FailureCode.ConnectTimeout, ...); }
    catch (Exception e)               when (gen == _generation) { Fail(FailureCode.ConnectFailed, e.Message); }
}
```

**Timeouts (FR-10):** `NetworkTimeouts { float ConnectSeconds = 15; float ReplySeconds = 15; float MapLoadSeconds = 60; }`, serialized on `NetworkRuntime`. Entering a waiting state (LoggingIn, ConnectingToChar, SelectingCharacter, CreatingCharacter, ConnectingToMap, LoadingMap) records `deadline = now + ReplySeconds`; `Tick` fails with `ReplyTimeout` when exceeded; leaving the state clears it. In `LoadingMap` the reply deadline applies only until `AUTHENTICATION_ACCEPTED` arrives; afterwards the deadline is `MapLoadSeconds` (the client's own scene load). Time comes from an injected `IClock` so tests use a fake.

**Login refusal (FR-11):** `Failed(LoginRefused)` only. No retry in the flow. Any retry policy belongs to the caller (spec section 6).

### 4.6 Session data — `SessionContext`
Satisfies FR-12.

```csharp
public sealed class SessionContext
{
    // login server
    public uint AccountId; public int AuthCode; public uint UserLevel;
    public uint LastLoginIp; public string LastLoginTime; public byte Sex;
    public ACCEPT_LOGIN.UninarokServerEndpoint[] Servers;      // existing generated type
    // char server
    public readonly List<CharacterInfo> Characters = new();    // existing generated element type
    public uint CurrentCharacterId; public byte SelectedSlot;
    // map server
    public MapEndpoint Map;      // where to connect
    public MapEntry Entry;       // what AUTHENTICATION_ACCEPTED reported
    public void Reset();
}
public readonly struct MapEndpoint { public readonly string Host; public readonly ushort Port;
                                     public readonly string MapName; public readonly string DnsHost; }
public readonly struct MapEntry { public readonly uint StartTick; public readonly UninarokPosition Position;
                                  public readonly byte XSize; public readonly byte YSize; }
```

Types of existing fields (`AuthCode` as `int`, etc.) are preserved because this work does not retype existing packet fields (except the corrections in the packets reference). The generated `MAP_ENTER_REQUEST_ACCEPTED.Port` is `short`; convert once at the boundary with `unchecked((ushort)p.Port)` and build `Host` from the 4 IP bytes. No other class stores a port as `short`.

### 4.7 Map session and heartbeat
Satisfies FR-14.

- `MapSession.Enter` on `ConnectingToMap`: connect, then `Send` `REQUEST_ENTER_MAP` using `Context.*` and `clock.NowMs` as timestamp.
- Character-server keepalive: `CharSession.Tick(now)` sends `CHARACTER_SERVER_PING` (0x0187, 6 bytes, `AccountId` from `Context`) every 12 s while the flow is in a char-server state (FR-14).
- Heartbeat: `MapSession.Tick(now)` sends the map keepalive packet (`HEARTBEAT_REQUEST` 0x0360, 6 bytes; the old local `MAP_HEARTBEAT` used 0x0187, which is the character-server ping; verified in `packets_shuffle_main.h`) when `now >= _nextPingAt` **and** state is `InGame` (OQ-1: after `MAP_LOADED`). Interval is a serialized `pingIntervalSeconds` (default `10`, OQ-2; the scene's current `pingDelay` of 5 is kept via `FormerlySerializedAs`). Send with `conn.Send(in heartbeat)` (`IOutgoingPacket`). Until the generator emits it, use `Span<byte> buf = stackalloc byte[HEARTBEAT_REQUEST.Size]; ping.Serialize(buf); conn.Send(buf);`. No `ArrayPool`, no per-tick allocation.
- `IClock.NowMs`: `unchecked((uint)(Time.realtimeSinceStartupAsDouble * 1000.0))`, a monotonic millisecond clock shared by the heartbeat and the enter-map timestamp.
- `MapSession.Exit` clears `_nextPingAt` and stops sending.

### 4.8 Bootstrap (dev only)
Satisfies FR-15.

`UninarokLoginBootstrap` is wrapped in `#if UNITY_EDITOR || DEVELOPMENT_BUILD`. It subscribes to `NetworkFlow.StateChanged` and `CharSession.PinStateChanged` and acts **once per state entry** (a `_actedForState` guard):

| State | Action |
|---|---|
| (Start) | `Flow.StartLogin(account, password)` once |
| ServerSelection | `SelectServer(serverIndex)` |
| CharSelection | If `Context.Characters` is empty and `PinState` is known and correct: `CreateCharacter(draft from serialized fields)`. Otherwise if characters exist: `SelectCharacter(characterSlot)` |
| LoadingMap | `Flow.MapLoaded()` once (the dev bootstrap has no scene to load) |
| Failed | Log reason. Retry only if `attempt < maxLoginAttempts` (default `1`) |

All serialized fields are used (`characterServerIndex` → `serverIndex`, `characterSlot`, `jobClass`, `hairColor`, `hairStyle`, `sex`, name) or removed (`*HostOverride`, `timeoutSeconds`: removed, replaced by `NetworkTimeouts`). The empty `async void Start()` is removed. The map connection is made by the flow, so the bootstrap no longer calls `ConnectToCurrentMap`.

### 4.9 Allocation policy
Satisfies FR-6.

Every per-packet allocation source found in the current code, and what replaces it:

| Source | Today | After this plan |
|---|---|---|
| `new byte[]` per received packet (serializer) | yes | none: payload is a `ReadOnlySpan<byte>` over the work buffer (§4.1, §4.3) |
| `Debug.Log` with string interpolation per packet received and per packet sent (string, boxed `ushort`, enum `ToString`, stack trace capture) | yes | none: `[Conditional("UNINAROK_NET_TRACE")] NetLog.Trace` removes the call and its arguments |
| `Serialize()` returning a new `Memory<byte>`, and `new byte[Size]` at call sites | yes | none: `Send<T>` serializes into the outbox; `stackalloc` + `Send(ReadOnlySpan<byte>)` as interim |
| `async void` wrappers per send (state-machine objects, notably in non-optimized builds) | yes | removed |
| `Task` per `ReadAsync`/`WriteAsync` (socket thread) | probable; depends on Unity's runtime | none: blocking I/O on dedicated threads (D9) |
| Dispatcher (`Dispatch<T>`) | none: static generic list, indexed loop, `in` delegates | unchanged (a test locks this in) |
| Generated deserializers | **unknown**; likely for arrays and strings (`IpAddress` is indexed like an array, server list and char list are arrays, names are strings) | see packet design rules below |
| Buffer growth | n/a | amortized doubling from 16/32 KB; rare |
| Handlers | `Debug.Log` interpolation, closures | rule below |
| `Subscribe(method group)` | one delegate per call | setup only; accepted |

**Packet design rules for generated structs (hot packets: movement, spawn/vanish, damage, ping; list in spec section 6):**
- No arrays or `string` fields. Use fixed inline fields, or a fixed-capacity UTF-8 text struct decoded lazily (`FixedString*` from `Unity.Collections`; usable capacity is 3 bytes less than the type name, so pick the size from the wire maximum, see the packets reference). `ToString()` is only called by code that needs a managed string.
- Repeated records (spawn lists, inventories) are exposed through a reused buffer or a per-record dispatch, not a freshly allocated array.
- The generator cannot pass `ref struct` through `Dispatch<T> where T : struct` (C# 9). Use the rules above, not span-backed structs.
- Rare packets (login accept, char list, map info) may allocate; this is accepted and documented per packet.

**Handler rules:** no string interpolation, LINQ, closures or `new` collections in handlers for hot packets.

**Measuring (the only way to settle runtime-dependent claims):**
1. EditMode: `Is.Not.AllocatingGCMemory()` on main-thread operations: `ProcessPackages` with a stub router, `Send<T>` for a fixed packet, `Dispatch<T>` with subscribers. This constraint observes the main thread only.
2. Development build on target device, Unity Profiler: GC Alloc per frame on the main thread, in-game idle and during a packet burst. Register socket threads with `Profiler.BeginThreadProfiling("Uninarok.Net", "Read")` (and `"Write"`) so their allocations show in the CPU module. Memory Profiler for heap totals.
3. Baseline before changes (T0.6), same scenario after (T7.6).

### 4.10 Packet definitions and conventions
Satisfies FR-6, FR-18.

The normative per-packet rules live in the packets reference (`Packets/Packet-Implementation.md`). This section records how they map onto the architecture.

- **Registration:** `[InPacket(id, size)]` and `[OutPacket(id, size)]` are generator inputs. They produce the length table (`GetLength`), the router (`RoutePacket`) and, for fixed-size outgoing packets, `IOutgoingPacket.WireSize => Size`. `Size` is the total wire length including the header: `-1` variable, `>= 2` fixed. The generator fails the build on `0`, `1`, values below `-1`, and duplicate IDs per direction. Variable-size outgoing packets declare `WireSize` by hand.
- **`PacketReader` / `PacketWriter`** are `ref struct`s in the engine-free Core assembly (D11):

```csharp
// Uninarok.Networking.Core
public ref struct PacketReader
{
    public PacketReader(ReadOnlySpan<byte> payload);
    public int Remaining { get; }
    public byte ReadByte();
    public ushort ReadUInt16();  public short ReadInt16();
    public uint ReadUInt32();    public int ReadInt32();
    public void ReadBytes(Span<byte> destination);
    public void Skip(int count);            // reserved/unknown regions; throws PacketFormatException on overrun
}
public ref struct PacketWriter
{
    public PacketWriter(Span<byte> destination);
    public int Position { get; }
    public void WriteByte(byte v);  public void WriteUInt16(ushort v);  public void WriteInt16(short v);
    public void WriteUInt32(uint v); public void WriteInt32(int v);
    public void WriteBytes(ReadOnlySpan<byte> v);
    public void Skip(int count);            // zero-filled
}
public sealed class PacketFormatException : Exception { }   // rare path only
```

  All reads and writes are little-endian and bounds-checked. `FixedString*` helpers are extension methods in `Uninarok.Networking` (for example `ReadFixedString32(ref this PacketReader r, int wireBytes)`, one overload per size in use). They stop at the first NUL, never allocate, and fail a test if the wire maximum exceeds the type's capacity. Core therefore has no `Unity.Collections` dependency.
- **Shared data types:** values used by more than one packet (`AccountSex`, `CharInfo`, `UninarokPosition`, `UninarokDirection`) live in `Assets/Scripts/Networking/Data/`, namespace `Uninarok.Networking.Data`, and are documented one file each in `docs/data/`. Each has `Read(ref PacketReader)` / `Write(ref PacketWriter)` and tests. Packets reference them, never nest copies.
- **Reader/writer:** the existing `PacketReader`/`PacketWriter` are evolved (bounds checks, moved to Core), not rewritten from scratch.
- **Packet coverage:** `Packets/INDEX.md` lists every documented packet; `Packets/CATALOG.md` lists every other map-server packet Hercules defines for 20220406 (machine-generated). Promoting a catalog entry to a doc is part of the feature that needs it.
- **Length-only registry (FR-19):** the generator also emits length entries for every ID in the Hercules fixture that has no struct yet. Such IDs are skipped by length and never routed; only IDs absent from the fixture are protocol faults (FR-2).
- **Binary vs text:** byte regions (reserved blocks, packed values such as `UninarokPosition`) are read as bytes. Only fields documented as text use `FixedString*`. `DnsHost` is 128 bytes on the wire (127 characters), so it needs `FixedString512Bytes`, checked by the capacity test.
- **Size verification (FR-18, D12):** a one-off script extracts Hercules' packet length table for PACKETVER 20220406 into `Assets/Tests/EditMode/Networking/Fixtures/hercules-packetlen-20220406.json` (regenerated when the version changes). `PacketSizeTests` reflects over every `[InPacket]`/`[OutPacket]` and compares. A wrong size would not just fail one packet: the receiver consumes exactly `Size` bytes, so every later packet on the connection would be misread.
- **Decode checks:** `PacketDecodeTests` assert that an incoming constructor leaves `Remaining == 0` on a real sample payload, and that an outgoing `Serialize` writes exactly `WireSize` bytes (variable packets: length field equals `WireSize`).
- **Packet documentation and coverage:** each packet doc carries `Status`, `Frequency` (hot/warm/cold) and `Verified` fields. Implementing packets beyond those SPEC-0001 needs (login, char, enter-map, keepalive, map-accepted) is a separate piece of work (a packet-coverage spec) that depends on the contracts in this plan.

## 5. Threading and lifecycle summary

| Thread | May do | Must not |
|---|---|---|
| Socket reader and writer threads (dedicated) | blocking `Receive`/`Send`, `AppendData`, outbox swap, `TryClose`, `Debug.Log` | any other `UnityEngine` API, call handlers, touch `NetworkFlow` |
| Main thread | `ProcessPackages`, dispatch, `Send`, `SkipNextBytes`, flow commands, `Tick` | block on I/O |

- `await`s that resume game code use the Unity synchronization context (no `ConfigureAwait(false)` at call sites).
- `NetworkRuntime` is created once, `DontDestroyOnLoad`; `OnDestroy` disposes the active connection, cancels flows, and bumps `_generation`.
- No `async void` in the network layer (the only fire-and-forget is `_ = ConnectAsync(...)` with internal try/catch).

## 6. Test plan
Satisfies FR-17 and verifies FR-1 to FR-14.

| Suite (`Assets/Tests/EditMode/Networking/`) | Covers |
|---|---|
| `SerializerFramingTests` | FR-1: fixed, variable, split at every byte offset, coalesced, back-to-back |
| `SerializerFaultTests` | FR-2: unknown ID, ID 0, length < 4, bad table value, cap exceeded; one fault, no more routing after |
| `SerializerSkipTests` | FR-3: skip in 1-byte chunks, skip of zeros, skip before any packet |
| `SerializerLimitsTests` | FR-5: 1000 packets with cap 256 take 4 calls; router that throws does not corrupt next call |
| `SerializerAllocTests` | FR-6: `Is.Not.AllocatingGCMemory()` over 1000 fixed packets (stub router) |
| `DispatchAllocTests` | FR-6: `Dispatch<T>` with 3 subscribers allocates 0 bytes |
| `PacketReaderWriterTests` | FR-6, FR-18: little-endian primitives, overrun throws `PacketFormatException`, `Skip`, `Remaining`, zero allocation |
| `PacketSizeTests` | FR-18: every registered size equals the Hercules table fixture; invalid sizes and duplicate IDs rejected |
| `PacketDecodeTests` | FR-18: incoming decode consumes the whole sample payload; outgoing `Serialize` writes exactly `WireSize` |
| `ConcurrencyTests` | FR-5: `AppendData` is not blocked while router runs 100 ms |
| `TcpConnectionTests` (loopback `TcpListener`) | FR-7, FR-8: remote close observed < 1 s; dispose is silent and unblocks both threads; empty send is a no-op; send after close returns false; drain-before-close |
| `OutgoingTests` (loopback) | FR-6, FR-8: 10 000 sends arrive complete and in order; 4 concurrent sender threads produce no partial or interleaved packets (per-packet checksum); `Send<T>` of a fixed packet allocates 0 bytes after warm-up; a throwing `Serialize` leaves no partial bytes and the next send works; backlog overflow closes with `Error` and returns false; golden-byte test per outgoing packet (new path equals old `Serialize()` output) |
| `DispatcherTests` | FR-4, FR-9: throwing handler does not block the next; enable/disable ×100 leaves count unchanged |
| `FlowTransitionTests` (fake connection, fake clock) | FR-10, FR-11, FR-13: table-driven valid/invalid transitions, timeouts, refusal, creation success/failure, PIN initial `None` |
| `MapSessionTests` | FR-14: heartbeat bytes, ms timestamps increasing, none before ready or after exit |
| `CaptureReplayTests` | FR-1: for each `Captures/*.bin`, replay with chunk sizes {1,2,3,7,64,whole} and compare to `*.expected.txt` (`skip=N` header, then `id,payloadLen,crc32` per packet) |
| `PumpOwnershipTests` | FR-5: source scan shows `ProcessPackages` is called only from `NetworkRuntime` and tests |

`Is.Not.AllocatingGCMemory()` observes the main thread only; socket-thread allocations are checked with the Profiler (§4.9). Golden files are generated once from the whole-buffer run, reviewed by hand, then committed. Manual smoke test against local Hercules is in `tasks.md` phase 7.

CLI (adjust path/version): `Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults results.xml -quit`.

## 7. Adopting this in the existing project

**Scene dependencies (spec section 6).** `Assets/Scenes/Bootstrap.unity` serializes `Uninarok.Login.LoginManager` (`loginHost`, `loginPort`), `Uninarok.Managers.GameConnectionManager` (`pingDelay`) and `Uninarok.Networking.RagnarokLoginBootstrap`. Rules: keep the `.cs` files and their `.meta` GUIDs (do not delete and recreate), keep namespaces or add `[MovedFrom]`, and rename serialized fields only with `[FormerlySerializedAs]`. The scene stores dev account credentials in plain text; remove them when the bootstrap becomes editor-only (FR-15).

| Old | New |
|---|---|
| `LoginManager.LoginState.None / LoggingIn` | `Idle / LoggingIn` |
| `OnServerSelection` | `ServerSelection` |
| `LoggingToCharacterServer` | `ConnectingToChar` |
| `OnEmptyCharacterSelection`, `OnCharacterSelection` | `CharSelection` (+ `Context.Characters.Count`) |
| `CreatingCharacter` | `CreatingCharacter` |
| `LoggingToZone` | `SelectingCharacter` |
| `LoginState.Playing`, `GameState.LoadingMap` | `ConnectingToMap` → `LoadingMap` |
| `GameState.Playing` | `InGame` |
| `LoginManager.Instance.AccountId` etc. | `NetworkRuntime.Instance.Flow.Context.AccountId` etc. |

The only known consumer outside the main scripts is `Bootstrap.unity` (spec section 6). If T0.2 finds more, keep a thin `[Obsolete]` `LoginManager` facade over `NetworkFlow` for one release, then delete.

## 8. Decisions

| # | Decision | Alternatives rejected |
|---|---|---|
| D1 | Inbox + work double buffer; lock only for the swap | Single buffer with one lock (blocks socket during dispatch, current bug); lock-free ring (complexity not justified) |
| D2 | Per-frame cap of 256 packets | Unlimited (frame spikes) |
| D3 | Fault closes the connection; no resync | Skip-ahead heuristics (unsafe without length knowledge) |
| D4 | Core in an engine-free asmdef | Keep in main asmdef (needs Unity for tests) |
| D5 | Keep static generic struct dispatcher | Interface/`object` bus (boxing, base type) |
| D6 | One `NetworkRuntime` + plain-C# sessions + closure as immutable `CloseInfo` | One MonoBehaviour per manager (undefined order); events from socket thread (thread-safety) |
| D7 | Synchronous `Send<T>(in packet)` that serializes straight into a double-buffered outbox; no `SendAsync`; failure reported by `bool` and by closure | Keep `ValueTask` (it never suspended and hid that "sent" means "queued"); exceptions for backlog overflow |
| D8 | No `async void`; generation counter for stale connects | try/catch around every `async void` (still not awaitable/observable) |
| D9 | Dedicated reader and writer threads with blocking `Socket.Receive`/`Send` and an `AutoResetEvent` wake-up, for zero steady-state allocation | `NetworkStream.ReadAsync/WriteAsync` loops and `SemaphoreSlim.WaitAsync` (a `Task` per I/O on at least some runtimes); `SocketAsyncEventArgs` (zero-alloc, more code; the fallback if blocking threads misbehave on a platform) |
| D10 | Generator emits `IOutgoingPacket` (`WireSize` + `Serialize(Span<byte>)`), used as a constrained generic | Returning `byte[]`/`Memory<byte>` (allocates per packet); interface-typed parameters (boxing); `stackalloc` at every call site (kept as interim only) |
| D11 | `PacketReader`/`PacketWriter` as `ref struct`s in Core; `FixedString*` helpers outside Core | Per-packet `BitConverter` code (duplicated, error-prone); making Core depend on Unity.Collections (loses engine-free tests) |
| D12 | Declared sizes verified by a test against a fixture extracted from Hercules | Trusting hand-typed sizes |
| D13 | Shared packet data in `Networking/Data` with one doc each in `docs/data` | Nested types inside the first packet that used them (cross-packet coupling) |

## 9. Risks

| Risk | Mitigation |
|---|---|
| Generator change for `ReadOnlySpan<byte>` is large | Fallback: adapter copies to `byte[]` (one allocation per packet). That is a deferral of FR-6 and needs human approval |
| Char server echo is not exactly 4 bytes for this PACKETVER | Verify with real captures in T7.1 before relying on `SkipNextBytes(4)` |
| `LoginManager` API used outside the main scripts | T0.2 greps first; facade per section 7 |
| Generator emits `0` for variable-length | T0.3 checks; fix generator |
| Drain-before-close hides closure under constant packet flood | Cap per frame is finite, so closure is delivered within a few frames |
| A blocked `Receive` is not woken by close on some platform (Linux, Android, iOS) | `Shutdown(Both)` before close; verify on each target platform (T7.7); fallback `SocketAsyncEventArgs` (D9) |
| Generator cannot emit `IOutgoingPacket`, or outgoing packet shapes are irregular | Interim `stackalloc` + `Send(ReadOnlySpan<byte>)` per packet (still zero GC for fixed sizes); shapes surveyed in T0.5 |
| Claims about per-I/O `Task` allocations depend on Unity's runtime and version and are not verified | T0.6 baseline decides whether D9 pays off; D9 is contained inside `TcpConnection` |
| Generated deserializers allocate (arrays, strings) | Per-packet policy in §4.9; hot packets redesigned one by one; rare packets accepted |
| A wrong declared size, or an unregistered refusal/keepalive packet, desynchronizes or faults the connection | FR-18 size verification; reconcile the "awaiting clarification" packets (T0.7) before T4.6 |
| `FixedString*` is UTF-8 but the server may send a legacy codepage | Decided in spec section 6: Windows-1252 default, one constant in the text helpers (T1.18) |

## 10. Traceability

| FR | Plan sections |
|---|---|
| FR-1, FR-2, FR-3 | 4.1, 4.3 |
| FR-6 | 4.1, 4.2, 4.3, 4.9 |
| FR-4 | 4.3 |
| FR-5 | 4.1, 4.4, 5 |
| FR-7, FR-8 | 4.2, 4.4 |
| FR-9 | 4.3, 4.5 |
| FR-10, FR-11, FR-13 | 4.5 |
| FR-12 | 4.6 |
| FR-14 | 4.7 |
| FR-15 | 4.8 |
| FR-16 | 4.3 |
| FR-17 | 3, 6 |
| FR-18 | 4.10, 6 |
