# CONNECTION_KEEPALIVE

| | |
|---|---|
| **Direction** | Client to login server |
| **Hercules packet name** | `CA_CONNECT_INFO_CHANGED` |
| **Packet ID** | `0x0200` |
| **Declared size** | 26 bytes, fixed |
| **Status** | documented |
| **Frequency** | warm |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no |
| **Allocates** | no |

**Hercules usage:** Registered and accepted as a keep-alive signal; the handler does not process its fields.

**Description:** Client keep-alive packet used to indicate that the connection remains active.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `int16` | 2 bytes | Packet identifier `0x0200`. |
| Id | fixed byte array | 24 bytes | Account username associated with the connection. |

**Hercules handling:** The login handler accepts this packet without further field processing.
