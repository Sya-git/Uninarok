# USE_ITEM

| | |
|---|---|
| **Direction** | Client to map server |
| **Hercules packet name** | `CZ_USE_ITEM` |
| **Packet ID** | `0x0439` |
| **Declared size** | 8 bytes total, including the 2-byte packet ID |
| **Status** | documented |
| **Frequency** | warm |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no; handler `clif->pUseItem` registered for this ID in `packets_shuffle_main.h` |
| **Allocates** | no |

**Hercules usage:** Handled by `clif_parse_UseItem` (offsets 2 and 4).

**Description:** Uses an inventory item.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Header, `0x0439`. |
| InventoryIndex | `ushort` | 2 bytes | Inventory index **plus 2**. |
| TargetId | `uint` | 4 bytes | Target object ID (the player's own ID for self use). |
