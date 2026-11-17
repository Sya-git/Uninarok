# LOGIN_REQUEST_PCBANG

| | |
|---|---|
| **Direction** | Client to login server |
| **Hercules packet name** | `CA_LOGIN_PCBANG` |
| **Packet ID** | `0x0277` |
| **Declared size** | 84 bytes, fixed |
| **Status** | documented |
| **Frequency** | cold |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no |
| **Allocates** | no |

**Hercules usage:** Registered and processed by the login server; `Ip` and `MacAddress` are currently ignored.

**Description:** Login request variant carrying account credentials and PC-bang network identifiers.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `int16` | 2 bytes | Packet identifier `0x0277`. |
| Version | `uint32` | 4 bytes | Client version. |
| Id | fixed byte array | 24 bytes | Account username. |
| Password | fixed byte array | 24 bytes | Account password. |
| ClientType | `uint8` | 1 byte | Client type. |
| Ip | fixed byte array | 16 bytes | IP address field. |
| MacAddress | fixed byte array | 13 bytes | MAC address field. |

**Hercules handling:** The login parser currently uses the version, client type, username, and password; it leaves the IP and MAC fields unused.
