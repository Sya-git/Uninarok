# LOGIN_REFUSAL

| | |
|---|---|
| **Direction** | Login server to client |
| **Hercules packet name** | `AC_REFUSE_LOGIN` |
| **Packet ID** | `0x006A` |
| **Declared size** | 23 bytes, fixed |
| **Status** | documented |
| **Frequency** | cold |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no |
| **Allocates** | no |

**Hercules usage:** Sent by the login server's login-error path for the legacy refusal response.

**Description:** Refuses a login request and optionally provides a block expiration date.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `int16` | 2 bytes | Packet identifier `0x006A`. |
| ErrorCode | `uint8` | 1 byte | Login refusal reason. |
| BlockDate | fixed byte array | 20 bytes | Ban expiration date when the error code indicates a ban; otherwise Hercules zero-fills this field. |

This is the legacy `AC_REFUSE_LOGIN` structure emitted by Hercules' login-error path. It is not `0x0081`: that ID is `LOGIN_SERVER_NOTICE` for version `20220406`.
