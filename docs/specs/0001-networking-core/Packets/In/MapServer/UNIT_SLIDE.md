# UNIT_SLIDE

| | |
|---|---|
| **Direction** | Map server to client |
| **Hercules packet name** | `ZC_HIGHJUMP` |
| **Packet ID** | `0x01FF` |
| **Declared size** | 10 bytes total, including the 2-byte packet ID |
| **Status** | documented |
| **Frequency** | warm |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406; layout read in `src/map/clif.cpp` `clif_slide` (checked 2026-11-22, SPEC-0010 T0.2). Capture: no |
| **Allocates** | no |

**Hercules usage:** Sent by `clif_slide` (knock-back through `clif_blown`, high jump, body relocation) to the area, the unit included.

**Description:** Moves a unit to a cell at once, without walking, even onto a cell that is not walkable. A sitting unit stays sitting.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Header, `0x01FF`. |
| UnitId | `uint` | 4 bytes | Object ID of the unit. |
| X | `ushort` | 2 bytes | Cell X. |
| Y | `ushort` | 2 bytes | Cell Y. |

**Parsing:** read fields in order; ends with `Remaining == 0`.
