# INVENTORY_LIST_EQUIP

| | |
|---|---|
| **Direction** | Map server to client |
| **Hercules packet name** | struct `packet_itemlist_equip` |
| **Packet ID** | `0x0B39` |
| **Declared size** | Variable (`Size = -1`) |
| **Status** | implemented |
| **Frequency** | cold |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-22). Capture: no; layout from `packets_struct.h`. Real server 2026-11-22 (Hercules built for 20220406): sent and received on the local server, decoded with nothing left over (SPEC-0010 T8.5). |
| **Allocates** | cold packet: may allocate (arrays) |

**Hercules usage:** As [INVENTORY_LIST_NORMAL](INVENTORY_LIST_NORMAL.md).

**Description:** The items of an inventory that do not stack.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Header, `0x0B39`. |
| PacketLength | `ushort` | 2 bytes | Total length. |
| ListType | `byte` | 1 byte | As in INVENTORY_LIST_NORMAL. |
| Items | record[] | 68 bytes each | `ushort` index (plus 2), `uint` item ID, `byte` type, `uint` location it can be worn at, `uint` location it is worn at (0: not worn), `uint[4]` cards, `int` expiry, `ushort` bind on equip, `ushort` view sprite, `byte` option count, 5 options of 5 bytes, `byte` refine, `byte` grade, `byte` flags (1 identified, 2 damaged, 4 favorite). |
