# ITEM_ENTRY

| | |
|---|---|
| **Direction** | Map server to client |
| **Hercules packet name** | `ZC_ITEM_ENTRY` |
| **Packet ID** | `0x009D` |
| **Declared size** | 19 bytes total, including the 2-byte packet ID |
| **Status** | documented |
| **Frequency** | hot |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406; layout read in `packets_struct.h` `PACKET_ZC_ITEM_ENTRY`, `src/map/clif.cpp` `clif_getareachar_item` (checked 2026-11-22, SPEC-0010 T0.2). Capture: no |
| **Allocates** | no |

**Hercules usage:** Sent by `clif_getareachar_item` when an item already on the ground enters the player's view.

**Description:** An item lying on the ground.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Header, `0x009D`. |
| ItemObjectId | `uint` | 4 bytes | Object ID of the item on the ground. |
| ItemId | `uint` | 4 bytes | Item id (its view item when it has one). |
| Identified | `byte` | 1 byte | `1` if identified. |
| X | `ushort` | 2 bytes | Cell X. |
| Y | `ushort` | 2 bytes | Cell Y. |
| Amount | `ushort` | 2 bytes | Stack amount. |
| SubX | `byte` | 1 byte | Sub-cell X, see [`ITEM_FALL_ENTRY`](ITEM_FALL_ENTRY.md). |
| SubY | `byte` | 1 byte | Sub-cell Y. |

**Parsing:** read fields in order; ends with `Remaining == 0`. `Amount` comes **before** the sub-cell here, after it in `ITEM_FALL_ENTRY`.
