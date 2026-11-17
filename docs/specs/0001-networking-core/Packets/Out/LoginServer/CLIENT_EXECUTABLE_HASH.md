# CLIENT_EXECUTABLE_HASH

| | |
|---|---|
| **Direction** | Client to login server |
| **Hercules packet name** | `CA_EXE_HASHCHECK` |
| **Packet ID** | `0x0204` |
| **Declared size** | 18 bytes, fixed |
| **Status** | documented |
| **Frequency** | cold |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no |
| **Allocates** | no |

**Hercules usage:** Registered and processed; the handler copies the hash into the login session.

**Description:** Sends the client's 16-byte executable MD5 hash.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `int16` | 2 bytes | Packet identifier `0x0204`. |
| HashValue | `byte[16]` | 16 bytes | Client executable MD5 hash. |

**Hercules handling:** The login server copies the hash into the session's client-hash field.
