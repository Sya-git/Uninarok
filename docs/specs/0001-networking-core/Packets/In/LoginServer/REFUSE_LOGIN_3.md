# REFUSE_LOGIN_3

| | |
|---|---|
| **Direction** | Login server to client |
| **Hercules packet name** | `AC_REFUSE_LOGIN_R3` (uses the `PACKET_AC_REFUSE_LOGIN_R2` layout) |
| **Packet ID** | `0x0B02` |
| **Declared size** | 26 bytes, including the 2-byte packet ID |
| **Status** | implemented |
| **Frequency** | cold |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no |
| **Allocates** | no |

**Hercules usage:** Sent by the login server for authentication failures at this packet version.

**Description:** Login refusal packet containing an error code and a fixed-width block-date value. The exact error-code meanings are not yet documented.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Packet identifier `0x0B02`. |
| ErrorCode | `int` | 4 bytes | Login refusal/error code. |
| BlockDate | fixed byte string | 20 bytes | Block date or related message value; zero bytes are omitted when converted to the in-memory string. |

**Parsing:** The constructor receives the payload after the packet ID and reads the fields in table order.
