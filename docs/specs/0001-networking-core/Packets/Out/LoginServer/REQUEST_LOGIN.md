# REQUEST_LOGIN

| | |
|---|---|
| **Direction** | Client to login server |
| **Hercules packet name** | `CA_LOGIN` |
| **Packet ID** | `0x0064` |
| **Declared size** | 55 bytes, including the 2-byte packet ID |
| **Status** | documented |
| **Frequency** | cold |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no |
| **Allocates** | no |

**Packet ID note:** `0x0064` (`LOGIN`)

**Hercules usage:** Registered and processed by the login server as the standard credential login request.

**Description:** Sends the account credentials, client packet version, and client type to begin login.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Packet identifier `0x0064`. |
| PacketVersion | `uint` | 4 bytes | Value of `LoginManager.PacketVersion`. |
| AccountName | fixed byte string | 24 bytes | Account name, padded with zero bytes or truncated to the fixed width. |
| AccountPassword | fixed byte string | 24 bytes | Account password, padded with zero bytes or truncated to the fixed width. |
| ClientType | `byte` | 1 byte | Client type; the source stores it as `char` and writes its byte value. Its meaning is marked TODO in the source. |

**Serialization:** writes 55 bytes in table order; unused text bytes are zero.
