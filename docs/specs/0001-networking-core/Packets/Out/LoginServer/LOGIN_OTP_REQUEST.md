# LOGIN_OTP_REQUEST

| | |
|---|---|
| **Direction** | Client to login server |
| **Hercules packet name** | `CA_LOGIN_OTP` |
| **Packet ID** | `0x0ACF` |
| **Declared size** | 68 bytes, fixed |
| **Status** | documented |
| **Frequency** | cold |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no |
| **Allocates** | no |

**Hercules usage:** Registered; the handler starts the OTP login flow but does not parse these fields in `lclif.cpp`.

**Description:** Submits the client-side OTP login form, including an encrypted password and client flags.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `int16` | 2 bytes | Packet identifier `0x0ACF`. |
| DevFlags | `uint32` | 4 bytes | Device/developer flags; included for packet versions from 20171113. |
| Login | fixed byte array | 25 bytes | Username. |
| Password | fixed byte array | 32 bytes | Password encrypted with Rijndael. |
| FlagsStr | fixed byte array | 5 bytes | Flags field; Hercules marks its meaning unknown and notes the common value `G000`. |

**Hercules handling:** The login handler invokes its OTP login routine but does not parse these packet fields in `lclif.cpp`.
