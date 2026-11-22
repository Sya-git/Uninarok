# DROP_ITEM

| | |
|---|---|
| **Direction** | Client to map server |
| **Hercules packet name** | `CZ_ITEM_THROW` |
| **Packet ID** | `0x0363` |
| **Declared size** | 6 bytes total, including the 2-byte packet ID |
| **Status** | implemented |
| **Frequency** | warm |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Real server 2026-11-22 (Hercules built for 20220406): sent and received on the local server, decoded with nothing left over (SPEC-0010 T8.5). Capture: no; handler `clif->pDropItem` registered for this ID in `packets_shuffle_main.h` |
| **Allocates** | no |

**Hercules usage:** Handled by `clif_parse_DropItem` (offsets 2 and 4).

**Description:** Drops items from the inventory onto the ground.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Header, `0x0363`. |
| InventoryIndex | `ushort` | 2 bytes | Inventory index **plus 2** (the server subtracts 2). |
| Amount | `ushort` | 2 bytes | Amount to drop. |
