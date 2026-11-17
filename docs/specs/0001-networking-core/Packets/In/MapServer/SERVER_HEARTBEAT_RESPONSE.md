# SERVER_HEARTBEAT_RESPONSE

| | |
|---|---|
| **Direction** | Map server to client |
| **Hercules packet name** | `ZC_NOTIFY_TIME` |
| **Packet ID** | `0x007F` |
| **Declared size** | 6 bytes, including the 2-byte packet ID |
| **Status** | implemented |
| **Frequency** | warm |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no; `clif_notify_time`, `clif.cpp` (4-byte tick at offset 2) |
| **Allocates** | no |

**Hercules usage:** Sent by Hercules in response to a client time request.

**Description:** Returns the map server's current tick value.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Packet identifier `0x007F`. |
| ServerTimestamp | `uint` | 4 bytes | Current server tick returned in response to a time request. |

**Parsing:** No matching local incoming packet parser is currently implemented; local `HEARTBEAT_RESPONSE` uses ID `0x0188`, which Hercules uses for `ZC_ACK_ITEMREFINING`.
