# UNIT_STOP_MOVE

| | |
|---|---|
| **Direction** | Map server to client |
| **Hercules packet name** | `ZC_STOPMOVE` |
| **Packet ID** | `0x0088` |
| **Declared size** | 10 bytes total, including the 2-byte packet ID |
| **Status** | documented |
| **Frequency** | hot |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no |
| **Allocates** | no |

**Hercules usage:** Sent by `clif_fixpos`.

**Description:** Stops a unit and places it on the given tile (also used to correct position, for example after a refused move).

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Header, `0x0088`. |
| UnitId | `uint` | 4 bytes | Object ID of the unit. |
| X | `ushort` | 2 bytes | Tile X to snap to. |
| Y | `ushort` | 2 bytes | Tile Y to snap to. |
