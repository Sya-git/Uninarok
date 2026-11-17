# OVERWEIGHT_PERCENT_INFO

| | |
|---|---|
| **Direction** | Map server to client |
| **Hercules packet name** | `ZC_OVERWEIGHT_PERCENT` |
| **Packet ID** | `0x0ADE` |
| **Declared size** | 6 bytes, including the 2-byte packet ID |
| **Status** | implemented |
| **Frequency** | warm |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no |
| **Allocates** | no |

**Packet ID note:** `0x0ADE` (`OVERWEIGHT_PERCENT` in `PacketIds`)

**Hercules usage:** Hercules has an active sender for the player's current overweight percentage when supported by the client version.

**Description:** Reports the current overweight percentage.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Packet identifier `0x0ADE`. |
| Percentage | `uint` | 4 bytes | Overweight percentage value. |

**Parsing:** The constructor receives the payload after the packet ID.
