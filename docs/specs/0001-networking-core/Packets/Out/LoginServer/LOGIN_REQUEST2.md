# LOGIN_REQUEST2

| | |
|---|---|
| **Direction** | Client to login server |
| **Hercules packet name** | `CA_LOGIN2` |
| **Packet ID** | `0x01DD` |
| **Declared size** | 47 bytes, fixed |
| **Status** | documented |
| **Frequency** | cold |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no |
| **Allocates** | no |

**Hercules usage:** Registered and processed by the login server; credentials are used for authentication.

**Description:** Login request using a 16-byte MD5 password digest instead of a plaintext password field.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `int16` | 2 bytes | Packet identifier `0x01DD`. |
| Version | `uint32` | 4 bytes | Client version. |
| Id | fixed byte array | 24 bytes | Account username. |
| PasswordMd5 | `byte[16]` | 16 bytes | MD5 password digest. |
| ClientType | `uint8` | 1 byte | Client type. |

**Hercules handling:** The login server reads these fields and treats `PasswordMd5` as a hexadecimal-encoded password hash.
