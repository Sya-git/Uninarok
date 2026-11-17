# OTP_CODE_SUBMISSION

| | |
|---|---|
| **Direction** | Client to login server |
| **Hercules packet name** | `CA_OTP_CODE` |
| **Packet ID** | `0x0AD0` |
| **Declared size** | 11 bytes, fixed |
| **Status** | documented |
| **Frequency** | cold |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no |
| **Allocates** | no |

**Hercules usage:** Registered, but the login-side handler currently has a TODO and does not parse the field.

**Description:** Carries a fixed-width OTP code to the login server.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `int16` | 2 bytes | Packet identifier `0x0AD0`. |
| Code | fixed byte array | 9 bytes | OTP code field. |

**Hercules handling:** The login-side handler currently has a TODO for parsing the packet data.
