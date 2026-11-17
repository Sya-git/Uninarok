# HASH_CHALLENGE_REQUEST

| | |
|---|---|
| **Direction** | Client to login server |
| **Hercules packet name** | `CA_REQ_HASH` |
| **Packet ID** | `0x01DB` |
| **Declared size** | 2 bytes, fixed |
| **Status** | documented |
| **Frequency** | cold |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no |
| **Allocates** | no |

**Hercules usage:** Registered and processed; the login server generates and sends a hash challenge.

**Description:** Requests the login server's hash challenge.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `int16` | 2 bytes | Packet identifier `0x01DB`. |

**Hercules handling:** The login server generates a 12-to-15-byte challenge and replies with `AC_ACK_HASH`.
