# PLAN-0001: Networking Core

| | |
|---|---|
| **Implements** | [spec.md](spec.md) (SPEC-0001) |
| **Status** | Approved |
| **Audience** | Architects, senior developers, AI agents |

> This file defines **how**. Requirement IDs (`FR-n`) refer to `spec.md`. Packet IDs, sizes and field layouts come from the packets reference ([`Packets/`](Packets/Packet-Implementation.md)), which is normative for its topic.

## 1. Constraints and assumptions

- Unity 6 (6000.x), C# 9 language features available in Unity (target-typed `new()`, `in` parameters, `readonly struct`), .NET Standard 2.1 profile.
- **Threading rule:** socket threads never call game code. Framing, dispatch, flow, managers and all `UnityEngine` API run on the main thread.
- Hercules framing: little-endian `ushort` packet ID, then either a fixed total length or (variable) a `ushort` total length that includes the 4-byte header.
- Length contract: `-1` variable, `-2` unknown, `>= 2` fixed total length including the header. `0`, `1` and values below `-1` never appear in a table (the table generator rejects them).
- The payload handed to an incoming packet constructor **excludes** the ID and length fields.
- Dependencies: Unity Test Framework (NUnit) and `com.unity.collections` (`FixedString*`). Both are first-party Unity packages already resolved in the project; they become direct dependencies in `Packages/manifest.json`. `System.Buffers.Binary` from the BCL. No third-party libraries. Build-time tooling outside Unity (not shipped): the generator project uses `Microsoft.CodeAnalysis.CSharp` 4.5.0 (the editor compiles with Roslyn 5.6, which binds a 4.x reference), and its test project (`Tools/Uninarok.Networking.Generator.Tests`) uses NUnit, `NUnit3TestAdapter` and `Microsoft.NET.Test.Sdk` (D18).
- **GC policy (FR-6):** the main thread allocates nothing per packet in steady state, in either direction. Unity's GC is global and stop-the-world (incremental mode softens it), so socket-thread allocations also matter. See §4.10.

## 2. Architecture

Satisfies FR-5, FR-20.

```
 socket threads                     main thread (Unity)
┌───────────────┐ bytes  ┌──────────────────────────────────────────────────────────┐
│ TcpConnection │───────▶│ NetworkClient : MonoBehaviour, INetworkClient            │
│  reader thread│ Append │  Update():                                               │
│  writer thread│◀───────│   1. complete pending connect → Connected / Disconnected │
└───────────────┘ outbox │   2. framer.Process(max) ──▶ GeneratedPacketRouter       │
                         │                               └▶ PacketDispatcher         │
                         │   3. closed and drained → Disconnected(role, CloseInfo)   │
                         │   4. Flow.Tick()                                          │
                         └──────────────▲──────────────────────────▲───────────────┘
                                        │ INetworkClient           │ creates, ticks
                                        │ (Connect, Send, Subscribe, events)
              ┌─────────────────────────┴───────┐         ┌───────┴────────┐
              │ LoginManager  CharacterManager  │◀────────│  NetworkFlow   │
              │ ZoneManager    (plain C#)       │ commands│ state machine, │
              │ protocol of one server each     │ events  │ timeouts,      │
              └─────────────────────────────────┘────────▶│ SessionContext │
                                                          └───────▲────────┘
                                         game / UI / DevAutoLogin │ commands, StateChanged
```

- **`NetworkClient`** is the only `MonoBehaviour` and the only type that owns a `TcpConnection`. It is pure transport plus scheduling: connect, pump incoming packets once per frame, send, report closure. It also acts as composition root: it builds the dispatcher, the router and the `NetworkFlow`, and ticks the flow after the pump so replies received this frame are seen before deadlines are checked.
- **Managers** (`LoginManager`, `CharacterManager`, `ZoneManager`) are plain C# classes, one per server. Each knows only its server's protocol: which packets to send, which to handle, what to write into `SessionContext`. They depend on `INetworkClient`, never on `TcpConnection`, and report outcomes to the flow through events. They do not change state or open connections.
- **`NetworkFlow`** is the orchestrator: it owns the state machine, all deadlines, the hand-off between servers (which connection to open, which manager is active) and the `SessionContext`. The game talks only to the flow.
- One connection is active at a time. Opening the next server's connection replaces the previous one.

## 3. Assemblies and file layout

Satisfies FR-17, FR-20.

Root: `Uninarok-client/Assets/Scripts/Networking/`.

| Path | Contents | Assembly |
|---|---|---|
| `Core/Wire/` | `PacketReader`, `PacketWriter`, `PacketFormatException` | `Uninarok.Networking.Core` (`noEngineReferences: true`, `allowUnsafeCode: false`) |
| `Core/Framing/` | `IPacketLengthTable`, `IPacketRouter`, `IInboundSink`, `PacketFramer` | Core |
| `Core/Dispatch/` | `PacketHandler<T>`, `PacketDispatcher` | Core |
| `Core/Transport/` | `TcpConnection`, `ConnectionRole`, `CloseReason`, `CloseInfo`, `IOutgoingPacket` | Core |
| `Core/Protocol/` | `InPacketAttribute`, `OutPacketAttribute`, `HerculesPacketTable.g.cs` (generated, §4.8) | Core |
| `Runtime/Text/` | `WireText` (`FixedString*` read/write, Windows-1252 table) | `Uninarok.Networking` (references Core, `Unity.Collections`) |
| `Runtime/Data/` | `AccountSex`, `CharInfo`, `UninarokPosition`, `UninarokDirection`, `UninarokMoveData` (one per `docs/data/*.md`; `UninarokDirection` is described in `UninarokPosition.md`) | `Uninarok.Networking` |
| `Runtime/Packets/` | `PacketIds`, `ProtocolInfo` (`PacketVersion`, default client type), `In/{Login,Character,Map}/*.cs`, `Out/{Login,Character,Map}/*.cs`, one struct per file | `Uninarok.Networking` |
| `Runtime/Packets/Generator/` | `Uninarok.Networking.Generator.dll` (Roslyn source generator, asset label `RoslynAnalyzer`, excluded from all platforms) | applies to `Uninarok.Networking` and assemblies that reference it |
| `Runtime/Client/` | `INetworkClient`, `NetworkClient`, `ConnectionStatus`, `Subscriptions`, `NetLog`, `IClock`, `UnityClock`, `NetworkSettings` | `Uninarok.Networking` |
| `Runtime/Flow/` | `NetworkFlow`, `FlowState`, `FailureCode`, `FlowFailure`, `SessionContext`, `ServerEndpoint`, `CharacterSlots`, `MapEndpoint`, `MapEntry`, `CharacterDraft` | `Uninarok.Networking` |
| `Runtime/Managers/` | `IServerManager`, `LoginManager`, `CharacterManager`, `ZoneManager`, `LoginRefusal`, `PinState` | `Uninarok.Networking` |
| `Runtime/Dev/` | `DevAutoLogin` (whole file inside `#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD`) | `Uninarok.Networking` |
| `Editor/` | `HerculesPacketTableGenerator` (menu item, §4.8) | `Uninarok.Networking.Editor` (Editor only) |
| `Uninarok-client/Assets/Tests/EditMode/Networking/` | tests, `Fixtures/`, `Captures/*.bin` + `*.expected.txt` | `Uninarok.Networking.Tests` (EditMode; references Core and `Uninarok.Networking`) |
| `Tools/Uninarok.Networking.Generator/` (repo root) | Roslyn generator source project (`netstandard2.0`, `Microsoft.CodeAnalysis.CSharp` version supported by the Unity editor in use); build output copied to `Runtime/Packets/Generator/` | outside Unity |
| `Tools/Uninarok.Networking.Generator.Tests/` (repo root) | NUnit project (`net10.0`) that compiles `Core` from source and runs the generator in memory (`CSharpGeneratorDriver`) over test compilations; run with `dotnet test` | outside Unity |

`Uninarok.Networking` declares `InternalsVisibleTo("Uninarok.Networking.Tests")`. Namespaces follow folders: `Uninarok.Networking.Core.*`, `Uninarok.Networking.Text`, `Uninarok.Networking.Data`, `Uninarok.Networking.Packets.*`, `Uninarok.Networking.Client`, `Uninarok.Networking.Flow`, `Uninarok.Networking.Managers`.

## 4. Component contracts

### 4.1 Wire primitives — `PacketReader`, `PacketWriter` (Core)
Satisfies FR-6, FR-18.

```csharp
namespace Uninarok.Networking.Core.Wire
{
    public ref struct PacketReader
    {
        public PacketReader(ReadOnlySpan<byte> payload);
        public int Remaining { get; }
        public byte ReadByte();
        public ushort ReadUInt16();  public short ReadInt16();
        public uint ReadUInt32();    public int ReadInt32();
        public void ReadBytes(Span<byte> destination);
        public ReadOnlySpan<byte> ReadSpan(int count);   // view, valid only while the payload is
        public void Skip(int count);
        public void EnsureConsumed();                    // throws PacketFormatException if bytes are left: incoming packet constructors call it last
    }
    public ref struct PacketWriter
    {
        public PacketWriter(Span<byte> destination);
        public int Position { get; }
        public void WriteByte(byte v);   public void WriteUInt16(ushort v); public void WriteInt16(short v);
        public void WriteUInt32(uint v); public void WriteInt32(int v);
        public void WriteBytes(ReadOnlySpan<byte> v);
        public void Skip(int count);                      // writes zeros
    }
    public sealed class PacketFormatException : Exception
    {
        public PacketFormatException(string message);
    }
}
```

All reads and writes are little-endian (`BinaryPrimitives`) and bounds-checked. Overrun throws `PacketFormatException`; that path allocates, which is acceptable because it ends the packet. The happy path allocates nothing.

### 4.2 Wire text — `WireText` (Runtime)
Satisfies FR-6.

```csharp
namespace Uninarok.Networking.Text
{
    public static class WireText
    {
        public static void Read(ref PacketReader r, int wireBytes, ref FixedString32Bytes value);   // one overload per FixedString size in use
        public static void Write(ref PacketWriter w, int wireBytes, in FixedString32Bytes value);
        // ... FixedString64Bytes, FixedString128Bytes, FixedString512Bytes
    }
}
```

- Reading consumes exactly `wireBytes`, stops the text at the first NUL, and converts Windows-1252 to UTF-8 with a hand-written 256-entry table (0x00–0x7F identity, 0x80–0x9F per the Windows-1252 chart, 0xA0–0xFF to U+00A0–U+00FF). Writing does the reverse; characters with no mapping become `?`; text longer than `wireBytes - 1` is truncated, the rest is zero-filled.
- The encoding is the single constant `WireText.Encoding` (spec §6). Changing it means swapping the table, nowhere else.
- A static test asserts, for every packet field using `WireText`, that the type's capacity (type name minus 3 bytes) holds the worst-case UTF-8 expansion of `wireBytes - 1` characters, or the field documents that truncation is accepted.

### 4.3 Packet framing — `PacketFramer` (Core)
Satisfies FR-1, FR-2, FR-3, FR-5, FR-6, FR-17, FR-19.

```csharp
namespace Uninarok.Networking.Core.Framing
{
    public interface IPacketLengthTable
    {
        int GetLength(ushort packetId);                             // -1 variable, -2 unknown, >=2 fixed
    }
    public interface IPacketRouter
    {
        bool Route(ushort packetId, ReadOnlySpan<byte> payload);    // false = not handled (skipped by length). Span valid only during the call
    }
    public interface IInboundSink
    {
        bool Append(ReadOnlySpan<byte> data);                       // ANY thread. false = buffer cap exceeded
    }

    public sealed class PacketFramer : IInboundSink
    {
        public const int DefaultMaxBufferedBytes = 4 * 1024 * 1024;
        public PacketFramer(IPacketLengthTable lengths, IPacketRouter router, int maxBufferedBytes = DefaultMaxBufferedBytes);

        public bool   IsFaulted   { get; }                          // main thread
        public string FaultReason { get; }                          // reason + up to 32 next bytes as hex

        public bool Append(ReadOnlySpan<byte> data);                // ANY thread
        public void SkipNextBytes(int count);                       // MAIN. Call BEFORE sending the request that provokes the bytes
        public int  Process(int maxPackets);                        // MAIN. Returns packets framed (routed or skipped). 0 once faulted or stopped
        public void Stop();                                         // MAIN. Process returns after the current packet; later calls return 0
    }
}
```

**Double buffer (D1).** The socket thread appends to a locked `_inbox`. `Process` takes the lock only to move the inbox into a main-thread-only `_work` buffer (compacting first), then parses `_work` with **no lock held**, so dispatch never blocks the socket thread. Both buffers start at 32 KB and grow by doubling up to `maxBufferedBytes`; `Append` returns `false` above the cap.

**Algorithm (per `Process` call):**
1. If faulted or stopped, return 0. Drain the inbox into `_work`.
2. Loop while `processed < maxPackets` and not stopped:
   1. If `_skip > 0`: consume `min(_skip, available)`; if `_skip` is still > 0, break; else continue. *(Runs before any header parsing: FR-3.)*
   2. If `available < 2`, break. Read `id` (LE). If `id == 0`, fault `"Packet ID 0"`.
   3. `len = lengths.GetLength(id)`. If `-2`, fault `"Unknown packet 0x{id:X4}"`. If `len` is not `-1` and `len < 2`, fault `"Invalid table length"`.
   4. If `-1`: if `available < 4`, break; `len = u16 @ +2`; if `len < 4`, fault `"Bad length"`; header is 4 bytes. Otherwise header is 2 bytes.
   5. If `available < len`, break (wait for more data).
   6. `payload = work[read + header .. read + len]`. **Advance `_read += len` first**, then `router.Route(id, payload)`. A `false` return means the ID is in the table but has no handler: it is skipped (FR-19) and traced under `UNINAROK_NET_TRACE` only. `processed++`.
3. If `_read == _write`, reset both to 0.

A fault stores `FaultReason` and stops parsing. **No resync (D3):** without trustworthy lengths a TCP stream cannot be re-aligned safely, so the owner closes the connection.

**Rules:** the framer does no logging (Core has no engine reference). `Route` must not call `Process`. If `Route` throws, the framer's state is already consistent; the exception propagates to the caller (`NetworkClient` catches it, §4.7). `Stop` exists so that a handler that replaces the connection mid-pump (for example the flow leaving the login server) prevents later packets of the old connection from being dispatched.

### 4.4 Transport — `TcpConnection` (Core)
Satisfies FR-2, FR-6, FR-7, FR-8.

```csharp
namespace Uninarok.Networking.Core.Transport
{
    public enum ConnectionRole { Login, Character, Map }
    public enum CloseReason    { LocalDispose, RemoteClosed, Error, ProtocolFault, ConnectFailed }
    public sealed class CloseInfo                                    // immutable
    {
        public CloseInfo(CloseReason reason, string message);
        public CloseReason Reason { get; }
        public string Message { get; }
    }

    // Implemented by every outgoing packet (a readonly struct). Only ever used as a constrained generic: never boxed.
    public interface IOutgoingPacket
    {
        int  WireSize { get; }                       // exact total bytes: ID + length field (if variable) + payload
        void Serialize(Span<byte> destination);      // writes exactly WireSize bytes; must not allocate
    }

    public sealed class TcpConnection : IDisposable
    {
        public static Task<TcpConnection> ConnectAsync(string host, ushort port, ConnectionRole role,
                                                       IInboundSink sink, CancellationToken token);  // one-shot; may allocate
        public ConnectionRole Role { get; }
        public bool      IsClosed { get; }           // Closure != null
        public CloseInfo Closure  { get; }           // null until closed; first close wins

        // Any thread (main in practice). Never blocks on I/O. Never throws because of connection state.
        // true = queued (NOT "sent"). false = not queued (closed, or backlog limit hit → connection closed).
        public bool Send<T>(in T packet) where T : struct, IOutgoingPacket;
        public bool Send(ReadOnlySpan<byte> rawPacket);

        public void Close(CloseReason reason, string message);   // any thread; idempotent
        public void Dispose();                                    // Close(LocalDispose, null) + release; idempotent
    }
}
```

**Connect.** `ConnectAsync` resolves the host, connects a `TcpClient` with `NoDelay = true`, honours the token (on cancellation it disposes the socket and throws `OperationCanceledException`), then starts the two threads below. It uses `ConfigureAwait(false)` throughout; it never touches Unity APIs.

**Receive path.** Dedicated thread `Uninarok.Net.{role}.Read`:

```csharp
private void ReadLoop()
{
    var buf = new byte[32 * 1024];                            // allocated once per connection
    try
    {
        while (true)
        {
            int n = _socket.Receive(buf, 0, buf.Length, SocketFlags.None);
            if (n == 0) { Close(CloseReason.RemoteClosed, "Server closed the connection"); return; }
            if (!_sink.Append(new ReadOnlySpan<byte>(buf, 0, n))) { Close(CloseReason.ProtocolFault, "Receive buffer overflow"); return; }
        }
    }
    catch (Exception) when (_closure != null) { }             // our own shutdown
    catch (Exception e) { Close(CloseReason.Error, e.Message); }
}
```

**Send path (D7, D9).** Mirror image of the receive path: an outbox appended under a short lock and a writer-owned buffer, swapped under the lock, written with no lock held.

```csharp
private readonly object _outLock = new();
private byte[] _outbox   = new byte[16 * 1024];
private byte[] _writeBuf = new byte[16 * 1024];
private int  _outLength;
private bool _writerIdle = true;                              // guarded by _outLock
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
            packet.Serialize(new Span<byte>(_outbox, _outLength, size));   // if this throws, _outLength is not advanced
            _outLength += size;
            if (_writerIdle) { _writerIdle = false; _outSignal.Set(); }
        }
    }
    if (overflow) { Close(CloseReason.Error, "Send backlog exceeded"); return false; }
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
                    (_outbox, _writeBuf) = (_writeBuf, _outbox);
                    n = _outLength; _outLength = 0;
                }
                for (int sent = 0; sent < n; )
                    sent += _socket.Send(_writeBuf, sent, n - sent, SocketFlags.None);
            }
        }
    }
    catch (Exception) when (_closure != null) { }
    catch (Exception e) { Close(CloseReason.Error, e.Message); }
}
```

- **Semantics of `Send`:** `true` means queued. A packet is committed whole or not at all (counted only after `Serialize` succeeds), so there are no partial packets, and order equals call order, across threads too. A throwing `Serialize` is a programmer error: it propagates and leaves the outbox intact.
- **Backlog limit:** client traffic is tiny. More than 1 MiB unsent means the server stopped reading, so the connection closes with `Error` instead of growing without bound.
- **Idle flag:** the signal is set only on the idle → busy transition, under the buffer's lock: no lost wake-up, no redundant signals.
- **Closure (D6):** `_closure` is set once with `Interlocked.CompareExchange(ref _closure, info, null)`. Every close path (0-byte read, I/O error, sink overflow, backlog overflow, `Close`, `Dispose`) goes through `Close`, which also calls `_outSignal.Set()` to wake the writer.
- **Shutdown-quiet (FR-7):** exceptions raised while `_closure != null` are swallowed by the filters above; any other exception becomes `Close(Error, message)`.
- **Waking blocked threads (D17):** `Close` calls `socket.Shutdown(SocketShutdown.Both)` and then closes the socket (`TcpClient.Dispose`), both inside try/catch. Measured on Unity 6000.7.0b7 (Mono, Windows editor): `Shutdown` alone does **not** wake a thread blocked in `Receive`; closing the handle does, and the reader sees a `SocketException`. That exception is swallowed by the shutdown-quiet filter because `_closure` is already set before the socket is closed. A thread that touches the closed socket afterwards gets `ObjectDisposedException` or `SocketException`, handled by the same filter. Only `_outSignal` (the `AutoResetEvent`) is still disposed by the last thread to exit (`Interlocked.Decrement` on a live-thread counter), so the writer never waits on a disposed handle.
- **Allocation:** `Send<T>` is generic over a struct, so there is no boxing. `AutoResetEvent.Set()`, `Socket.Send` and `Socket.Receive` do not allocate. A non-`readonly` struct passed by `in` may be copied defensively on the stack (not GC).
- **Trace capture (optional, `UNINAROK_NET_TRACE`):** the `NetworkClient` wraps the sink with a tee that appends received bytes to `Application.persistentDataPath/captures/{role}-{yyyyMMdd-HHmmss}.bin` for replay fixtures (Core stays engine-free; the tee lives in Runtime).

### 4.5 Dispatch — `PacketDispatcher` (Core)
Satisfies FR-4, FR-6, FR-9.

```csharp
namespace Uninarok.Networking.Core.Dispatch
{
    public delegate void PacketHandler<T>(in T packet) where T : struct;

    public sealed class PacketDispatcher
    {
        public PacketDispatcher(Action<Exception> onHandlerError);
        public void Subscribe<T>(PacketHandler<T> handler) where T : struct;     // setup time; ignores a duplicate
        public void Unsubscribe<T>(PacketHandler<T> handler) where T : struct;   // setup time; ignores a missing handler
        public void Dispatch<T>(in T packet) where T : struct;                   // main thread; no allocation
        public int  HandlerCount<T>() where T : struct;                          // tests
    }
}
```

- **Instance, not static (D5).** Handler lists are stored per instance in an `object[]` indexed by a per-type slot (`static class Slot<T> { internal static readonly int Index = Interlocked.Increment(ref s_next) - 1; }`). Lookup is an array index plus an `is PacketHandler<T>[]` type test: no boxing, no dictionary, no allocation.
- **Copy-on-write lists.** `Subscribe`/`Unsubscribe` replace the `PacketHandler<T>[]` with a new array (setup-time allocation). `Dispatch` iterates the array it read at the start, so a handler may subscribe or unsubscribe anything during dispatch without skipping or repeating a callback; the change applies from the next packet.
- **Isolation (FR-4).** Each callback runs in its own `try/catch`; an exception goes to `onHandlerError` and the next callback runs.

### 4.6 Packet definitions, registration and the generated router (Runtime)
Satisfies FR-4, FR-6, FR-18, FR-19.

The per-packet rules (struct shape, text fields, arrays, verification, documentation) are normative in [`Packets/Packet-Implementation.md`](Packets/Packet-Implementation.md). This section defines how they plug into the architecture.

- **Shape.** One `readonly struct` per packet and file, named as its doc (for example `ACCEPT_LOGIN`), namespace `Uninarok.Networking.Packets.In.Login` / `.Character` / `.Map` and `...Out.*`. IDs are `const ushort` fields of `PacketIds`. Each struct has `public const int Size`.
- **Incoming:** `[InPacket(PacketIds.X, Size)]` and a constructor `X(ReadOnlySpan<byte> payload)` that reads with `PacketReader` and leaves `Remaining == 0`.
- **Outgoing:** `[OutPacket(PacketIds.X, Size)]` and `IOutgoingPacket`. Fixed packets implement `WireSize => Size`; variable packets compute it without allocating.
- **Shared data types** used by several packets live in `Runtime/Data/` and are documented one file each in `docs/data/`. Each has `Read(ref PacketReader)` / `Write(ref PacketWriter)` and tests. Packets reference them, never nest copies.
- **Packets in scope for this spec** (everything the flow and keepalives need): login `REQUEST_LOGIN`, `ACCEPT_LOGIN`, `LOGIN_REFUSAL`, `REFUSE_LOGIN_3`, `LOGIN_SERVER_NOTICE`; character `CHARACTER_SERVER_REQUEST_ENTRY`, `CHARACTER_SLOTS_INFO`, `CHARACTER_DATA`, `PINCODE_LOGINSTATE`, `CREATE_CHARACTER`, `CHARACTER_CREATION_SUCCESS`, `CHARACTER_CREATION_FAILED`, `SELECT_CHARACTER`, `MAP_ENTER_REQUEST_ACCEPTED`, `MAP_ENTER_REQUEST_REFUSED`, `CHARACTER_SERVER_PING`; map `REQUEST_ENTER_MAP`, `MAP_ENTRY_ACCEPTANCE`, `MAP_ENTRY_REFUSAL`, `AUTHENTICATION_ACCEPTED`, `MAP_LOADED`, `HEARTBEAT_REQUEST`, `SERVER_HEARTBEAT_RESPONSE`. Other documented packets are implemented by the features that need them.

**Source generator (D10).** `Uninarok.Networking.Generator` (Roslyn `IIncrementalGenerator`) scans the compilation for `[InPacket]` structs and emits:

```csharp
// <auto-generated/>  Uninarok.Networking.Packets.GeneratedPacketRouter
public sealed class GeneratedPacketRouter : IPacketRouter
{
    public GeneratedPacketRouter(PacketDispatcher dispatcher, Action<ushort, PacketFormatException> onDecodeError);
    public static ReadOnlySpan<ushort> HandledIds { get; }        // sorted; backed by a static array
    public bool Route(ushort id, ReadOnlySpan<byte> payload)
    {
        switch (id)
        {
            case PacketIds.ACCEPT_LOGIN:
                try { var p = new ACCEPT_LOGIN(payload); _dispatcher.Dispatch(in p); }
                catch (PacketFormatException e) { _onDecodeError(id, e); }
                return true;
            // ... one case per [InPacket]
            default: return false;
        }
    }
}
```

- A decode failure (`PacketFormatException`) is reported once and the stream continues: the framer has already advanced past the packet, so alignment is not affected (FR-4). Handler exceptions are isolated by the dispatcher.
- Compile-time diagnostics (errors): duplicate ID per direction; declared size `0`, `1` or below `-1`; `[InPacket]` struct without a `(ReadOnlySpan<byte>)` constructor; `[OutPacket]` struct that does not implement `IOutgoingPacket`; packet type that is not a `readonly struct`.
- The generator emits nothing when the compilation has no `[InPacket]` type (it runs on every assembly that references `Uninarok.Networking`, including tests).
- Lengths are **not** generated from the structs: the framer reads them from the Hercules table (§4.8), and a test proves the struct sizes agree with it.

### 4.7 Connection point — `INetworkClient` / `NetworkClient` (Runtime)
Satisfies FR-5, FR-7, FR-8, FR-16, FR-20.

```csharp
namespace Uninarok.Networking.Client
{
    public enum ConnectionStatus { Idle, Connecting, Connected }

    public interface INetworkClient
    {
        ConnectionStatus Status { get; }
        ConnectionRole   Role   { get; }              // meaningful unless Idle

        // Drops any current or pending connection silently, then starts connecting.
        void Connect(string host, ushort port, ConnectionRole role);
        // Drops the current or pending connection. Raises no event and logs nothing (intentional, FR-7).
        void Disconnect();

        bool Send<T>(in T packet) where T : struct, IOutgoingPacket;   // false unless Connected and queued
        void SkipNextBytes(int count);                                  // Connecting/Connected only; applies to the next bytes received

        void Subscribe<T>(PacketHandler<T> handler) where T : struct;
        void Unsubscribe<T>(PacketHandler<T> handler) where T : struct;

        event Action<ConnectionRole> Connected;                         // main thread
        event Action<ConnectionRole, CloseInfo> Disconnected;           // main thread, after drain; connect failures included
    }

    [DefaultExecutionOrder(-100)]
    public sealed class NetworkClient : MonoBehaviour, INetworkClient
    {
        [SerializeField] string loginHost = "127.0.0.1";
        [SerializeField, Range(1, 65535)] int loginPort = 6900;
        [SerializeField, Min(1)] int maxPacketsPerFrame = 256;
        [SerializeField] NetworkSettings settings = new();

        public static NetworkClient Instance { get; }
        public NetworkFlow Flow { get; }

        internal void Pump();                             // called by Update; tests call it directly
    }
}
```

**Lifecycle.** `Awake`: enforce a single instance (destroy duplicates with a warning), `DontDestroyOnLoad`, build `PacketDispatcher(NetLog.HandlerError)`, `GeneratedPacketRouter(dispatcher, NetLog.DecodeError)` and `NetworkFlow(this, new UnityClock(), settings, loginEndpoint)`. `Update`: `Pump()`, then `Flow.Tick()`. `OnDestroy`: `Flow.Dispose()`, `Disconnect()`.

**`Connect`.** Creates a fresh `PacketFramer(HerculesPacketTable.Instance, router)`, a `CancellationTokenSource`, and starts `TcpConnection.ConnectAsync(host, port, role, framer, token)`. The task is stored as `_pending`; nothing is awaited (D8). A replaced or cancelled pending task gets a continuation (`TaskScheduler.Default`) that disposes its connection if it completed anyway. `Status = Connecting`.

**`Pump` (main thread):**
1. If `_pending` completed: success → `_connection = result`, `Status = Connected`, raise `Connected(role)`. Failure or cancellation → release, `Status = Idle`, raise `Disconnected(role, CloseInfo(ConnectFailed, message))`.
2. If connected: capture `framer` and `connection` locally; `n = framer.Process(maxPacketsPerFrame)` inside `try/catch` (defensive; logs via `Debug.LogException`). If the client was replaced or disconnected during `Process` (`_framer != framer`), stop here.
3. If `framer.IsFaulted`: `connection.Close(ProtocolFault, framer.FaultReason)`.
4. **Drain-before-close:** if `connection.IsClosed && n < maxPacketsPerFrame`: release, `Status = Idle`, `NetLog.ReportClosure(role, closure)`, raise `Disconnected(role, closure)`. Nothing can arrive after close, so packets received just before the server closed the socket (for example a login refusal) are dispatched first (FR-7).

`Disconnect` and `Connect` call `framer.Stop()` and `connection.Dispose()` on the old connection, so a handler that replaces the connection during dispatch stops the old stream at once. `Send` forwards to the active connection and returns `false` when none is connected.

No other class calls `PacketFramer.Process` (a source-scan test enforces it).

**`Subscriptions` (FR-9).** Owners register handlers through a helper that remembers them and releases them all at once:

```csharp
public sealed class Subscriptions
{
    public Subscriptions(INetworkClient client);
    public void Add<T>(PacketHandler<T> handler) where T : struct;   // subscribes now, remembered for Clear
    public void Clear();                                              // unsubscribes everything added
    public int  Count { get; }
}
```

Managers create the handler delegates once in their constructor and keep them in fields. `Clear` unsubscribes but keeps its entries, so the next `Add` of the same handler reuses the entry: `Subscriptions` itself allocates nothing after the first `Enter`/`Exit` cycle (the dispatcher's copy-on-write handler arrays still allocate, at setup time).

**`NetLog` (FR-16).** `[Conditional("UNINAROK_NET_TRACE")] Trace(...)` for per-packet tracing, so the call and its arguments disappear without the define. `ReportClosure(role, info)`: `LogWarning` for `RemoteClosed`, `LogError` for `Error`, `ProtocolFault` and `ConnectFailed`, nothing for `LocalDispose`. `HandlerError` → `Debug.LogException`. `DecodeError(id, e)` → `LogError` with the ID. State changes log at `Log`. Nothing informational uses `LogError`.

**Clock.** `IClock { long NowMs { get; } }`; `UnityClock` returns `(long)(Time.realtimeSinceStartupAsDouble * 1000.0)`. Monotonic, shared by deadlines, keepalives and the enter-map timestamp. Tests use `FakeClock`.

**Settings.**

```csharp
[Serializable] public sealed class NetworkSettings
{
    public float ConnectSeconds = 15, ReplySeconds = 15, MapLoadSeconds = 60;
    public float MapKeepaliveSeconds = 10, CharacterKeepaliveSeconds = 12;   // spec §6
}
```

### 4.8 Hercules length table
Satisfies FR-18, FR-19.

- Input: [`fixtures/hercules-packetlen-20220406.json`](fixtures/hercules-packetlen-20220406.json) (`{"_source": ..., "lengths": {"0x0064": 55, ...}}`, 1595 entries, extracted from Hercules `packets2022_len_main.h` evaluated for PACKETVER 20220406). When the target version changes, re-extract this file from Hercules first.
- `HerculesPacketTableGenerator` (Editor menu `Uninarok/Networking/Regenerate Hercules Packet Table`) reads the JSON, validates every value (`-1` or `>= 2`, no duplicates) and writes `Core/Protocol/HerculesPacketTable.g.cs`. The file is generated: never edit it by hand; change the JSON and rerun.
- Generated shape: `public sealed class HerculesPacketTable : IPacketLengthTable` with `public static readonly HerculesPacketTable Instance`; IDs and lengths as two `static readonly` arrays expanded once into a `short[65536]` (unknown = `-2`). `GetLength` is one array index.
- The same JSON is copied to `Assets/Tests/EditMode/Networking/Fixtures/` for tests, and a test checks that the generated table equals the fixture.
- Every ID in the table that has no `[InPacket]` struct is framed and skipped (FR-19). Only IDs absent from the table are protocol faults (FR-2).

### 4.9 Flow, managers and session data (Runtime)
Satisfies FR-9, FR-10, FR-11, FR-12, FR-13, FR-14, FR-20.

#### Session data (FR-12)

```csharp
namespace Uninarok.Networking.Flow
{
    public sealed class SessionContext
    {
        // login server
        public uint AccountId; public int AuthCode; public uint UserLevel; public AccountSex Sex;
        public readonly List<ServerEndpoint> Servers = new();
        // character server
        public readonly List<CharInfo> Characters = new();
        public CharacterSlots Slots;                 // from CHARACTER_SLOTS_INFO
        public int CharacterId; public byte SelectedSlot;   // CharacterId is int, like CharInfo.CharId and MAP_ENTER_REQUEST_ACCEPTED.CharId
        // map server
        public MapEndpoint Map;                      // where to connect
        public MapEntry Entry;                       // what AUTHENTICATION_ACCEPTED reported
        public void Reset();
    }
    public readonly struct ServerEndpoint { public readonly string Host; public readonly ushort Port; public readonly FixedString32Bytes Name;
                                            public readonly ushort UserCount, State, Property; }
    public readonly struct MapEndpoint   { public readonly string Host; public readonly ushort Port;
                                            public readonly FixedString32Bytes MapName; public readonly FixedString512Bytes DnsHost; }
    public readonly struct MapEntry      { public readonly uint StartTick; public readonly UninarokPosition Position;
                                            public readonly byte XSize, YSize; }
}
```

- Ports are `ushort` end to end. Packets whose doc gives the port as a signed wire type are converted once in the manager with `unchecked((ushort)value)`; IPv4 addresses are converted once to a dotted `string` `Host` (a cold, one-per-session allocation).
- Field types follow the packet docs; `SessionContext` is the only place the flow and managers share data.

#### Managers (FR-9, FR-13, FR-14, FR-20)

```csharp
namespace Uninarok.Networking.Managers
{
    public interface IServerManager
    {
        void Enter();                 // subscribe (Subscriptions), reset per-server state
        void Exit();                  // Subscriptions.Clear(), stop keepalive
        void OnConnected();           // the server's connection is up: send the opening request
        void Tick(long nowMs);        // keepalive
    }

    public sealed class LoginManager : IServerManager
    {
        public LoginManager(INetworkClient client, SessionContext context);
        public event Action Accepted;                         // Context login fields and Servers filled
        public event Action<LoginRefusal> Refused;            // LOGIN_REFUSAL, REFUSE_LOGIN_3 or LOGIN_SERVER_NOTICE
        public void SetCredentials(string user, string password);   // held until OnConnected sends REQUEST_LOGIN, then cleared
    }

    // Values from Hercules pincode_login_response: Ok 0, Ask 1, NotSet 2, Expired 3, RestrictPassword 5, Unused 7, Wrong 8.
    public enum PinState { Unknown = -1, Ok = 0, Ask = 1, NotSet = 2, Expired = 3, RestrictPassword = 5, Unused = 7, Wrong = 8 }
    // LoginRefusal { Source (Refusal | Refuse3 | ServerNotice), ErrorCode (int), BlockDate (FixedString64Bytes) }

    public sealed class CharacterManager : IServerManager
    {
        public CharacterManager(INetworkClient client, SessionContext context, NetworkSettings settings);
        public PinState PinState { get; }                     // Unknown on Enter, never a default zero
        public event Action<PinState> PinStateChanged;
        public event Action CharactersReceived;               // Context.Characters filled
        public event Action CreationSucceeded;                // character added to Context.Characters
        public event Action<byte> CreationFailed;             // reason code
        public event Action MapServerAssigned;                // Context.Map and CharacterId filled
        public event Action<byte> MapEnterRefused;            // reason code
        public void RequestCreate(in CharacterDraft draft);   // CREATE_CHARACTER
        public void RequestSelect(byte slot);                 // SELECT_CHARACTER
        // OnConnected: client.SkipNextBytes(4) BEFORE sending CHARACTER_SERVER_REQUEST_ENTRY (FR-3)
        // Tick: CHARACTER_SERVER_PING (AccountId) every CharacterKeepaliveSeconds while entered and connected
    }

    public sealed class ZoneManager : IServerManager
    {
        public ZoneManager(INetworkClient client, SessionContext context, IClock clock, NetworkSettings settings);
        public event Action Authenticated;                    // AUTHENTICATION_ACCEPTED stored in Context.Entry
        public event Action<byte> EnterRefused;               // MAP_ENTRY_REFUSAL
        public void SendMapLoaded();                          // MAP_LOADED, then starts the keepalive
        // OnConnected: REQUEST_ENTER_MAP with Context values and (uint)clock.NowMs
        // Tick: HEARTBEAT_REQUEST with (uint)nowMs every MapKeepaliveSeconds, only after SendMapLoaded
    }
}
```

- Managers never change flow state, never open or close connections, and never touch `TcpConnection`. Every send goes through `INetworkClient.Send<T>` with a stack-built struct: no per-send allocation (FR-6).
- Each manager subscribes only to the packets of its server, and only between `Enter` and `Exit` (FR-9).
- Keepalive scheduling: `_nextKeepaliveAt = nowMs + interval` when started; `Tick` sends when `nowMs >= _nextKeepaliveAt` and advances by the interval; `Exit` stops it. `SERVER_HEARTBEAT_RESPONSE` is handled (subscribed) but only traced.

#### Flow (FR-10, FR-11, FR-13)

```csharp
namespace Uninarok.Networking.Flow
{
    public enum FlowState
    {
        Idle, LoggingIn, ServerSelection, ConnectingToChar, CharSelection,
        CreatingCharacter, SelectingCharacter, ConnectingToMap, LoadingMap, InGame,
        Failed, Disconnected
    }
    public enum FailureCode { ConnectFailed, ConnectTimeout, ReplyTimeout, LoginRefused,
                              RemoteClosed, ProtocolFault, TransportError, MapRefused }
    public readonly struct FlowFailure { public readonly FailureCode Code; public readonly string Message; }

    public sealed class NetworkFlow : IDisposable
    {
        public NetworkFlow(INetworkClient client, IClock clock, NetworkSettings settings, ServerEndpoint loginServer);

        public FlowState      State   { get; }
        public FlowFailure?   Failure { get; }
        public SessionContext Context { get; }
        public LoginManager     Login     { get; }
        public CharacterManager Character { get; }      // PinState, last creation/refusal reason
        public ZoneManager      Zone      { get; }
        public event Action<FlowState> StateChanged;    // main thread

        // Commands (main thread). Each validates the current state; otherwise LogWarning and ignore.
        public void StartLogin(string user, string password);
        public void SelectServer(int index);
        public void CreateCharacter(in CharacterDraft draft);
        public void SelectCharacter(byte slot);
        public void MapLoaded();                        // the game finished loading the map scene
        public void Reset();                            // Failed/Disconnected → Idle

        public void Tick();                             // called by NetworkClient after Pump
        public void Dispose();                          // unsubscribes from the client and managers
    }
}
```

The flow subscribes to `client.Connected`, `client.Disconnected` and every manager event once, in its constructor. Exactly one manager is active at a time (`Enter`ed before its connection is opened, so no packet is missed; `Exit`ed when leaving its server). `Connected(role)` is forwarded to the active manager's `OnConnected` if the role matches.

**Transition table (anything else is rejected with a warning, FR-10):**

| From | Event | Action | To |
|---|---|---|---|
| Idle | `StartLogin(u, p)` | `Login.Enter`, `Login.SetCredentials`, `client.Connect(login)` | LoggingIn |
| LoggingIn | `Login.Accepted` | `client.Disconnect`, `Login.Exit` | ServerSelection |
| LoggingIn | `Login.Refused` | `client.Disconnect`, `Login.Exit` | Failed(`LoginRefused`) |
| ServerSelection | `SelectServer(i)` (valid index) | `Character.Enter`, `client.Connect(Servers[i])` | ConnectingToChar |
| ConnectingToChar | `Character.CharactersReceived` | | CharSelection |
| CharSelection | `CreateCharacter` | `Character.RequestCreate` | CreatingCharacter |
| CharSelection | `SelectCharacter` | `Character.RequestSelect` | SelectingCharacter |
| CreatingCharacter | `CreationSucceeded` / `CreationFailed` | reason kept on `Character` | CharSelection |
| SelectingCharacter | `MapEnterRefused` | reason kept on `Character` | CharSelection |
| SelectingCharacter | `MapServerAssigned` | `Character.Exit`, `Zone.Enter`, `client.Connect(Context.Map)` | ConnectingToMap |
| ConnectingToMap | `Connected(Map)` | `Zone.OnConnected` (sends enter request) | LoadingMap |
| LoadingMap | `Zone.Authenticated` | deadline becomes `MapLoadSeconds` | LoadingMap |
| LoadingMap | `Zone.EnterRefused` | `client.Disconnect`, `Zone.Exit` | Failed(`MapRefused`) |
| LoadingMap (authenticated) | `MapLoaded()` | `Zone.SendMapLoaded` | InGame |
| LoggingIn … LoadingMap | `Disconnected(role)` of the active role | managers `Exit` | Failed(code from `CloseReason`: `ConnectFailed`, `RemoteClosed`, `ProtocolFault`, `Error` → `TransportError`) |
| InGame | `Disconnected(Map)` | `Zone.Exit` | Disconnected |
| any waiting state | deadline exceeded | `client.Disconnect`, managers `Exit` | Failed(`ConnectTimeout` before `Connected`, else `ReplyTimeout`) |
| Failed, Disconnected | `Reset()` | `Context.Reset`, failure cleared | Idle |

**Deadlines (FR-10).** Entering a waiting state (LoggingIn, ConnectingToChar, CreatingCharacter, SelectingCharacter, ConnectingToMap, LoadingMap) sets `deadline = now + ConnectSeconds` while the connection is not yet up, then `now + ReplySeconds` from `Connected` on. In `LoadingMap` the deadline becomes `now + MapLoadSeconds` once `Authenticated` arrives (the client's own scene load). Leaving the state clears it. ServerSelection and CharSelection wait for the user and have no deadline (the char-server keepalive keeps CharSelection alive). The flow is the single owner of timeouts; `NetworkClient` has none.

**Tick.** `Tick()` reads `clock.NowMs`, checks the deadline, then calls `Tick(nowMs)` on the active manager.

**Login refusal (FR-11).** One `REQUEST_LOGIN` per `StartLogin`; a refusal ends in `Failed(LoginRefused)` and nothing retries. Any retry policy belongs to the caller.

**Stale events.** Because `Connect`/`Disconnect` drop the previous connection silently and stop its framer, events from a replaced connection never reach the flow. `Disconnected` for a role that is not active is ignored.

### 4.10 Allocation policy
Satisfies FR-6.

| Source | Policy |
|---|---|
| Received payload | `ReadOnlySpan<byte>` over the framer's work buffer; no copy (§4.3) |
| Per-packet logging | only through `[Conditional("UNINAROK_NET_TRACE")] NetLog.Trace`: compiled out without the define |
| Outgoing serialization | `Send<T>` serializes straight into the outbox (§4.4); no `byte[]`, `Memory<byte>` or pooled arrays |
| Socket I/O | blocking `Receive`/`Send` on dedicated threads (D9); buffers allocated once per connection |
| Dispatch | instance dispatcher, array-indexed per-type slot, copy-on-write handler arrays (§4.5) |
| Subscriptions | delegates created once per manager; `Enter`/`Exit` cycles allocate nothing after the first |
| Buffer growth | amortized doubling from 16/32 KB; rare |
| Async | no `async void`; the only `Task` is the one-shot `ConnectAsync` per connection |

**Packet rules for hot packets** (spec §6): no arrays or `string` fields; text through `WireText` into `FixedString*`; repeated records exposed through a reused buffer or one dispatch per record. Cold packets (login accept, character list, map info) may allocate while decoding; their docs say so.

**Handler rules:** no string interpolation, LINQ, closures or new collections in handlers of hot packets.

**Measuring:**
1. EditMode: `Is.Not.AllocatingGCMemory()` over main-thread operations: `PacketFramer.Process` with a stub router, `TcpConnection.Send<T>` for a fixed packet, `PacketDispatcher.Dispatch<T>` with subscribers, a manager keepalive `Tick`. This constraint observes the main thread only.
2. Development build, Unity Profiler: GC Alloc per frame on the main thread in-game idle and during a packet burst. Socket threads register with `Profiler.BeginThreadProfiling("Uninarok.Net", "Read"/"Write")` (from a Runtime hook, since Core is engine-free) so their allocations show in the CPU module.

## 5. Threading and lifecycle

Satisfies FR-5, FR-7.

| Thread | May do | Must not |
|---|---|---|
| Socket reader and writer threads | blocking `Receive`/`Send`, `IInboundSink.Append`, outbox swap, `TcpConnection.Close` | any `UnityEngine` API, call handlers, touch flow, managers or `NetworkClient` |
| Thread pool (connect task, stale-connect cleanup) | DNS, `TcpClient.ConnectAsync`, `Dispose` of a stale connection | same as above |
| Main thread | `Pump`, `Process`, dispatch, `Send`, `SkipNextBytes`, flow commands, `Tick`, raising `Connected`/`Disconnected` | block on I/O |

- Connect completion is observed by polling the task in `Pump`, not by `await`: no synchronization-context assumptions, and connection events are raised at a fixed point in the frame.
- `NetworkClient` is created once and survives scene loads (`DontDestroyOnLoad`). `OnDestroy` disposes the flow, drops the active and pending connection, and leaves no thread running.
- No `async void` anywhere in the layer.

## 6. Test plan

Satisfies FR-17, FR-20 and verifies FR-1 to FR-19.

| Suite (`Assets/Tests/EditMode/Networking/`) | Covers |
|---|---|
| `PacketReaderWriterTests` | FR-6, FR-18: little-endian primitives, overrun throws `PacketFormatException`, `Skip`, `Remaining`, zero allocation |
| `WireTextTests` | FR-6: NUL termination, zero fill, truncation, Windows-1252 round trip, capacity check per field |
| `DataTypeTests` | FR-18: each `Runtime/Data` type against the vectors in its `docs/data` doc |
| `FramerFramingTests` | FR-1: fixed, variable, split at every byte offset, coalesced, back-to-back |
| `FramerFaultTests` | FR-2: unknown ID, ID 0, length < 4, invalid table value, cap exceeded; one fault, no routing after |
| `FramerSkipTests` | FR-3: skip in 1-byte chunks, skip of zeros, skip before any packet |
| `FramerLimitsTests` | FR-5, FR-19: 1000 packets with cap 256 take 4 calls; a throwing router does not corrupt the next call; `Stop` ends processing; known-but-unhandled ID skipped |
| `FramerConcurrencyTests` | FR-5: `Append` is not blocked while the router runs 100 ms |
| `FramerAllocTests` | FR-6: `Is.Not.AllocatingGCMemory()` over 1000 fixed packets (stub router) |
| `DispatcherTests` | FR-4, FR-9: throwing handler does not block the next; subscribe/unsubscribe during dispatch; 100 add/remove cycles leave the count unchanged; zero allocation with 3 subscribers |
| `TcpConnectionTests` (loopback `TcpListener`) | FR-7, FR-8: remote close observed < 1 s; `Dispose` is silent and ends both threads; empty send is a no-op; send after close returns `false`; backlog overflow closes with `Error` |
| `OutgoingTests` (loopback) | FR-6, FR-8: 10 000 sends arrive complete and in order; 4 concurrent sender threads produce no partial or interleaved packets (checksum per packet); `Send<T>` allocates 0 bytes after warm-up; a throwing `Serialize` leaves no partial bytes |
| `PacketTableTests` | FR-18, FR-19: generated table equals the fixture; no invalid values |
| `PacketSizeTests` | FR-18: every `[InPacket]`/`[OutPacket]` `Size` equals the table |
| `PacketDecodeTests` | FR-18: each in-scope incoming packet decodes its doc's sample with `Remaining == 0`; each outgoing packet writes exactly `WireSize` bytes equal to golden bytes from its doc |
| `RouterTests` | FR-4, FR-19: a malformed payload calls `onDecodeError` once and the next packet is dispatched; unhandled ID returns `false` |
| `NetworkClientTests` (component on a test GameObject, loopback server, `Pump` called directly) | FR-7, FR-20: `Connected` raised once; connect to a closed port raises `Disconnected(ConnectFailed)`; drain-before-close (refusal then close: refusal handled first); `Disconnect` raises nothing; a handler that reconnects mid-pump stops the old stream; protocol fault closes with `ProtocolFault` and one error log |
| `LoginManagerTests`, `CharacterManagerTests`, `ZoneManagerTests` (`FakeNetworkClient`) | FR-3, FR-12, FR-13, FR-14, FR-20: requests sent with the right bytes; `SkipNextBytes(4)` before the char entry request; PIN starts `Unknown`; creation failure exposes reason; port 40000 stored as 40000; keepalive intervals and increasing timestamps; none after `Exit`; keepalive tick allocates nothing |
| `FlowTransitionTests` (`FakeNetworkClient`, `FakeClock`) | FR-10, FR-11, FR-13, FR-20: table-driven valid and invalid transitions; every deadline; refusal sends exactly one request; `Reset` |
| `CaptureReplayTests` | FR-1: each `Captures/*.bin` replayed with chunk sizes {1,2,3,7,64,whole} against `*.expected.txt` (`skip=N` header, then `id,payloadLen,crc32` per packet) |
| `PumpOwnershipTests` | FR-5, FR-20: source scan shows `Process` called only from `NetworkClient` and tests, and `TcpConnection` used only by `NetworkClient` and Core tests |
| `GeneratorTests` (`Tools/Uninarok.Networking.Generator.Tests`, `dotnet test`) | FR-4, FR-18: router generated for `[InPacket]` structs and compiles; handled IDs sorted; unhandled ID returns `false`; malformed payload reaches `onDecodeError`; diagnostics for duplicate ID, size 0/1/below -1, missing `(ReadOnlySpan<byte>)` constructor, `[OutPacket]` without `IOutgoingPacket`, non-readonly struct; no generated file without packets |

`FakeNetworkClient : INetworkClient` (test assembly) records sent packets as bytes (by calling `Serialize`), lets tests raise `Connected`/`Disconnected`, and delivers incoming packets through a real `PacketDispatcher`.

Golden files are generated once from the whole-buffer run, reviewed by hand, then committed. The manual smoke test against local Hercules is in `tasks.md` phase 9.

CLI (adjust path/version): `Unity -batchmode -projectPath Uninarok-client -runTests -testPlatform EditMode -testResults results.xml -quit`.

## 7. Migration and compatibility

The pre-existing prototype networking is removed, not migrated (decided 2026-11-17): `Assets/Scripts/Network/` (packets, `TcpConnection`, serializer, dispatcher, bootstrap), its two consumers in `Assets/Scripts/Managers/` (`GameConnectionManager`, `LoginManager`), the `UninarokSourceGenerator` project and its DLL in `Assets/Plugins/`. Their `MonoBehaviour`s were stripped from the `Bootstrap` scene, which keeps an empty `Bootstrap` object for `NetworkClient` and, in development, `DevAutoLogin` (phase 7). Nothing else referenced them. Git history keeps the old code.

Scenes that need networking add a `NetworkClient` (and, in development, `DevAutoLogin`).

### 7.1 Development auto-login — `DevAutoLogin`
Satisfies FR-15.

The whole file is wrapped in `#if UNITY_EDITOR || DEVELOPMENT_BUILD`, so release builds do not contain the type. It subscribes to `NetworkFlow.StateChanged` and `CharacterManager.PinStateChanged` and acts **once per state entry** (an `_actedFor` guard):

| State | Action |
|---|---|
| (Start) | `Flow.StartLogin(user, password)` once |
| ServerSelection | `SelectServer(serverIndex)` |
| CharSelection | If `Context.Characters` is empty and `PinState` is known and allows it: `CreateCharacter(draft from fields)`. Otherwise `SelectCharacter(characterSlot)` |
| LoadingMap (after `Zone.Authenticated`) | `Flow.MapLoaded()` once (no scene to load) |
| Failed | log the reason; `Reset` + `StartLogin` again only while `attempt < maxLoginAttempts` (default `1`) |

Every serialized field (`serverIndex`, `characterSlot`, draft fields, `maxLoginAttempts`) is used. **Credentials are not serialized in the scene:** they are read from `EditorPrefs` (editor) or a `dev-login.json` under `Application.persistentDataPath` (development player), both outside the repository. If none is set, it logs a warning and does nothing.

## 8. Decisions

| # | Decision | Alternatives rejected |
|---|---|---|
| D1 | Inbox + work double buffer in the framer; lock only for the swap | Single buffer under one lock (blocks the socket thread during dispatch); lock-free ring (complexity not justified) |
| D2 | Per-frame cap of 256 packets | Unlimited (frame spikes) |
| D3 | A framing fault closes the connection; no resync | Skip-ahead heuristics (unsafe without length knowledge) |
| D4 | Engine-free Core assembly with framing, wire, dispatch and transport | Everything in one Unity assembly (needs the engine for core tests) |
| D5 | Instance `PacketDispatcher` with per-type array slots and copy-on-write handler arrays | Static global dispatcher (hidden global state, harder tests); `Dictionary<Type, …>` or `object` bus (boxing, lookups) |
| D6 | One `MonoBehaviour` (`NetworkClient`) as transport, pump and composition root; managers and flow are plain C# behind `INetworkClient`; closure as immutable `CloseInfo` | One `MonoBehaviour` per manager (undefined update order, scene lifetime, needs PlayMode tests); managers holding `TcpConnection` (untestable without sockets); a second `MonoBehaviour` to own the flow (two update orders to coordinate) |
| D7 | Synchronous `Send<T>(in packet)` serializing into a double-buffered outbox; failure reported by `bool` and closure | `SendAsync`/`ValueTask` (hides that "sent" means "queued"); exceptions for backlog overflow |
| D8 | Connect task polled in `Pump`; no `async void`; replaced connects disposed by continuation | `await` in the flow (continuations depend on the synchronization context and can resume after `Reset`) |
| D9 | Dedicated reader and writer threads with blocking `Socket.Receive`/`Send` and an `AutoResetEvent` wake-up | `NetworkStream.ReadAsync/WriteAsync` and `SemaphoreSlim.WaitAsync` (a `Task` per I/O on some runtimes); `SocketAsyncEventArgs` (zero-alloc but more code; the fallback if blocking threads misbehave on a platform) |
| D10 | Roslyn source generator emits the router `switch` from `[InPacket]` structs | Hand-written registration (does not scale to hundreds of packets, easy to forget); reflection at startup (AOT/IL2CPP issues with generic instantiation, startup cost) |
| D11 | `PacketReader`/`PacketWriter` as `ref struct`s in Core; `FixedString*` handling in Runtime (`WireText`) | Per-packet `BitConverter` code (duplicated, error-prone); Core depending on `Unity.Collections` (loses engine-free tests) |
| D12 | Lengths for the framer come from a table generated from the Hercules fixture; struct sizes are checked against it by test | Lengths from struct attributes only (every unhandled packet would be a fault, FR-19); trusting hand-typed sizes |
| D13 | Shared packet data in `Runtime/Data` with one doc each in `docs/data` | Types nested inside the first packet that used them (cross-packet coupling) |
| D14 | `NetworkFlow` orchestrates; managers only speak their server's protocol and raise events | Managers handing off to each other directly (each would know the next server and the timeouts; the state machine would be spread over three classes) |
| D15 | Windows-1252 through a hand-written table in `WireText` | `System.Text.Encoding.CodePages` (extra dependency, allocating APIs in places) |
| D16 | Dev credentials read from `EditorPrefs` or a local file outside the repository | Serialized fields in the scene (credentials committed in plain text) |
| D17 | `Close` shuts the socket down and then closes it, so a blocked `Receive` is woken on Mono; closure is recorded first so the resulting exception is silent | Shutdown only with the socket disposed by the last thread (measured: Mono's blocked `Receive` is not woken by `Shutdown`); `Poll` loop with timeout (also not woken by `Shutdown`, adds wake-ups) |
| D18 | The generator has its own `dotnet test` project (NUnit, in-memory generator driver, Core compiled from source) | Testing the generator only through Unity compilation (cannot assert diagnostics or run on a clean compilation); a Unity-side test that loads Roslyn into the editor domain (fragile) |

## 9. Risks

| Risk | Mitigation |
|---|---|
| Char server echo is not exactly 4 bytes for this PACKETVER | Verify with real captures (T9.1) before relying on `SkipNextBytes(4)` |
| A blocked `Receive` is not woken by close on some platform (Linux, Android, iOS) | `Shutdown(Both)` then close the socket (D17: verified on the Windows editor, where `Shutdown` alone is not enough); verify on each target platform (T9.7); fallback `SocketAsyncEventArgs` (D9) |
| Unity's supported Roslyn version limits the generator API | Generator targets `netstandard2.0` and the `Microsoft.CodeAnalysis` version documented for the editor in use (T4.3) |
| Generic interface calls (`INetworkClient.Send<T>`) allocate on first use under Mono or IL2CPP | Covered by the allocation tests after warm-up; if not, managers take `NetworkClient`'s send through a sealed class instead of the interface |
| Per-I/O allocation claims depend on Unity's runtime | Profiler measurement in T9.6; D9 is contained inside `TcpConnection` |
| A wrong declared size desynchronizes the connection | FR-18 size tests against the Hercules table; the framer uses the table, not struct sizes |
| `FixedString*` is UTF-8 but the server may send a legacy codepage | `WireText` converts in one place; the encoding is one constant (spec §6) |
| Drain-before-close delays closure under a constant packet flood | The per-frame cap is finite, so closure is delivered within a few frames |

## 10. Traceability

| FR | Plan sections |
|---|---|
| FR-1 | 4.3, 6 |
| FR-2 | 4.3, 4.4, 4.7, 4.8 |
| FR-3 | 4.3, 4.9 |
| FR-4 | 4.5, 4.6 |
| FR-5 | 2, 4.3, 4.7, 5 |
| FR-6 | 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.10 |
| FR-7 | 4.4, 4.7, 5 |
| FR-8 | 4.4, 4.7 |
| FR-9 | 4.5, 4.7, 4.9 |
| FR-10, FR-11 | 4.9 |
| FR-12, FR-13 | 4.9 |
| FR-14 | 4.7 (settings), 4.9 |
| FR-15 | 7.1 |
| FR-16 | 4.7 |
| FR-17 | 3, 6 |
| FR-18 | 4.1, 4.6, 4.8, 6 |
| FR-19 | 4.3, 4.6, 4.8 |
| FR-20 | 2, 3, 4.7, 4.9, 6 |
