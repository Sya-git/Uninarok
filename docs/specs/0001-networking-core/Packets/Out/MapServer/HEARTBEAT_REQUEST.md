# HEARTBEAT_REQUEST

| | |
|---|---|
| **Direction** | Client to map server |
| **Hercules packet name** | `CZ_REQUEST_TIME2` (`CZ_REQUEST_TIME` in older packet versions) |
| **Packet ID** | `0x0360` |
| **Declared size** | 6 bytes, including the 2-byte packet ID |
| **Status** | documented |
| **Frequency** | warm |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no; handler `clif->pTickSend` (offset 2) registered at 0x0360 in `packets_shuffle_main.h` |
| **Allocates** | no |

**Packet ID note:** `0x0360` (20220406); legacy `0x007E`

**Hercules usage:** Hercules handles this request as `0x0360` for PACKETVER 20220406; older versions use `0x007E`.

**Description:** Requests the server tick value and includes the client's tick.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Packet identifier `0x0360` for PACKETVER 20220406, or legacy `0x007E`. |
| ClientTimestamp | `uint` | 4 bytes | Client tick value. |

**Serialization:** Local `MAP_HEARTBEAT` has the timestamp field but uses `0x0187`, which Hercules does not handle as a MapServer packet. The active 20220406 request is `0x0360` (verified in `packets_shuffle_main.h`). Rename the local type to `HEARTBEAT_REQUEST`. Reply: `SERVER_HEARTBEAT_RESPONSE` (0x007F).
