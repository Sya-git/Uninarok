# Packet Implementation

Normative rules for packet definitions. Linked from `plan.md` §4.10 of SPEC-0001. Target: Hercules, `PACKETVER 20220406`.

## Wire format

- All values are little-endian.
- Every packet starts with a `ushort` packet ID.
- `Size` is the packet's **total** length in bytes, **including the header**.
  - `Size >= 2`: fixed length. Header is the 2-byte ID.
  - `Size == -1`: variable length. Header is the 2-byte ID followed by a `ushort` **total** length (which includes these 4 header bytes).
  - `Size` of `0`, `1` or below `-1` is invalid.
- The payload handed to an incoming constructor **excludes** the header.
- A wrong `Size` desynchronizes the whole connection (the receiver consumes exactly `Size` bytes), so sizes are verified against Hercules by test (see "Verification").

## Registration

- One packet per file, one `readonly struct`.
- Register with `[InPacket(id, size)]` or `[OutPacket(id, size)]`. These are inputs to the source generator, which produces the length table, the router, and `IOutgoingPacket.WireSize` for fixed-size outgoing packets.
- Packet IDs are `ushort` values in `Uninarok.Networking.Packets.PacketIds`. The generator fails the build on duplicate IDs per direction.
- `Size` is a `const` field on the struct.

## Incoming packets

- The constructor takes a `ReadOnlySpan<byte>` payload and reads fields in wire order with `PacketReader`.
- The constructor must consume the whole payload. Use `reader.Skip(n)` for reserved or unknown regions so that `Remaining == 0` at the end. Tests enforce this.
- The span is valid only during the call. Never store it. Copy into fields.
- A malformed payload throws `PacketFormatException` (rare path). Handlers never see a half-built struct.
- Handlers receive the struct by `in` and must not retain it past the call.

## Outgoing packets

- Implement `IOutgoingPacket`:
  - `int WireSize { get; }`: exact total bytes (fixed packets: `Size`; variable packets: header + payload, computed without allocating).
  - `void Serialize(Span<byte> destination)`: writes **exactly** `WireSize` bytes with `PacketWriter`: the ID, then the length field if variable (value = `WireSize`), then the fields.
- Send with `connection.Send(in packet)`. Never build `byte[]` or `Memory<byte>`, and never return a buffer from `Serialize`.
- `Serialize` must not throw for valid field values. A text field longer than its wire size is truncated by the writer, never overflowed.

## Text fields

- **Binary regions are not text.** Reserved blocks and packed values are read as bytes. Only documented text fields use `FixedString*`.
- **Never** use `string`. Use `FixedString32Bytes` (or the smallest `FixedString*` that fits the wire maximum).
- `FixedString` capacity is 3 bytes less than the type name (for example `FixedString32Bytes` holds 29 bytes). Choose the type from the wire maximum. A test fails if a wire field is larger than its type.
- On the wire, text ends at the first `'\0'`. Bytes after it up to the field size are ignored on read and zero-filled on write.
- The wire encoding for text is Windows-1252 by default (decided in `spec.md` section 6). `FixedString*` is UTF-8, so any conversion happens only in the shared text helpers, never inside individual packets.
- Convert to `string` only in code that needs one (UI), never in a packet or a per-packet handler.

## Arrays and repeated records

- Hot packets (see `Frequency`) must not contain arrays, lists or `string`. Use fixed inline fields, or expose records one at a time.
- Cold packets (login accept, character list, map info) may allocate an array while decoding. State this in the packet doc.

## DON'T

- No `string`, `List<T>`, LINQ, closures, boxing, `ToString()`, or string interpolation inside a packet or its serialization.
- No logging inside packet code.
- No `ref struct` packets (they cannot pass through the generic dispatcher).
- No reading or writing outside the packet's own payload.

## Per-packet documentation

Each packet has a document next to its code description. Required header fields:

| Field | Values |
|---|---|
| Direction | Client to server / Server to client, and the server (login, char, map) |
| Hercules packet name | for example `ZC_ACCEPT_ENTER2` |
| Packet ID | `0x....` |
| Declared size | total bytes including header, or `-1` |
| **Status** | `documented` (spec only), `implemented` (struct and tests exist), `verified` (matched against Hercules and a real capture) |
| **Frequency** | `hot` (per frame or per entity), `warm` (occasional during play), `cold` (login, loading, rare) |
| **Verified** | date and source (Hercules file/line or capture name), or `no` |
| **Allocates** | `no`, or the reason (cold packets only) |

Followed by the field table (name, wire type, size, description) and a parsing note.

## Verification (required for every implemented packet)

1. **Size test:** the declared `Size` equals Hercules' length for PACKETVER 20220406 (`PacketSizeTests`, fixture `Fixtures/hercules-packetlen-20220406.json`).
2. **Decode test:** a known sample payload decodes with `Remaining == 0` and the expected field values (`PacketDecodeTests`).
3. **Serialize test (outgoing):** output equals the expected golden bytes and has length `WireSize`.
4. **Allocation test (hot packets):** decode and serialize allocate 0 bytes.

## Shared data types

Values used by more than one packet (`AccountSex`, `CharInfo`, `UninarokPosition`, `UninarokMoveData`) are documented in `docs/data/` and live in `Assets/Scripts/Networking/Data/` (`Uninarok.Networking.Data`). Packet docs link to them with the field type set to the data type name. Never redefine one inside a packet.

## Documentation upkeep

Packet code and its doc change in the same commit. Wire types in docs are C# types (`ushort`, `uint`, `short`, `byte`), not C names.

## Coverage

- [`INDEX.md`](INDEX.md): every documented packet (one doc each), with local code name where it differs.
- [`CATALOG.md`](CATALOG.md): every other map-server packet Hercules defines for 20220406 (machine-generated; promote an entry to a doc when a feature needs it).

## Code corrections required (task T0.7)

All IDs and sizes in the docs were checked against the Hercules 2026.09 length table and, for map-server packets, against `packets_shuffle_main.h` (which overrides `packets.h` for this version). The docs are right; these local definitions are not.

| Local code name | Doc | Correction |
|---|---|---|
| `MAP_HEARTBEAT` (0x0187) | `HEARTBEAT_REQUEST` | ID `0x0360`, 6 bytes. (0x0187 is the **character-server** ping, now `CHARACTER_SERVER_PING`.) |
| `HEARTBEAT_RESPONSE` (0x0188) | `SERVER_HEARTBEAT_RESPONSE` | ID `0x007F`; 0x0188 is another packet. |
| `REQUEST_ENTER_MAP` (19 bytes) | `REQUEST_ENTER_MAP` | 23 bytes, `Reserved` at offsets 14 to 17. |
| `MAP_ACCEPT_ENTER` | `MAP_ENTRY_ACCEPTANCE` | Exactly 6 bytes (header plus ID). Reading only the ID leaves `Remaining == 0`; no `Skip`. |
| `MAP_ENTER_REQUEST_REFUSED` (size 1) | `MAP_ENTER_REQUEST_REFUSED` | Size 3. |
| `REFUSE_LOGIN` (0x0081, 23 bytes) | `LOGIN_SERVER_NOTICE` (3 bytes), `LOGIN_REFUSAL` (0x006A, 23), `REFUSE_LOGIN_3` (0x0B02, 26) | Handle all three as refusals. |
| `SEND_PACKET_VER` (0x0368) | `REQUEST_UNIT_NAME` | Rename; the field is a unit ID. |
| `MAP_ENTER_REQUEST_ACCEPTED.Port` (`short`) | `MAP_ENTER_REQUEST_ACCEPTED` | `ushort`. |
| `MAP_ENTER_REQUEST_ACCEPTED.DnsHost` | same | Needs a type that holds 127 characters (`FixedString512Bytes`). |
| `CHARACTER_DATA`, `ACCEPT_LOGIN` | same | Known parser bugs (`ReadCharsInfo`, server-list slice); see task T0.10. |

Verified and not issues: `MAP_ENTRY_REFUSAL` (0x0074) and `CHARACTER_CREATION_FAILED` (0x006E) are 3 bytes; `CREATE_CHARACTER.JobClass` is 4 bytes (`char.cpp` reads it with `RFIFOL`); `ACCEPT_LOGIN` has a fixed 128-byte reserved region (`Skip(128)`).
