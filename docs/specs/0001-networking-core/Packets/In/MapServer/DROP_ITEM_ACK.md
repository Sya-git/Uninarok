# DROP_ITEM_ACK

| | |
|---|---|
| **Direction** | Map server to client |
| **Hercules packet name** | `ZC_ITEM_THROW_ACK` |
| **Packet ID** | `0x00AF` |
| **Declared size** | 6 bytes total, including the 2-byte packet ID |
| **Status** | implemented |
| **Frequency** | warm |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-22). Capture: no; layout from `packets_struct.h`. Real server 2026-11-22 (Hercules built for 20220406): sent and received on the local server, decoded with nothing left over (SPEC-0010 T8.5). |
| **Allocates** | no |

**Hercules usage:** Sent by `clif_dropitem`, after the item is on the ground (`ITEM_FALL_ENTRY`).

**Description:** Items left the inventory: dropped. A drop the server refuses has no answer of its own.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Header, `0x00AF`. |
| InventoryIndex | `ushort` | 2 bytes | Inventory index **plus 2**. |
| Amount | `ushort` | 2 bytes | Amount removed. |
