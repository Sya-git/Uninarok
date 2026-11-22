# UNIT_DIRECTION

| | |
|---|---|
| **Direction** | Map server to client |
| **Hercules packet name** | `ZC_CHANGE_DIRECTION` |
| **Packet ID** | `0x009C` |
| **Declared size** | 9 bytes total, including the 2-byte packet ID |
| **Status** | documented |
| **Frequency** | warm |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406; layout read in `src/map/clif.cpp` `clif_changed_dir` (checked 2026-11-22, SPEC-0010 T0.2). Capture: no |
| **Allocates** | no |

**Hercules usage:** Sent by `clif_changed_dir`. After a player's own `CHANGE_DIRECTION` it goes to the area **without** the player (`clif_parse_ChangeDir`, `AREA_WOS`); turns made by the server (`unit_set_dir`) go to the area.

**Description:** A unit turned its body or head.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Header, `0x009C`. |
| UnitId | `uint` | 4 bytes | Object ID of the unit. |
| HeadDirection | `ushort` | 2 bytes | `0` straight, `1` right (clockwise), `2` left (counter-clockwise); `0` for units that are not players. |
| Direction | `byte` | 1 byte | Body direction, 0 to 7 (`UninarokDirection`). |

**Parsing:** read fields in order; ends with `Remaining == 0`.
