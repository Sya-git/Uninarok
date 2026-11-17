# LOGIN_SERVER_NOTICE

| | |
|---|---|
| **Direction** | Login server to client |
| **Hercules packet name** | `SC_NOTIFY_BAN` |
| **Packet ID** | `0x0081` |
| **Declared size** | 3 bytes, fixed |
| **Status** | implemented |
| **Frequency** | cold |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no |
| **Allocates** | no |

**Hercules usage:** Sent by the login server when reporting connection errors/status.

**Description:** Notifies the client of a login-server connection error or status.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `int16` | 2 bytes | Packet identifier `0x0081`. |
| ErrorCode | `uint8` | 1 byte | Connection/status reason; Hercules uses `1` for server closed and `8` when the server still recognizes the previous login. |

**Compatibility note:** Hercules identifies `0x0081` as `SC_NOTIFY_BAN` and defines a 3-byte packet. It is not the 23-byte `LOGIN_REFUSAL` (`0x006A`); the client treats both, and `REFUSE_LOGIN_3`, as login refusals.
