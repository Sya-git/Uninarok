# REQUEST_UNIT_NAME

| | |
|---|---|
| **Direction** | Client to map server |
| **Hercules packet name** | `CZ_REQNAME2` |
| **Packet ID** | `0x0368` |
| **Declared size** | 6 bytes, including the 2-byte packet ID |
| **Status** | documented |
| **Frequency** | warm |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no; handler `clif->pGetCharNameRequest` (offset 2) at 0x0368 in `packets_shuffle_main.h` |
| **Allocates** | no |

**Hercules usage:** Parsed by Hercules as a request for a visible unit's name; packet ID `0x0368` is registered for this handler for PACKETVER 20220406.

**Description:** Requests the name associated with a unit object ID.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Packet identifier `0x0368`. |
| UnitId | `uint` | 4 bytes | Object ID of the visible unit whose name is requested. |

**Serialization:** Hercules interprets the 4-byte payload as `UnitId`, not a packet version.
