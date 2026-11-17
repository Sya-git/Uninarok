# CHARACTER_SERVER_REQUEST_ENTRY

| | |
|---|---|
| **Direction** | Client to character server |
| **Hercules packet name** | no named constant in the Hercules source |
| **Packet ID** | `0x0065` |
| **Declared size** | 17 bytes, including the 2-byte packet ID |
| **Status** | implemented |
| **Frequency** | cold |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no |
| **Allocates** | current code: yes (new buffer per call); target: no (`IOutgoingPacket`, plan §4.2) |

**Description:** Requests entry to the character server using login credentials and account attributes.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Packet identifier `0x0065`. |
| AccountId | `uint` | 4 bytes | Account identifier. |
| AuthCode | `int` | 4 bytes | Authentication code received during login. |
| UserLevel | `uint` | 4 bytes | Account user level. |
| Reserved | zero-filled bytes | 2 bytes | Skipped by the writer; the newly allocated buffer leaves these bytes zero. Their meaning is unspecified. |
| Sex | `byte` | 1 byte | Account sex value. |

**Serialization:** `Serialize()` allocates a 17-byte buffer and writes fields in table order.
