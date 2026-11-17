# SPEC-0001: Networking Core

| | |
|---|---|
| **Status** | Draft |
| **Created** | 2026-11-16 |
| **Owner** | _(fill in)_ |
| **Plan** | [plan.md](plan.md) |
| **Tasks** | [tasks.md](tasks.md) |

> This file defines **what** and **why**. It contains no class names, file paths or library choices. Those live in `plan.md`.

## 1. Why

The client reaches the game through three Hercules servers in sequence: login, character, map. It needs one networking layer that carries a player from credentials to a loaded map and keeps the world in sync afterwards. Everything else (entities, movement, inventory, UI) is built on it, and the packet count will grow into the hundreds, so the layer has to be right before features pile on it.

The layer must:

- **Never stall silently.** Fragmented, merged or malformed data is handled, and every failure is reported once and ends in a defined state.
- **Never hang a flow.** Login, character selection and map entry always end in a known state within bounded time, and a refused login is never retried in a loop.
- **Stay cheap at runtime.** Reading, dispatching and sending packets create no per-packet garbage on the main thread, so heavy traffic does not cause frame hitches.
- **Clean up after itself.** Event subscriptions are released with their owners.
- **Be testable.** The packet-reading core runs in automated tests, without the game, including against recorded real sessions.

## 2. Goals

1. Packet reading is correct however the network splits or merges data.
2. Any protocol or transport failure is detected, reported once, and leads to a defined, recoverable state.
3. The login → character → map flow always ends in a known state (progressing, failed, or disconnected) within bounded time.
4. The packet-reading core can be tested automatically, with recorded real sessions.

## 3. Non-goals

- Packet encryption or packet-ID obfuscation (not needed for the target version, see section 6).
- Entity, movement or inventory handling (future specs; this spec only guarantees their packets can be received and sent).
- Login, character selection or in-game UI.

## 4. Requirements

Priority: **M** = must, **S** = should.

| ID | Pri | Requirement | Acceptance (observable) |
|---|---|---|---|
| FR-1 | M | Packets are read correctly under any TCP segmentation: split, coalesced, fixed-length and variable-length. | Replaying the same recorded session split into chunks of 1, 2, 3, 7, 64 bytes and whole yields identical packet sequences. |
| FR-2 | M | Protocol faults (unknown packet ID, zero ID, invalid length, buffer limit exceeded) are detected, reported once with a diagnostic (reason + the next bytes), and the connection is closed. | Feeding each fault produces exactly one error log and a closed connection. No repeated logs. |
| FR-3 | M | Raw bytes that are not packets (the char server's 4-byte account-ID echo) are skipped reliably, even when they arrive in pieces or contain zeros. | Skipping works with 1-byte chunks and with `00 00 00 00`. |
| FR-4 | M | A failing handler never stalls the packet stream or prevents other handlers of the same packet from running. | A throwing handler does not block the next handler or the next packet. |
| FR-5 | M | All incoming packets are processed on the main thread, once per frame, in a deterministic order, with a bounded amount of work per frame. | A burst of 1000 packets is spread over several frames. Handlers only run on the main thread. |
| FR-6 | M | In steady state, framing, dispatching and sending packets do not allocate managed memory per packet on the main thread. Packets with inherently variable content (names, lists) may allocate while being decoded, but high-frequency packets (movement, spawn, ping) must not. | Allocation tests over 1000 received fixed-size packets and 1000 sent packets show zero allocated bytes on the main thread. A device profiler capture of 60 s of play shows no per-packet allocation from the network layer. |
| FR-7 | M | The game can tell, from the main thread and soon after, that a connection closed (by us, by the server, by error or by fault) and why. An intentional disconnect logs no error. Packets already received before a close are still delivered first. | Server closes the socket: closure is observed within 1 s. A login refusal sent just before the server closes is still handled. |
| FR-8 | M | Sending never blocks the main thread and does not throw for connection problems. A send that cannot be queued reports failure to the caller. If the unsent backlog exceeds its limit, the connection is closed with a reason. Empty sends are harmless. | Sending after close reports failure. Overfilling the backlog closes the connection with a reason. An empty send does nothing. Packets arrive in the order they were sent, with no partial or interleaved packets. |
| FR-9 | M | Event subscriptions are always released when their owner is disabled or destroyed. | Enabling and disabling an owner 100 times leaves the subscriber count unchanged. |
| FR-10 | M | The login → character → map flow is an explicit state machine with failure and disconnected states. Every connection attempt and every wait for a server reply has a timeout. No state can be stuck. | Connecting to a closed port, or to a silent server, reaches a failed state within the configured timeouts. |
| FR-11 | M | A refused login never triggers an unbounded automatic retry. | Wrong credentials produce exactly one login request (or at most the configured number). |
| FR-12 | M | Data handed from one server to the next (account, authorization codes, selected character, map address) lives in one shared, correctly typed session record. Ports are never negative. | A map port above 32767 connects correctly. |
| FR-13 | M | PIN state is "unknown" until the server reports it. Character creation success and failure both lead to a state from which the user can continue, and failure exposes its reason. | Creation failure returns to character selection with a reason. |
| FR-14 | M | Keepalives are sent while a connection is idle-prone: on the map server `HEARTBEAT_REQUEST` (0x0360, answered by `SERVER_HEARTBEAT_RESPONSE` 0x007F) every 10 s with a monotonic millisecond timestamp, starting when the session is in game; on the character server `CHARACTER_SERVER_PING` (0x0187) every 12 s. Both stop on disconnect. | A test server receives well-formed keepalives at the configured interval (map: increasing timestamps), and none after disconnect or after leaving that server. |
| FR-15 | S | The development bootstrap issues each action once per state entry, uses (or no longer has) its configuration fields, and does not ship in release builds. | A release build contains no bootstrap. Each action is sent once. |
| FR-16 | S | Per-packet tracing is opt-in at build time. Informational events are not logged as errors. | Without the trace option, no per-packet logging cost remains. |
| FR-17 | M | The packet-reading core can be tested automatically outside the running game, including with recorded real sessions. | The test suite runs in the editor test runner and in batch mode. |
| FR-18 | M | Every registered packet's declared size matches the server's size for PACKETVER 20220406, and every incoming packet decoder consumes exactly its payload. (A wrong size desynchronizes the whole stream.) | An automated test compares all registered sizes with the Hercules length table. Decoding a real sample of each implemented incoming packet leaves no unread bytes. |
| FR-19 | M | A packet the server may send that the client does not handle yet is skipped by its known length and never closes the connection. Only an ID absent from the server's length table is a protocol fault. | Feeding a known-but-unhandled ID (for example a 0x0ADE-style info packet) yields no error and the next packet decodes correctly. |

## 5. Success criteria

- A full local run (login, create or select a character, enter map, 60 s idle with heartbeat, server shutdown) completes with no unhandled exception and no repeated error logs.
- Every acceptance item in section 4 is covered by an automated test, except FR-15's release-build check (manual).
- Replay tests pass for recorded login, character and map sessions.

## 6. Assumptions and constraints

- Servers are Hercules; target packet version is `20220406`.
- Unity APIs may only be used from the main thread.
- Packet definitions come from the packets reference (`Packets/`), which is verified against Hercules. Where existing code disagrees with it, the reference wins (`Packets/Packet-Implementation.md`, section "Code corrections required").
- The project already has code for some of this (connection, serializer, dispatcher, source generator, login and game managers). This spec describes the target behaviour regardless; the plan says how existing code is reused or replaced.
- The packet source generator is the established way to produce packet types, router and length table.
- Packet structs follow the packets reference (one file per packet, `FixedString*` for text, no allocation).
- The session is `InGame` once the client has sent `MAP_LOADED` (0x007D) after `AUTHENTICATION_ACCEPTED`; the map keepalive starts then.
- Keepalives: map server `HEARTBEAT_REQUEST` (0x0360) every 10 s; character server `CHARACTER_SERVER_PING` (0x0187, account ID) every 12 s; none on the login server after `ACCEPT_LOGIN`. Hercules closes idle sockets after `stall_time` = 60 s (`conf/common/socket.conf`).
- Packet-ID obfuscation is not needed: `packet_obfuscation` is 2 in `conf/map/battle/client.conf`, but for PACKETVER 20220406 `packets_keys_main.h` sets all three keys to 0, so the XOR is a no-op. Revisit only if the server is compiled with `OBFUSCATIONKEY1..3`.
- A refused login ends in `Failed(LoginRefused)`. There is no automatic retry; the user retries by action.
- The scene `Assets/Scenes/Bootstrap.unity` serializes the existing managers (`LoginManager`, `GameConnectionManager`, `RagnarokLoginBootstrap`: host, port, account fields, server index, slot, `pingDelay`). Migration keeps their script files and `.meta` GUIDs and uses `FormerlySerializedAs` for renamed fields (plan section 7). No other scripts or scenes reference them.
- High-frequency (hot) packets for FR-6: `UNIT_MOVE_ENTRY`, `UNIT_NEW_ENTRY`, `UNIT_STAND_ENTRY`, `UNIT_VANISH`, `UNIT_STOP_MOVE`, `UNIT_HP`, `DAMAGE`, `ITEM_FALL_ENTRY`, `ITEM_DISAPPEAR`, `REQUEST_ACT`, `USE_SKILL_TO_ID`. Warm: local player movement, chat, status, pickups, keepalives. Hot packets must not allocate.
- Wire text goes through one shared text helper that converts between wire bytes and `FixedString*` (UTF-8). Default wire encoding is Windows-1252 (ASCII-identical for ordinary names); it is a single constant, to change if the client data is not Western (for example CP949).
- `REQUEST_ENTER_MAP` is 0x0436 (23 bytes) and the map keepalive is 0x0360: `packets_shuffle_main.h` overrides `packets.h` for this version.
