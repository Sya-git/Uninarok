# INVENTORY_EXPANSION_INFO

| | |
|---|---|
| **Direction** | Map server to client |
| **Hercules packet name** | `ZC_EXTEND_BODYITEM_SIZE` |
| **Packet ID** | `0x0B18` |
| **Declared size** | 4 bytes, including the 2-byte packet ID |
| **Status** | implemented |
| **Frequency** | cold |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no |
| **Allocates** | no |

**Hercules usage:** Hercules has an active sender for inventory expansion information when supported by the client version.

**Description:** Reports the inventory expansion size.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Packet identifier `0x0B18`. |
| ExpansionSize | `short` | 2 bytes | Inventory expansion size. |

**Parsing:** The constructor receives the payload after the packet ID.
