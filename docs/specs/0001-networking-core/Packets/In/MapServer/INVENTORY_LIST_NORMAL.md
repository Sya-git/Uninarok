# INVENTORY_LIST_NORMAL

| | |
|---|---|
| **Direction** | Map server to client |
| **Hercules packet name** | struct `packet_itemlist_normal` |
| **Packet ID** | `0x0B09` |
| **Declared size** | Variable (`Size = -1`) |
| **Status** | implemented |
| **Frequency** | cold |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-22). Capture: no; layout from `packets_struct.h`. Real server 2026-11-22 (Hercules built for 20220406): sent and received on the local server, decoded with nothing left over (SPEC-0010 T8.5). |
| **Allocates** | cold packet: may allocate (arrays) |

**Hercules usage:** Sent by `clif_inventoryItems` at the first map entry, between `0x0B08` (start) and `0x0B0B` (end), when there is such an item; also for storage and cart.

**Description:** The stackable items of an inventory.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Header, `0x0B09`. |
| PacketLength | `ushort` | 2 bytes | Total length. |
| ListType | `byte` | 1 byte | `0` inventory, `1` cart, `2` storage, `3` guild storage. |
| Items | record[] | 34 bytes each | `ushort` index (plus 2), `uint` item ID (view ID), `byte` type, `ushort` amount, `uint` location, `uint[4]` cards, `int` expiry, `byte` flags (1 identified, 2 favorite). |
