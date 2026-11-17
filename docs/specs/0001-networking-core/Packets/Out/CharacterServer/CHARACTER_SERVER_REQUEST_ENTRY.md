# CHARACTER_SERVER_REQUEST_ENTRY

| | |
|---|---|
| **Direction** | Client to character server |
| **Hercules packet name** | no named constant in the Hercules source |
| **Packet ID** | `0x0065` |
| **Declared size** | 17 bytes, including the 2-byte packet ID |
| **Status** | verified |
| **Frequency** | cold |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no Real server 2026-11-17 (Hercules built for 20220406): accepted by the server during a full login to map and 62 s in game (`Captures/`). |
| **Allocates** | no |

**Description:** Requests entry to the character server using login credentials and account attributes.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Packet identifier `0x0065`. |
| AccountId | `uint` | 4 bytes | Account identifier. |
| AuthCode | `int` | 4 bytes | Authentication code received during login. |
| UserLevel | `uint` | 4 bytes | Account user level. |
| Reserved | zero-filled bytes | 2 bytes | Written as zeros. Their meaning is unspecified. |
| Sex | `byte` | 1 byte | Account sex value. |

**Serialization:** writes 17 bytes in table order.
