# UNIT_MOVE

| | |
|---|---|
| **Direction** | Map server to client |
| **Hercules packet name** | `ZC_NOTIFY_MOVE` |
| **Packet ID** | `0x0086` |
| **Declared size** | 16 bytes total, including the 2-byte packet ID |
| **Status** | implemented |
| **Frequency** | hot (every walk of every unit in view) |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406; layout read in `src/map/clif.cpp` `clif_move` (checked 2026-11-22, SPEC-0010 T0.2). Capture: no |
| **Allocates** | no |

**Hercules usage:** Sent by `clif_move` to the area without the unit itself, each time a unit the area already knows starts a walk or changes its path.

**Description:** A known unit walks. The unit itself gets `PLAYER_MOVE`; a unit entering view mid-walk comes with `UNIT_MOVE_ENTRY`, which Hercules also sends instead of this packet when the walk speed changed.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Header, `0x0086`. |
| UnitId | `uint` | 4 bytes | Object ID of the unit (account ID for players). |
| MoveData | `UninarokMoveData` | 6 bytes | Source cell (the unit's server cell when the packet is sent) and destination; sub-cell always 8/8. [`UninarokMoveData`](../../../../../data/UninarokMoveData.md). |
| StartTick | `uint` | 4 bytes | Server tick when the packet is sent: the start of the walk. |

**Parsing:** `UnitId`, `UninarokMoveData.Read`, `StartTick`; ends with `Remaining == 0`. The path between the two cells is not sent: the client searches it (PLAN-0010 4.4).
