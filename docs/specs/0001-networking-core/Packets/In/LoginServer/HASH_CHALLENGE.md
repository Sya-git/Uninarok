# HASH_CHALLENGE

| | |
|---|---|
| **Direction** | Login server to client |
| **Hercules packet name** | `AC_ACK_HASH` |
| **Packet ID** | `0x01DC` |
| **Declared size** | Variable; minimum header is 4 bytes |
| **Status** | documented |
| **Frequency** | cold |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no |
| **Allocates** | cold packet: may allocate (arrays); see parsing note |

**Hercules usage:** Sent by the login server in response to `CA_REQ_HASH`.

**Description:** Returns the login server's hash challenge in response to `CA_REQ_HASH`.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `int16` | 2 bytes | Packet identifier `0x01DC`. |
| PacketLength | `int16` | 2 bytes | Total packet length, including this header. |
| Secret | `byte[]` | Variable | Challenge bytes. Hercules currently generates 12 to 15 bytes. |
