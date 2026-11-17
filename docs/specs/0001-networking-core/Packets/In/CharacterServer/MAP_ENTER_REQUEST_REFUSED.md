# MAP_ENTER_REQUEST_REFUSED

| | |
|---|---|
| **Direction** | Character server to client |
| **Hercules packet name** | no named constant (`chr->auth_error`, `char.cpp`) |
| **Packet ID** | `0x006C` |
| **Declared size** | 3 bytes total, including the 2-byte packet ID |
| **Status** | documented |
| **Frequency** | cold |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no; layout from `char_auth_error` |
| **Allocates** | no |

**Hercules usage:** Sent when the character server rejects the client's entry request (`CH_ENTER`): unknown account, bad auth codes, or a duplicate connection.

**Description:** Entry to the character server was refused. In SPEC-0001 this moves the flow to `Failed(CharServerRefused)`.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Header, `0x006C`. |
| ErrorCode | `byte` | 1 byte | Hercules always sends `0` ("rejected from server"). |

**Parsing:** the old local definition declared a fixed size of 1, which is invalid; the correct total is 3 (header plus 1 byte).
