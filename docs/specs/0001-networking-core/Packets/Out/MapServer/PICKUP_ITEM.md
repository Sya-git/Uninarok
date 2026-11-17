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

**Hercules usage:** Handled by `clif_parse_TakeItem` (offset 2). Answer: `ITEM_PICKUP_ACK`, then `ITEM_DISAPPEAR`.

**Description:** Picks up a floor item.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Header, `0x0362`. |
| ItemObjectId | `uint` | 4 bytes | Object ID of the floor item (`ITAID` from `ITEM_FALL_ENTRY`). |
