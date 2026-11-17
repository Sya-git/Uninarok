# LOGIN_REQUEST3

| | |
|---|---|
| **Direction** | Client to login server |
| **Hercules packet name** | `CA_LOGIN3` |
| **Packet ID** | `0x01FA` |
| **Declared size** | 48 bytes, fixed |
| **Status** | documented |
| **Frequency** | cold |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no |
| **Allocates** | no |

**Hercules usage:** Registered and processed by the login server; `ClientInfo` is currently ignored.

**Description:** MD5-based login request with an additional client-info byte.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `int16` | 2 bytes | Packet identifier `0x01FA`. |
| Version | `uint32` | 4 bytes | Client version. |
| Id | fixed byte array | 24 bytes | Account username. |
| PasswordMd5 | `byte[16]` | 16 bytes | MD5 password digest. |
| ClientType | `uint8` | 1 byte | Client type. |
| ClientInfo | `uint8` | 1 byte | Client-info index; Hercules documents it as an index in the client-info file, with 10 added when the command line contains `pc`. The login parser currently leaves it unused. |
