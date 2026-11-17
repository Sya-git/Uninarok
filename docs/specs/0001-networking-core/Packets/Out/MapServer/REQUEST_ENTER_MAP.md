# REQUEST_ENTER_MAP

| | |
|---|---|
| **Direction** | Client to map server |
| **Hercules packet name** | `CZ_ENTER2` |
| **Packet ID** | `0x0436` |
| **Declared size** | 23 bytes, including the 2-byte packet ID |
| **Status** | implemented |
| **Frequency** | cold |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no; handler `clif->pWantToConnection` offsets 2, 6, 10, 18, 22 at 0x0436 in `packets_shuffle_main.h` |
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

**Serialization:** offsets in the 20220406 shuffled packet: `AccountId` 2, `CharId` 6, `AuthCode` 10, `Reserved` 14 to 17 (write zeros), `Timestamp` 18, `Sex` 22; verified against `packets_shuffle_main.h`. The local `REQUEST_ENTER_MAP` writes 19 bytes and omits the reserved region; it must be changed to 23 bytes (task T0.7).
