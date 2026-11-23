# PICKUP_ITEM

| | |
|---|---|
| **Direction** | Client to map server |
| **Hercules packet name** | `CZ_ITEM_PICKUP` |
| **Packet ID** | `0x0362` |
| **Declared size** | 6 bytes total, including the 2-byte packet ID |
| **Status** | documented |
| **Frequency** | warm |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no; handler `clif->pTakeItem` registered for this ID in `packets_shuffle_main.h` |
| **Allocates** | no |

**Hercules usage:** Handled by `clif_parse_TakeItem` (offset 2). The item must be within 2 cells (square) of the server's cell of the character (`pc_takeitem`). Answer on success: `ITEM_PICKUP_ACK` result 0, `UNIT_ACT` action 1 to the area, then `ITEM_DISAPPEAR`; on failure: `ITEM_PICKUP_ACK` with a result other than 0 and nothing else (SPEC-0011 T0.1).

**Description:** Picks up a floor item.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Header, `0x0362`. |
| ItemObjectId | `uint` | 4 bytes | Object ID of the floor item (`ITAID` from `ITEM_FALL_ENTRY`). |
