# Packet Implementation

Normative rules for packet definitions. Linked from `plan.md` §4.6 of SPEC-0001. Target: Hercules, `PACKETVER 20220406`.

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
- Register with `[InPacket(id, size)]` or `[OutPacket(id, size)]`. `[InPacket]` structs are inputs to the source generator, which produces the router. Frame lengths come from the Hercules length table, and a test checks every declared `Size` against it.
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
- Send with `INetworkClient.Send(in packet)`. Never build `byte[]` or `Memory<byte>`, and never return a buffer from `Serialize`.
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

Values used by more than one packet (`AccountSex`, `CharInfo`, `UninarokPosition`, `UninarokMoveData`) are documented in `docs/data/` and live in `Assets/Scripts/Networking/Runtime/Data/` (`Uninarok.Networking.Data`). Packet docs link to them with the field type set to the data type name. Never redefine one inside a packet.

## Documentation upkeep

Packet code and its doc change in the same commit. Wire types in docs are C# types (`ushort`, `uint`, `short`, `byte`), not C names.

## Coverage

- [`INDEX.md`](INDEX.md): every documented packet (one doc each).
- [`CATALOG.md`](CATALOG.md): every other map-server packet Hercules defines for 20220406 (machine-generated; promote an entry to a doc when a feature needs it).

## Verified facts

All IDs and sizes in the docs were checked against the Hercules 2026.09 length table and, for map-server packets, against `packets_shuffle_main.h` (which overrides `packets.h` for this version). Points that are easy to get wrong:

- The map keepalive is `HEARTBEAT_REQUEST` (`0x0360`, 6 bytes), answered by `SERVER_HEARTBEAT_RESPONSE` (`0x007F`). `0x0187` is the **character-server** ping (`CHARACTER_SERVER_PING`); `0x0188` is an unrelated packet.
- `REQUEST_ENTER_MAP` is 23 bytes with `Reserved` at offsets 14 to 17.
- `MAP_ENTRY_ACCEPTANCE` is exactly 6 bytes (header plus ID); reading the ID leaves `Remaining == 0`.
- `MAP_ENTER_REQUEST_REFUSED` is 3 bytes.
- Login refusals come as `LOGIN_SERVER_NOTICE` (`0x0081`, 3 bytes), `LOGIN_REFUSAL` (`0x006A`, 23) or `REFUSE_LOGIN_3` (`0x0B02`, 26); all three are handled as refusals.
- `0x0368` is `REQUEST_UNIT_NAME`; its payload is a unit ID, not a packet version.
- `MAP_ENTER_REQUEST_ACCEPTED.Port` is `ushort`; `DnsHost` needs a type that holds 127 characters (`FixedString512Bytes`).
- `MAP_ENTRY_REFUSAL` (0x0074) and `CHARACTER_CREATION_FAILED` (0x006E) are 3 bytes; `CREATE_CHARACTER.JobClass` is 4 bytes (`char.cpp` reads it with `RFIFOL`); `ACCEPT_LOGIN` has a fixed 128-byte reserved region (`Skip(128)`).
