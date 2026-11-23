# RESURRECTION

| | |
|---|---|
| **Direction** | Map server to client |
| **Hercules packet name** | `ZC_RESURRECTION` |
| **Packet ID** | `0x0148` |
| **Declared size** | 8 bytes total, including the 2-byte packet ID |
| **Status** | implemented |
| **Frequency** | cold |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406; layout read in `src/map/clif.cpp` `clif_resurrection` (no struct) (checked 2026-11-22, SPEC-0011 T0.1). Capture: no |
| **Allocates** | no |

**Hercules usage:** Sent by `clif_resurrection` to the area when a dead unit is revived in place: `@raise`, resurrection skills, and `pc_respawn` when the warp fails.

**Description:** A dead unit stands up where it lies.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Header, `0x0148`. |
| UnitId | `uint` | 4 bytes | Object ID of the revived unit. |
| Type | `ushort` | 2 bytes | Always `0`. |

**Parsing:** read fields in order; ends with `Remaining == 0`.
