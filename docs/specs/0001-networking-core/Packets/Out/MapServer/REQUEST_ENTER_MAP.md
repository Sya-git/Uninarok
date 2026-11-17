# REQUEST_ENTER_MAP

| | |
|---|---|
| **Direction** | Client to map server |
| **Hercules packet name** | `CZ_ENTER2` |
| **Packet ID** | `0x0436` |
| **Declared size** | 23 bytes, including the 2-byte packet ID |
| **Status** | verified |
| **Frequency** | cold |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Real server built for 20220406 accepts this 23-byte layout (see the finding below). Capture: no; handler `clif->pWantToConnection` offsets 2, 6, 10, 18, 22 at 0x0436 in `packets_shuffle_main.h` Real server 2026-11-17 (Hercules built for 20220406): accepted by the server during a full login to map and 62 s in game (`Captures/`). |
| **Allocates** | no |

**Hercules usage:** Parsed by Hercules as the client's map-server connection request for `PACKETVER 20220406`.

**Description:** Requests entry to the map server using the account, character, authentication, and timestamp values.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Packet identifier `0x0436`. |
| AccountId | `uint` | 4 bytes | Account identifier. |
| CharId | `int` | 4 bytes | Character identifier. |
| AuthCode | `int` | 4 bytes | Authentication code. |
| Reserved | `byte[4]` | 4 bytes | Padding at offsets 14–17 in the 20220406 shuffled packet; Hercules reads the client tick beginning at offset 18. |
| Timestamp | `uint` | 4 bytes | Client tick value. |
| Sex | `byte` | 1 byte | Account sex value. |

**Real-server finding (2026-11-17), resolved:** a map server built for an older `PACKETVER` (the stale `bin/map-server.exe`, whose packet table is the Hercules block for versions up to `20210616`) reads `0x0436` as the 19-byte form (`Timestamp` at 14, `Sex` at 18). Sent the 23-byte layout below, it consumed 19 bytes and disconnected on the remaining 4. A server built for `PACKETVER 20220406` (the newer `build/map-server.exe`) accepts the 23-byte layout below: login to map took 113 ms and the session stayed in game for 62 s. The client therefore requires a server built for 20220406.

**Serialization:** offsets in the 20220406 shuffled packet: `AccountId` 2, `CharId` 6, `AuthCode` 10, `Reserved` 14 to 17 (write zeros), `Timestamp` 18, `Sex` 22; verified against `packets_shuffle_main.h`. The packet is 23 bytes; omitting the reserved region desynchronizes the server.
