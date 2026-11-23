# ITEM_FALL_ENTRY

| | |
|---|---|
| **Direction** | Map server to client |
| **Hercules packet name** | `ZC_ITEM_FALL_ENTRY` (struct `packet_dropflooritem`) |
| **Packet ID** | `0x0ADD` |
| **Declared size** | 24 bytes total, including the 2-byte packet ID |
| **Status** | implemented |
| **Frequency** | hot |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no; layout from `packets_struct.h` (`packet_dropflooritem`) |
| **Allocates** | no |

**Hercules usage:** Sent by `clif_dropflooritem` and when a unit with floor items enters view.

**Description:** A dropped item appears on the ground (monster drop, player drop, or an item already on the floor when the client enters view).

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Header. |
| ITAID | `uint` | 4 bytes | Object ID of the floor item. |
| ITID | `uint` | 4 bytes | Item ID (view ID). |
| type | `ushort` | 2 bytes | Item type. |
| IsIdentified | `byte` | 1 byte | `1` if identified. |
| xPos | `short` | 2 bytes | Tile X. |
| yPos | `short` | 2 bytes | Tile Y. |
| subX | `byte` | 1 byte | Sub-tile X (drop jitter inside the tile): 3, 6, 9 or 12. |
| subY | `byte` | 1 byte | Sub-tile Y, same values. |
| count | `short` | 2 bytes | Stack amount. |
| showdropeffect | `sbyte` | 1 byte | `1` to play the drop effect. |
| dropeffectmode | `short` | 2 bytes | Drop effect mode. |

**Position:** `xPos + subX / 12`, `yPos + subY / 12` in cells from the map's south-west corner, so `sub / 12 − 0.5` from the cell's centre (Hercules `map.cpp` `map_addflooritem` draws the values; formula from roBrowserLegacy `src/Engine/MapEngine/Item.js`; SPEC-0010 T0.3).

**Parsing:** read fields in order; ends with `Remaining == 0`. `ITAID` is the key later used by `PICKUP_ITEM` and `ITEM_DISAPPEAR`.
