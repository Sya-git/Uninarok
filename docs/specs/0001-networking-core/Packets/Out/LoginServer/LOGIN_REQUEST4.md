# LOGIN_REQUEST4

| | |
|---|---|
| **Direction** | Client to login server |
| **Hercules packet name** | `CA_LOGIN4` |
| **Packet ID** | `0x027C` |
| **Declared size** | 60 bytes, fixed |
| **Status** | documented |
| **Frequency** | cold |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no |
| **Allocates** | no |

**Hercules usage:** Registered and processed by the login server; `MacAddress` is currently ignored.

**Description:** MD5-based login request that also carries a client MAC address.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `int16` | 2 bytes | Packet identifier `0x027C`. |
| Version | `uint32` | 4 bytes | Client version. |
| Id | fixed byte array | 24 bytes | Account username. |
| PasswordMd5 | `byte[16]` | 16 bytes | MD5 password digest. |
| ClientType | `uint8` | 1 byte | Client type. |
| MacAddress | fixed byte array | 13 bytes | MAC address field. Hercules' login parser currently leaves this field unused. |
