# TASKS-0001: Networking Core

| | |
|---|---|
| **Implements** | [plan.md](plan.md) → [spec.md](spec.md) |
| **Status** | Implementing |

**How to use this file**
- Do tasks **in order**. One task = one reviewable commit. Tick the box in the same commit.
- Each task lists the requirements it serves (`FR-n`) and a **Done when** check. Do not tick without verifying it.
- Tests come before implementation (red → green). A "failing test" task is committed failing or skipped with `[Ignore("pending Tn")]`, then enabled by the implementation task.
- If a task needs a change not covered by the plan, stop and update `plan.md` first.

---

## Phase 0: Prepare

- [x] **T0.1** Create branch `feature/0001-networking-core`.
  *Done when:* the branch exists and the project compiles.
- [x] **T0.2** Record the session-state and keepalive decisions (`InGame` after `MAP_LOADED`; map 10 s, char 12 s; `spec.md` section 6).
  *Done when:* spec section 6 states them.
- [x] **T0.3** Decide the wire text encoding: Windows-1252 default, one constant (`spec.md` section 6).
  *Done when:* encoding stated in the packets reference and spec section 6.
- [x] **T0.4** Add `com.unity.collections` and `com.unity.test-framework` as direct dependencies in `Packages/manifest.json` (versions already resolved in `packages-lock.json`).
  *Done when:* the manifest lists both; the project compiles.
- [x] **T0.5** Create the assemblies of plan §3: `Uninarok.Networking.Core` (`noEngineReferences: true`), `Uninarok.Networking` (references Core and `Unity.Collections`, `InternalsVisibleTo` tests), `Uninarok.Networking.Editor`, `Uninarok.Networking.Tests` (EditMode). *(FR-17)*
  *Done when:* project compiles; an empty test runs in the Test Runner and in batch mode.

## Phase 1: Wire primitives and shared data (FR-6, FR-18)

- [x] **T1.1** Write failing `PacketReaderWriterTests`: little-endian primitives, `ReadSpan`, `Skip`, `Remaining`, overrun throws `PacketFormatException`, zero allocation on the happy path.
- [x] **T1.2** Implement `PacketReader`, `PacketWriter`, `PacketFormatException` in `Core/Wire/` (plan §4.1).
  *Done when:* T1.1 passes.
- [x] **T1.3** Write failing `WireTextTests`: NUL termination, zero fill, truncation, Windows-1252 round trip including 0x80–0x9F, capacity check. *(FR-6)*
- [x] **T1.4** Implement `WireText` with the hand-written Windows-1252 table, one overload per `FixedString*` size in use (plan §4.2).
  *Done when:* T1.3 passes.
- [x] **T1.5** Implement the shared data types of `docs/data/` (`AccountSex`, `CharInfo`, `UninarokDirection`, `UninarokPosition`, `UninarokMoveData`) in `Runtime/Data/` with `Read`/`Write` and `DataTypeTests` from the vectors in each doc. *(FR-18)*
  *Done when:* data tests pass.

## Phase 2: Framing (FR-1, FR-2, FR-3, FR-5, FR-6, FR-17, FR-19)

- [x] **T2.1** Add `IPacketLengthTable`, `IPacketRouter`, `IInboundSink` in `Core/Framing/` and test helpers `TableStub` (table-driven lengths) and `RecordingRouter` (records `(id, payload copy)`, configurable handled IDs).
  *Done when:* compiles; helpers used by one test.
- [x] **T2.2** Write failing `FramerFramingTests`: fixed, variable, split at every byte offset, coalesced, back-to-back. *(FR-1)*
- [x] **T2.3** Write failing `FramerFaultTests`: unknown ID, ID 0, variable length < 4, invalid table length, cap exceeded, no routing after fault, single `FaultReason` with hex. *(FR-2)*
- [x] **T2.4** Write failing `FramerSkipTests`: skip split across chunks, skip of `00 00 00 00`, skip larger than available data. *(FR-3)*
- [x] **T2.5** Write failing `FramerLimitsTests` (cap 256 → 4 calls for 1000 packets; throwing router leaves state consistent; `Stop`; known-but-unhandled ID skipped) and `FramerConcurrencyTests` (`Append` not blocked while the router runs). *(FR-5, FR-19)*
- [x] **T2.6** Write failing `FramerAllocTests` with `Is.Not.AllocatingGCMemory()` over 1000 fixed packets. *(FR-6)*
- [x] **T2.7** Implement `PacketFramer` buffers: inbox/work double buffer, `Append` (returns `false` over cap), drain with compaction and doubling growth (plan §4.3, D1).
  *Done when:* the buffer parts of T2.2 and T2.5 pass.
- [x] **T2.8** Implement `Process` steps 1–3 with header handling, advance-then-route, unhandled skip, per-call cap and `Stop`. *(FR-1, FR-5, FR-19)*
  *Done when:* T2.2, T2.5 and T2.6 pass.
- [x] **T2.9** Implement `SkipNextBytes` and the skip step before header parsing. *(FR-3)*
  *Done when:* T2.4 passes.
- [x] **T2.10** Implement faults (`IsFaulted`, `FaultReason` with up to 32 hex bytes, stop parsing, return 0). *(FR-2)*
  *Done when:* T2.3 passes; all Phase 2 tests green.

## Phase 3: Dispatch and transport (FR-2, FR-4, FR-6, FR-7, FR-8, FR-9)

- [x] **T3.1** Write failing `DispatcherTests`: throwing handler does not block the next; subscribe/unsubscribe during dispatch applies from the next packet; 100 subscribe/unsubscribe cycles leave the count unchanged; `Dispatch` with 3 subscribers allocates 0 bytes. *(FR-4, FR-6, FR-9)*
- [x] **T3.2** Implement `PacketHandler<T>` and `PacketDispatcher` (per-type slots, copy-on-write arrays, per-callback `try/catch`, plan §4.5).
  *Done when:* T3.1 passes.
- [x] **T3.3** Add `ConnectionRole`, `CloseReason`, `CloseInfo`, `IOutgoingPacket` in `Core/Transport/`.
  *Done when:* compiles.
- [x] **T3.4** Write failing `TcpConnectionTests` (loopback `TcpListener`): remote close observed < 1 s with `RemoteClosed`; `Dispose` is silent (`LocalDispose`) and both threads exit; empty send returns `true` and sends nothing; send after close returns `false`; sink overflow closes with `ProtocolFault`. *(FR-7, FR-8)*
- [x] **T3.5** Implement `ConnectAsync` (cancellation disposes the socket), closure (`Close`, first-wins `CompareExchange`, `IsClosed`, `Closure`) and the reader thread with blocking `Receive`, shutdown-quiet filters and `Shutdown(Both)` on close (plan §4.4). *(FR-2, FR-7)*
  *Done when:* the receive and close parts of T3.4 pass.
- [x] **T3.6** Write failing `OutgoingTests`: 10 000 sends arrive complete and in order; 4 concurrent sender threads produce no partial or interleaved packets; `Send<T>` of a fixed test packet allocates 0 bytes after warm-up; a throwing `Serialize` leaves no partial bytes; backlog overflow closes with `Error` and returns `false`. *(FR-6, FR-8)*
- [x] **T3.7** Implement the outbox, writer buffer swap, idle flag, `AutoResetEvent` wake-up, writer thread, `MaxSendBacklogBytes`, `Send<T>` and `Send(ReadOnlySpan<byte>)`. *(FR-6, FR-8)*
  *Done when:* T3.4 and T3.6 pass.
- [x] **T3.8** Handle ownership on exit: the last exiting thread disposes `_outSignal` and the socket. Stress test: 100 connect/dispose cycles with no exception and no leaked thread. *(FR-7)*
  *Done when:* stress test passes.

## Phase 4: Packets, length table and generated router (FR-4, FR-6, FR-18, FR-19)

- [x] **T4.1** Add `InPacketAttribute`/`OutPacketAttribute` in `Core/Protocol/` and `HerculesPacketTableGenerator` (Editor menu) that turns `fixtures/hercules-packetlen-20220406.json` into `Core/Protocol/HerculesPacketTable.g.cs` (plan §4.8). Generate and commit the table. *(FR-18, FR-19)*
  *Done when:* the table compiles and `GetLength(0x0064) == 55`, `GetLength(0x006B) == -1`, `GetLength(0x0000) == -2`.
- [x] **T4.2** Copy the fixture to `Tests/EditMode/Networking/Fixtures/` and add `PacketTableTests` (generated table equals the fixture; no invalid values). *(FR-18)*
  *Done when:* test passes.
- [x] **T4.3** Create the generator project `Tools/Uninarok.Networking.Generator/` (`netstandard2.0`, `Microsoft.CodeAnalysis` version supported by the editor), emitting `GeneratedPacketRouter` and the compile-time diagnostics of plan §4.6. Build it into `Runtime/Packets/Generator/` with the `RoslynAnalyzer` label and all platforms excluded. *(FR-4, FR-18)*
  *Done when:* with one test `[InPacket]` struct the router is generated; a duplicate ID or size `0` fails compilation with the generator's diagnostic; an assembly without packets compiles with no generated file.
- [x] **T4.4** Write failing `RouterTests`: malformed payload calls `onDecodeError` once and the next packet is dispatched; unhandled ID returns `false`. *(FR-4, FR-19)*
  *Done when:* tests exist and fail or are ignored pending T4.6.
- [x] **T4.5** Write `PacketSizeTests`: every `[InPacket]`/`[OutPacket]` `Size` equals `HerculesPacketTable`. *(FR-18)*
  *Done when:* test exists (passes trivially until packets are added).
- [x] **T4.6** Implement the login-server packets in scope (plan §4.6) with `PacketIds`, following `Packets/Packet-Implementation.md`; add their `PacketDecodeTests` and golden-byte tests from the docs. Update each doc's `Status` in the same commit. *(FR-6, FR-18)*
  *Done when:* decode, golden-byte, size and router tests pass for these packets.
- [x] **T4.7** Same for the character-server packets in scope. *(FR-6, FR-18)*
  *Done when:* as T4.6.
- [x] **T4.8** Same for the map-server packets in scope. *(FR-6, FR-18)*
  *Done when:* as T4.6.

## Phase 5: Connection point (FR-5, FR-7, FR-8, FR-9, FR-16, FR-20)

- [x] **T5.1** Add `NetLog` (`Trace` with `[Conditional("UNINAROK_NET_TRACE")]`, `ReportClosure`, `HandlerError`, `DecodeError`), `IClock`, `UnityClock` and `NetworkSettings`. *(FR-16)*
  *Done when:* compiles; a test shows `Trace` calls are removed without the define (no allocation).
- [x] **T5.2** Add `INetworkClient`, `ConnectionStatus` and `Subscriptions`, with a `Subscriptions` test: 100 add/clear cycles leave handler counts unchanged. *(FR-9, FR-20)*
- [x] **T5.3** Add `FakeNetworkClient` in the test assembly (records sent bytes, raises `Connected`/`Disconnected`, delivers packets through a real dispatcher). *(FR-20)*
  *Done when:* used by one test.
- [x] **T5.4** Write failing `NetworkClientTests` (plan §6): `Connected` raised once; closed port raises `Disconnected(ConnectFailed)`; refusal followed by server close delivers the refusal first; `Disconnect` raises nothing and logs nothing; reconnect from a handler stops the old stream; unknown ID closes with `ProtocolFault` and exactly one error log. *(FR-2, FR-7, FR-8)*
- [x] **T5.5** Implement `NetworkClient` (plan §4.7): single instance, `DontDestroyOnLoad`, composition in `Awake`, `Connect`/`Disconnect` with framer `Stop` and stale-connect cleanup, `Pump` steps 1–4, `Send`, `SkipNextBytes`, `Subscribe`. Leave `Flow` construction to T6.8. *(FR-5, FR-7, FR-8)*
  *Done when:* T5.4 passes.
- [x] **T5.6** Write `PumpOwnershipTests` (source scan: `Process` only from `NetworkClient` and tests; `TcpConnection` only from `NetworkClient` and Core tests). *(FR-5, FR-20)*
  *Done when:* test passes.
- [x] **T5.7** (Optional, `UNINAROK_NET_TRACE`) Add the capture tee that writes received bytes to `persistentDataPath/captures/{role}-{timestamp}.bin`.
  *Done when:* a run against Hercules produces capture files. *(Verified here against a loopback server with `UNINAROK_NET_TRACE` defined: `Login-20261117-112049.bin` held the received bytes. The run against Hercules is T9.1.)*

## Phase 6: Managers and flow (FR-3, FR-9 to FR-14, FR-20)

- [x] **T6.1** Add `SessionContext` (`ServerEndpoint` already exists since T4.6), `CharacterSlots`, `MapEndpoint`, `MapEntry`, `CharacterDraft`, `FlowState`, `FailureCode`, `FlowFailure`, `IServerManager`, `LoginRefusal`, `PinState`, and `FakeClock` in tests (plan §4.9). *(FR-12)*
  *Done when:* compiles.
- [x] **T6.2** Write failing `LoginManagerTests` (`FakeNetworkClient`): `REQUEST_LOGIN` bytes sent on `OnConnected`, credentials cleared after; `ACCEPT_LOGIN` fills `Context` and `Servers` and raises `Accepted`; each of the three refusal packets raises `Refused` once; no subscription after `Exit`. *(FR-9, FR-11, FR-12, FR-20)*
- [x] **T6.3** Implement `LoginManager`.
  *Done when:* T6.2 passes.
- [ ] **T6.4** Write failing `CharacterManagerTests`: `SkipNextBytes(4)` is called before `CHARACTER_SERVER_REQUEST_ENTRY` is sent; `PinState` is `Unknown` after `Enter`; char list fills `Context.Characters`; creation success adds a character; creation failure exposes the reason; map assignment with port 40000 stores 40000; refusal exposes the reason; ping every 12 s with `AccountId`, none after `Exit`, keepalive `Tick` allocates nothing. *(FR-3, FR-12, FR-13, FR-14)*
- [ ] **T6.5** Implement `CharacterManager`.
  *Done when:* T6.4 passes.
- [ ] **T6.6** Write failing `ZoneManagerTests`: `REQUEST_ENTER_MAP` bytes with `Context` values and clock timestamp; `AUTHENTICATION_ACCEPTED` fills `Context.Entry` and raises `Authenticated`; `MAP_ENTRY_REFUSAL` raises `EnterRefused`; no heartbeat before `SendMapLoaded`; after it, `HEARTBEAT_REQUEST` every 10 s with increasing timestamps; none after `Exit`; heartbeat `Tick` allocates nothing. *(FR-6, FR-14)*
- [ ] **T6.7** Implement `ZoneManager`.
  *Done when:* T6.6 passes.
- [ ] **T6.8** Write failing `FlowTransitionTests` (`FakeNetworkClient`, `FakeClock`), table-driven from plan §4.9: every valid transition with its actions, every invalid command rejected with a warning, every deadline (`ConnectTimeout` vs `ReplyTimeout`, `MapLoadSeconds` after `Authenticated`), `CloseReason` → `FailureCode` mapping, `InGame` closure → `Disconnected`, wrong credentials send exactly one `REQUEST_LOGIN`, `Reset`. *(FR-10, FR-11, FR-13)*
- [ ] **T6.9** Implement `NetworkFlow` (state, commands, manager activation, deadlines, `Tick`, `Dispose`) and construct it in `NetworkClient.Awake`. *(FR-10, FR-11, FR-20)*
  *Done when:* T6.8 passes; a manual run against local Hercules reaches `CharSelection`.

## Phase 7: Development auto-login (FR-15)

- [ ] **T7.1** Implement `DevAutoLogin` per plan §7.1: one action per state entry, `MapLoaded()` in `LoadingMap`, `maxLoginAttempts`, every field used, credentials from `EditorPrefs` or the local file. Add `NetworkClient` and `DevAutoLogin` to the bootstrap scene. *(FR-11, FR-15)*
  *Done when:* a wrong-credentials run sends exactly one login request; the scene file contains no credentials.
- [ ] **T7.2** Wrap `DevAutoLogin` in `#if UNITY_EDITOR || DEVELOPMENT_BUILD`; build a release player and confirm the type is absent. *(FR-15)*
  *Done when:* the release build compiles and does not contain the type.

## Phase 8: Allocation check (FR-6)

- [ ] **T8.1** Register the socket threads with `Profiler.BeginThreadProfiling` from Runtime (Core stays engine-free).
  *Done when:* reader and writer threads appear in the Profiler.
- [ ] **T8.2** Development build: Profiler capture of login → map and 60 s in game. Verify no per-packet GC Alloc from the network layer on the main thread; record socket-thread allocations. If they matter, add a follow-up (for example `SocketAsyncEventArgs`) to `docs/specs/BACKLOG.md`.
  *Done when:* numbers or screenshots are in the PR.

## Phase 9: Real-session verification (FR-1, FR-17 and all)

- [ ] **T9.1** Record captures (T5.7) of a full login, a char-server session, and at least 60 s of map traffic. Confirm the char server prefix is 4 bytes for PACKETVER 20220406.
  *Done when:* `.bin` files exist under `Assets/Tests/EditMode/Networking/Captures/`.
- [ ] **T9.2** Generate `*.expected.txt` from the whole-buffer run (`skip=N`, then `id,payloadLen,crc32`), review by hand, commit.
- [ ] **T9.3** Add `CaptureReplayTests` over chunk sizes {1,2,3,7,64,whole}. *(FR-1, FR-17)*
  *Done when:* all captures × chunk sizes pass, and consumed bytes equal file length minus skip with no fault.
- [ ] **T9.4** Run the full manual smoke test against local Hercules: login, select server, create character, select character, enter map, 60 s idle with heartbeat, stop the server. Verify no unhandled exceptions, no repeated error logs, and the flow ends in `Disconnected`. *(spec §5)*
- [ ] **T9.5** Run the failure checks: closed port → `Failed(ConnectFailed)` or `Failed(ConnectTimeout)` within the timeout; silent server → `Failed(ReplyTimeout)`; wrong password → one request, `Failed(LoginRefused)`. *(FR-10, FR-11)*
- [ ] **T9.6** Repeat the T8.2 capture after all phases and compare. *(FR-6)*
  *Done when:* numbers are in the PR.
- [ ] **T9.7** Verify that closing unblocks the reader and writer threads on every target platform (editor, Windows build, each mobile target in use), with no leaked threads after 20 connect/dispose cycles. *(FR-7)*
  *Done when:* thread count returns to baseline on each platform.

## Phase 10: Close-out

- [ ] **T10.1** Run the full EditMode suite in batch mode (CLI in plan §6).
  *Done when:* all tests pass in the batch output.
- [ ] **T10.2** Set `spec.md` status to `Implemented`, tick all boxes here, list deferred items in `docs/specs/BACKLOG.md`.
  *Done when:* the PR description links spec, plan and test results.
