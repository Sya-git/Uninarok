# TASKS-0001: Networking Core

| | |
|---|---|
| **Implements** | [plan.md](plan.md) → [spec.md](spec.md) |
| **Status** | Not started |

**How to use this file**
- Where a task names a file that already exists, update it; if it does not exist, create it. The existing network scripts are reused where they fit the plan and replaced where they do not.
- Do tasks **in order**. One task = one reviewable commit. Tick the box in the same commit.
- Each task lists the requirements it serves (`FR-n`) and a **Done when** check. Do not tick without verifying it.
- Tests come before implementation (red → green). A "failing test" task must be committed failing or skipped with `[Ignore("pending Tn")]`, then enabled by the implementation task.
- If a task needs a change not covered by the plan, stop and update `plan.md` first.

---

## Phase 0: Prepare

- [ ] **T0.1** Create branch `feature/0001-networking-core`. Run the current client against local Hercules (login → map) and note the result in the PR description.
  *Done when:* baseline behaviour is recorded.
- [ ] **T0.2** Grep for all usages of `LoginManager`, `GameConnectionManager`, `TcpConnection`, `UninarokPackageDispatcher`, `UninarokPacketsSerializer`, `IgnoreBytes`, `SerializePackages`. Paste the list into the PR and update `plan.md` §3 and §7 if paths or consumers differ. *(answers OQ-5)*
  *Done when:* list committed to the PR; plan paths confirmed or corrected.
- [ ] **T0.3** Inspect the Roslyn generator output: record the exact `GetLength` return values (`-1`, `-2`, `0`?), where `RoutePacket` and `Dispatch` are emitted, and whether payload parameters can become `ReadOnlySpan<byte>`.
  *Done when:* findings written under plan §1 / §4.3; if the generator emits `0` for variable length, a generator task is added to Phase 1.
- [x] **T0.4** Record the session-state and keepalive decisions (decided: `InGame` after `MAP_LOADED`; map 10 s, char 12 s; see `spec.md` section 6).
  *Done when:* spec section 6 updated.
- [ ] **T0.5** Survey every outgoing packet used today (`REQUEST_LOGIN`, `CHARACTER_SERVER_REQUEST_ENTRY`, `CREATE_CHARACTER`, `SELECT_CHARACTER`, `REQUEST_ENTER_MAP`, `HEARTBEAT_REQUEST`, `MAP_LOADED`, others): fixed or variable size, string/array fields, and which `Serialize` overload exists (returns `Memory<byte>` vs writes into a span). Decide generator path vs interim `stackalloc` path (plan D10). *(FR-6)*
  *Done when:* table pasted into the PR and a decision recorded in plan §4.2.
- [ ] **T0.6** Baseline GC measurement on a Development build: Profiler GC Alloc per frame (main thread) during login → map and 60 s in game; register reader/writer threads with `Profiler.BeginThreadProfiling` if they are created by then, otherwise take Memory Profiler heap totals. Also record OQ-6 (hot packet list). *(FR-6)*
  *Done when:* numbers or screenshots are in the PR.
- [ ] **T0.7** Close the rows in `Packets/Packet-Implementation.md` "Code corrections required": rename the code's `MAP_HEARTBEAT` to `HEARTBEAT_REQUEST` (0x0360, 6), `REQUEST_ENTER_MAP` to 0x0436 (23), `HEARTBEAT_RESPONSE` 0x0188 removed, `MAP_ENTER_REQUEST_ACCEPTED` size per doc, `MAP_ENTER_REQUEST_REFUSED` size 3, `MAP_ACCEPT_ENTER` (0x0283) size 6 with no `Skip`; `MAP_ENTER_REQUEST_ACCEPTED.Port` to `ushort`. IDs and sizes are already verified (OQ-8 resolved); a capture is optional. *(FR-2, FR-14, FR-18)*
  *Done when:* every row is applied in code and `PacketSizeTests` passes for it.
- [ ] **T0.9** Normalize the packet docs under `Packets/` to the template (metadata header, Status/Frequency/Verified/Allocates) and add `Packets/INDEX.md` mapping code names to doc names. *(FR-18)*
  *Done when:* every doc has the header; INDEX lists all packets.
- [ ] **T0.10** Fix the known parser bugs before the rewrite: `CHARACTER_DATA` `ReadCharsInfo`, `ACCEPT_LOGIN` server-list slice (add failing decode tests first). *(FR-18)*
  *Done when:* decode tests pass with `Remaining == 0`.
- [x] **T0.8** Decide the wire text encoding: Windows-1252 default, one constant in the text helpers (`spec.md` section 6). *(FR-6)*
  *Done when:* encoding stated in the packets reference and spec section 6.

## Phase 1: Packet framing core (FR-1, FR-2, FR-3, FR-5, FR-6, FR-17)

- [ ] **T1.1** Create asmdefs: `Uninarok.Networking.Core` (`noEngineReferences: true`), `Uninarok.Networking` (references Core), `Uninarok.Networking.Tests` (EditMode). Move nothing yet.
  *Done when:* project compiles; empty test runs in the Test Runner.
- [ ] **T1.2** Add `IPacketRouter` in Core (plan §4.1).
  *Done when:* compiles.
- [ ] **T1.3** Add test helper `RecordingRouter : IPacketRouter` (table-driven lengths, records `(id, payload copy)`).
  *Done when:* used by at least one test.
- [ ] **T1.4** Write failing `SerializerFramingTests`: fixed, variable, split at every byte offset, coalesced, back-to-back. *(FR-1)*
  *Done when:* tests exist and fail against the new API stub.
- [ ] **T1.5** Write failing `SerializerFaultTests`: unknown ID, ID 0, variable length < 4, invalid table length, cap exceeded, no routing after fault, single `FaultReason` with hex. *(FR-2)*
- [ ] **T1.6** Write failing `SerializerSkipTests`: skip split across chunks, skip of `00 00 00 00`, skip with fewer bytes available than requested. *(FR-3)*
- [ ] **T1.7** Write failing `SerializerLimitsTests` (cap 256 → 4 calls for 1000 packets; router throw leaves state consistent) and `ConcurrencyTests` (`AppendData` not blocked while router runs). *(FR-5)*
- [ ] **T1.8** Write failing `SerializerAllocTests` with `Is.Not.AllocatingGCMemory()` over 1000 fixed packets. *(FR-6)*
- [ ] **T1.9** Implement the new `UninarokPacketsSerializer` in Core: constructor, inbox/work buffers, `AppendData` (returns `false` over cap), drain with compaction and doubling growth. *(plan §4.1 D1)*
  *Done when:* buffer-management parts of T1.4/T1.7 pass.
- [ ] **T1.10** Implement `ProcessPackages` steps 1–3 including header handling, `advance-then-route`, and per-call cap. *(FR-1, FR-5)*
  *Done when:* T1.4 and the cap test pass.
- [ ] **T1.11** Implement `SkipNextBytes` and the skip step that runs before header parsing. *(FR-3)*
  *Done when:* T1.6 passes.
- [ ] **T1.12** Implement fault handling (`IsFaulted`, `FaultReason` with up to 32 hex bytes, stop parsing, return 0). *(FR-2)*
  *Done when:* T1.5 passes.
- [ ] **T1.13** Add `GeneratedPacketRouter` in `Uninarok.Networking` (try/catch with `Debug.LogException`). Use the span signature if T0.3 allows; otherwise copy with `ToArray()`, add a `// FR-6 deferral` note and get the deferral approved by a human (FR-6 is a Must).
  *Done when:* adapter compiles and routes to the generated router.
- [ ] **T1.14** (Only if T0.3 says feasible) Change the generator template and generated deserializers to take `ReadOnlySpan<byte>`; make `T1.8` pass.
  *Done when:* alloc test passes, or the human-approved deferral is recorded in the PR and the test is marked `[Ignore]` with a reason.
- [ ] **T1.15** Delete the old serializer; point `TcpConnection` at the new one only through a temporary shim if needed to compile.
  *Done when:* project compiles; all Phase 1 tests are green.
- [ ] **T1.16** Write failing `PacketReaderWriterTests`: little-endian primitives, bounds checks (`PacketFormatException` on overrun), `Skip`, `Remaining`, zero allocation. *(FR-6, FR-18)*
- [ ] **T1.17** Evolve the existing `PacketReader`/`PacketWriter`: add bounds checks and `PacketFormatException`, move them to the Core assembly (plan §4.10).
  *Done when:* T1.16 passes.
- [ ] **T1.17a** Extract shared data types into `Networking/Data` (`AccountSex`, `CharInfo`, `UninarokDirection`, `UninarokPosition` per `docs/data/*.md`) with `Read`/`Write` and tests using the documented vectors; make `AUTHENTICATION_ACCEPTED` use `UninarokPosition`. *(FR-18)*
  *Done when:* data tests pass and `AUTHENTICATION_ACCEPTED` decodes the sample in its doc.
- [ ] **T1.17b** Copy the Hercules 20220406 length table fixture (`docs/specs/0001-networking-core/fixtures/hercules-packetlen-20220406.json`, 1595 entries) to `Fixtures/hercules-packetlen-20220406.json`; make the generator emit length-only entries for IDs without a struct (FR-19). *(FR-19, FR-18)*
  *Done when:* a known-but-unhandled ID is skipped without error in a serializer test.
- [ ] **T1.18** Add `FixedString*` read/write extensions in `Uninarok.Networking` (NUL-terminated, capacity check, encoding per T0.8) with tests. *(FR-6)*
  *Done when:* tests pass, including a field longer than the type's capacity.
- [ ] **T1.19** Add the script that extracts Hercules' length table for PACKETVER 20220406 into `Fixtures/hercules-packetlen-20220406.json`, and `PacketSizeTests` comparing every `[InPacket]`/`[OutPacket]` size with it (also rejects sizes `0`, `1`, `< -1` and duplicate IDs). *(FR-18)*
  *Done when:* the test passes, or each mismatch is listed as known in the packets reference.
- [ ] **T1.20** Add `PacketDecodeTests`: each implemented incoming packet decodes a known sample payload with `Remaining == 0`; each outgoing packet's `Serialize` writes exactly `WireSize` bytes. Add samples as packets are implemented. *(FR-18)*
  *Done when:* tests pass for the packets used by this spec.

## Phase 2: Transport (FR-2, FR-6, FR-7, FR-8)

- [ ] **T2.1** Add `ConnectionRole`, `CloseReason`, `CloseInfo`, `IOutgoingPacket` (plan §4.2).
  *Done when:* compiles.
- [ ] **T2.2** Write failing `TcpConnectionTests` with a loopback `TcpListener`: remote close observed < 1 s with `RemoteClosed`; local `Dispose` is silent (`LocalDispose`, no exception logged) and both threads exit; empty send is a no-op returning `true`; send after close returns `false`; backlog overflow closes with `Error` and returns `false`. *(FR-7, FR-8)*
- [ ] **T2.3** Add `_closure` with `TryClose` (first-wins via `Interlocked.CompareExchange`), `IsClosed`, `IsConnected`, `Closure`, `ClosureReported`.
  *Done when:* closure unit tests pass.
- [ ] **T2.4** Replace the async read loop with a dedicated reader thread using blocking `Socket.Receive` (buffer allocated once). Map exits to `CloseReason`, swallow shutdown-caused exceptions (`when _closure != null`), call `Shutdown(Both)` before closing in `Dispose`. *(FR-2, FR-6, FR-7)*
  *Done when:* remote-close and dispose tests pass.
- [ ] **T2.5** Make `AppendData == false` close with `ProtocolFault` ("receive buffer overflow"); make `ProcessPackages` close with `ProtocolFault` when the serializer faults. *(FR-2)*
  *Done when:* a loopback test sending an unknown ID ends with `Closure.Reason == ProtocolFault`.
- [ ] **T2.6** Write failing `OutgoingTests`: 10 000 sends arrive complete and in order; 4 concurrent sender threads produce no partial or interleaved packets (checksum per packet); `Send<T>` of a fixed test packet allocates 0 bytes after warm-up (`Is.Not.AllocatingGCMemory()`); a throwing `Serialize` leaves no partial bytes; backlog overflow. *(FR-6, FR-8)*
- [ ] **T2.7** Implement the outbox and `_writeBuf` double buffer, the idle flag, the `AutoResetEvent` wake-up, `MaxSendBacklogBytes` (1 MiB) and `Send(ReadOnlySpan<byte>)` (plan §4.2). *(FR-8)*
  *Done when:* backlog, closed-send and ordering unit tests pass (against a fake writer if the thread is not in yet).
- [ ] **T2.8** Implement the dedicated writer thread: blocking `Socket.Send` loop, swap under lock, no lock during I/O, exits on closure. *(FR-6, FR-8)*
  *Done when:* the loopback ordering and concurrency tests from T2.6 pass end to end.
- [ ] **T2.9** Implement `Send<T>(in T) where T : struct, IOutgoingPacket` serializing directly into the outbox. *(FR-6)*
  *Done when:* the zero-allocation test from T2.6 passes with a hand-written test packet.
- [ ] **T2.10** Add the `role` and `router` parameters to `ConnectAsync`; rename `IgnoreBytes` → `SkipNextBytes` and `SerializePackages` → `ProcessPackages(int)`. Remove `SendAsync`, `SendBufferFullException`, and the per-send `Debug.LogError`; move existing call sites to `Send(ReadOnlySpan<byte>)` as an interim so the project compiles.
  *Done when:* project compiles; Phase 2 tests green.
- [ ] **T2.11** Ownership and dispose ordering: `_outSignal` is disposed by the last exiting thread (live-thread counter), never by `Dispose()`. Stress test: 100× connect/dispose with no exception.
  *Done when:* stress test passes.
- [ ] **T2.12** Write golden-byte tests: for every outgoing packet used today, the new serialization path produces the same bytes as the old `Serialize()`. *(FR-6)*
- [ ] **T2.13** Generator: emit `IOutgoingPacket` (`WireSize` + `Serialize(Span<byte>)`) on outgoing structs, as `readonly struct` where possible (`WireSize => Size` for fixed-size packets, taken from `[OutPacket]`; variable-size packets declare `WireSize` by hand). If T0.5 found this infeasible, add per-packet `stackalloc` helpers instead and record the decision in plan §4.2. *(FR-6)*
  *Done when:* T2.12 passes.
- [ ] **T2.14** Migrate the login-flow call sites (`REQUEST_LOGIN`, `CHARACTER_SERVER_REQUEST_ENTRY`, `CREATE_CHARACTER`, `SELECT_CHARACTER`) to `Send<T>` (or `stackalloc` + `Send(span)`). Remove their `async void` send wrappers and `new byte[...]`/`Memory<byte>` allocations. *(FR-6)*
  *Done when:* grep finds no `new byte[` or `Memory<byte>` in login/char send code; manual login still works.
- [ ] **T2.15** Migrate `REQUEST_ENTER_MAP` the same way. (The heartbeat is handled in T5.3.) *(FR-6)*
  *Done when:* grep clean for map send code except the heartbeat; entering the map still works.
- [ ] **T2.16** (Optional, under `UNINAROK_NET_TRACE`) Add raw-byte capture to `persistentDataPath/captures/{role}-{timestamp}.bin`.
  *Done when:* a run against Hercules produces capture files.

## Phase 3: Dispatch, subscriptions, pump, logging (FR-4, FR-5, FR-7, FR-9, FR-16)

- [ ] **T3.1** Write failing `DispatcherTests`: throwing callback does not block the next callback or packet; self-unsubscribe during dispatch is safe. *(FR-4)*
- [ ] **T3.2** Add per-callback `try/catch` to `Dispatch<T>` (or to the generator template if that is where it is emitted); add XML doc about unsubscribing other handlers. *(FR-4)*
  *Done when:* T3.1 passes.
- [ ] **T3.3** Add `Subscriptions` helper with a test: 100 add/dispose cycles leave callback counts unchanged. *(FR-9)*
- [ ] **T3.4** Add `NetLog` (`Trace` with `[Conditional("UNINAROK_NET_TRACE")]`, `ReportClosure`). Replace per-packet `Debug.Log` calls. *(FR-16)*
  *Done when:* grep shows no per-packet `Debug.Log` outside `NetLog`.
- [ ] **T3.5** Add `NetworkRuntime` with the pump and drain-before-close rule (plan §4.4), pumping `Flow.ActiveConnection` (use a minimal stub flow until Phase 4).
  *Done when:* a play-mode manual check shows packets dispatched once per frame.
- [ ] **T3.6** Write `PumpOwnershipTests` (source scan: `ProcessPackages` only from `NetworkRuntime` and tests), then remove `SerializePackages` calls from `LoginManager.Update` and `GameConnectionManager.Update`. *(FR-5)*
  *Done when:* test passes; login and map still work in manual check.
- [ ] **T3.7** Fix `GameConnectionManager.OnDisable` to `Unsubscribe`; log-level fixes (`LogError` → `Log`/`LogWarning` for informational events; fix the `IpAddress` array log). *(FR-9, FR-16)*
  *Done when:* the `Subscriptions` helper or paired calls are verified by an enable/disable test.
- [ ] **T3.8** Write `DispatchAllocTests`: `Dispatch<T>` with 3 subscribers allocates 0 bytes (`Is.Not.AllocatingGCMemory()`). *(FR-6)*
  *Done when:* test passes (fix the dispatcher if it does not).

## Phase 4: Session and flow (FR-10, FR-11, FR-12, FR-13)

- [ ] **T4.1** Add `SessionContext`, `MapEndpoint`, `FlowState`, `FailureCode`, `FlowFailure`, `NetworkTimeouts`, `IClock` (+ `FakeClock` in tests). *(plan §4.5, §4.6)*
  *Done when:* compiles.
- [ ] **T4.2** Write failing `FlowTransitionTests` (table-driven from plan §4.5): every valid transition, every invalid command rejected with warning, `Reset`. *(FR-10)*
- [ ] **T4.3** Implement `NetworkFlow` state, `StateChanged`, command validation, `Reset` with `_generation`. *(FR-10)*
  *Done when:* T4.2 passes.
- [ ] **T4.4** Write failing timeout tests with `FakeClock`: connect timeout, reply timeout in each waiting state, stale connect completion after `Reset` is disposed. *(FR-10)*
- [ ] **T4.5** Implement `ConnectAsync` (no `async void`), `Tick` deadlines, `OnConnectionClosed` (Failed vs Disconnected). *(FR-7, FR-10)*
  *Done when:* T4.4 passes.
- [ ] **T4.6** Add `ISession` and `LoginSession` (login request, `ACCEPT_LOGIN`, `LOGIN_REFUSAL` 0x006A, `REFUSE_LOGIN_3` 0x0B02, `LOGIN_SERVER_NOTICE` 0x0081; dispose login connection after accept; no retry). Tests with a fake connection. *(FR-10, FR-11, FR-12)*
- [ ] **T4.7** Add `CharSession`: `CHARACTER_SERVER_REQUEST_ENTRY` after `SkipNextBytes(4)` (called before send), `CHARACTER_DATA`, `PINCODE_LOGINSTATE` (`PinState` starts `None`), creation success/failure to `CharSelection` with stored reason, `MAP_ENTER_REQUEST_REFUSED`. Tests: PIN initial `None`; creation failure exposes reason. *(FR-3, FR-13)*
- [ ] **T4.8** Store map endpoint in `SessionContext` with `unchecked((ushort)Port)` and a test with port 40000. *(FR-12)*
- [ ] **T4.9** Wire `NetworkRuntime` to own `NetworkFlow` and the three sessions; sessions use `Subscriptions` in `Enter`/`Exit`. *(FR-9)*
  *Done when:* full login → char → `ConnectingToMap` runs against local Hercules through the new flow (manual).
- [ ] **T4.10** Test a refusal followed immediately by a server close: refusal handled first, flow ends in `Failed(LoginRefused)`. *(FR-7, FR-11)*

## Phase 5: Map session and keepalive (FR-10, FR-14)

- [ ] **T5.1** Add `MapSession` connect + `REQUEST_ENTER_MAP` with `IClock.NowMs` timestamp, using `Context`. Move to `LoadingMap`. *(FR-14)*
- [ ] **T5.2** Write failing `MapSessionTests`: `AUTHENTICATION_ACCEPTED` stores start tick, `UninarokPosition` and map view size in `Context.Entry` and keeps the flow in `LoadingMap`; `Flow.MapLoaded()` sends the map-loaded packet and moves to `InGame`; `MapLoaded()` in any other state is rejected; the `LoadingMap` deadline switches to `MapLoadSeconds` once `AUTHENTICATION_ACCEPTED` arrives. *(FR-10)*
- [ ] **T5.3** Implement the `AUTHENTICATION_ACCEPTED` handler and `Flow.MapLoaded()`. Add `MapLoadSeconds` to `NetworkTimeouts`.
  *Done when:* T5.2 passes.
- [ ] **T5.4** Write failing keepalive tests: bytes include the packet ID, timestamps in ms and increasing, none before `InGame`, none after `Exit`; sending allocates 0 bytes. *(FR-6, FR-14)*
- [ ] **T5.5** Implement the keepalive in `MapSession.Tick` using `Send<T>` (or `stackalloc` + `Send(span)` as interim), with no per-tick allocation; ready state from T0.4 and packet `HEARTBEAT_REQUEST` (T0.7). *(FR-6, FR-14)*
  *Done when:* T5.4 passes.
- [ ] **T5.5a** Implement the character-server keepalive in `CharSession.Tick`: `CHARACTER_SERVER_PING` (0x0187) every 12 s while in a char-server state, stopped on leaving it; rename the local `MAP_HEARTBEAT` type accordingly. *(FR-14)*
  *Done when:* a fake-clock test sees pings at 12 s intervals and none after the flow leaves the char server.
- [ ] **T5.6** Dispose and cancel on teardown unconditionally (not only when `IsConnected`); `NetworkRuntime.OnDestroy` bumps `_generation`. *(FR-10)*

## Phase 6: Bootstrap (FR-15)

- [ ] **T6.1** Rewrite `UninarokLoginBootstrap` per plan §4.8: event-driven, one action per state entry (including `MapLoaded()` in `LoadingMap`), `maxLoginAttempts`, serialized fields wired, `*HostOverride`/`timeoutSeconds` removed, empty `Start` removed. *(FR-11, FR-15)*
  *Done when:* wrong-credentials run sends exactly one login request.
- [ ] **T6.2** Wrap the bootstrap in `#if UNITY_EDITOR || DEVELOPMENT_BUILD`; build a release player and confirm it does not contain the type. *(FR-15)*
  *Done when:* release build compiles without the bootstrap.

## Phase 7: Real-session verification (FR-1, FR-17 and all)

- [ ] **T7.1** Record captures (T2.16) of a full login, a char-server session, and ≥ 60 s of map traffic. Confirm the char server prefix length is 4 bytes for PACKETVER 20220406.
  *Done when:* `.bin` files exist under `Assets/Tests/EditMode/Networking/Captures/`.
- [ ] **T7.2** Generate `*.expected.txt` from the whole-buffer run (`skip=N`, then `id,payloadLen,crc32`), review by hand, commit.
- [ ] **T7.3** Add `CaptureReplayTests` over chunk sizes {1,2,3,7,64,whole}. *(FR-1, FR-17)*
  *Done when:* all captures × chunk sizes pass, and consumed bytes equal file length minus skip with no fault.
- [ ] **T7.4** Run the full manual smoke test against local Hercules: login, select server, create character, select character, enter map, 60 s idle with heartbeat, stop the server. Verify no unhandled exceptions, no repeated error logs, flow ends in `Disconnected`. *(spec §5)*
- [ ] **T7.5** Run failure checks: closed port → `Failed(ConnectFailed/Timeout)` within timeout; silent server → `Failed(ReplyTimeout)`; wrong password → one request, `Failed(LoginRefused)`. *(FR-10, FR-11)*
- [ ] **T7.6** Repeat the T0.6 capture with the same scenario. Verify no per-packet GC Alloc from the network layer on the main thread, and record any remaining socket-thread allocations. If they matter, record a follow-up (for example `SocketAsyncEventArgs`). *(FR-6)*
  *Done when:* before/after numbers are in the PR.
- [ ] **T7.7** Verify that `Dispose` unblocks the reader and writer threads on every target platform (editor, Windows build, and each mobile target in use), with no leaked threads after 20 connect/dispose cycles. *(FR-7)*
  *Done when:* thread count returns to baseline on each platform.

## Phase 8: Cleanup and close-out

- [ ] **T8.1** Remove `LoginManager`, `GameConnectionManager` and any temporary shims (or add the `[Obsolete]` facade if T0.2 found external consumers). Update remaining scene/prefab references.
  *Done when:* no references remain; project compiles; scenes load without missing scripts.
- [ ] **T8.2** Run the full EditMode suite in batch mode (CLI in plan §6).
  *Done when:* all tests pass in CI/batch output.
- [ ] **T8.3** Update `spec.md` status to `Implemented`, mark all boxes here, list any deferred items (for example FR-6 span follow-up) in `docs/specs/BACKLOG.md`.
  *Done when:* PR description links spec, plan and test results.
