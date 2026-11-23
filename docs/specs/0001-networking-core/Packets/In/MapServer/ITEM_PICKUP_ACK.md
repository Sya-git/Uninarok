# ITEM_PICKUP_ACK

| | |
|---|---|
| **Direction** | Map server to client |
| **Hercules packet name** | `ZC_ITEM_PICKUP_ACK` |
| **Packet ID** | `0x0B41` |
| **Declared size** | 70 bytes total, including the 2-byte packet ID |
| **Status** | implemented |
| **Frequency** | warm |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Real server 2026-11-22 (Hercules built for 20220406): sent and received on the local server, decoded with nothing left over (SPEC-0010 T8.5). Capture: no; layout from `packets_struct.h` (`PACKET_ZC_ITEM_PICKUP_ACK`) |
| **Allocates** | no |

**Hercules usage:** Sent by `clif_additem` (also when an item is added to the inventory by other means).

**Description:** Result of picking up an item: the item as it now appears in the inventory (or a failure `result`).

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Header. |
| Index | `ushort` | 2 bytes | Inventory index, offset by 2 (the client counts from 2). |
| count | `ushort` | 2 bytes | Amount added. |
| nameid | `uint` | 4 bytes | Item ID (view ID). |
| IsIdentified | `byte` | 1 byte | `1` if identified. |
| IsDamaged | `byte` | 1 byte | `1` if broken. |
| slot | `uint[4]` | 16 bytes | Card slots (item IDs). |
| location | `uint` | 4 bytes | Equip location bitmask. |
| type | `byte` | 1 byte | Item type. |
| result | `byte` | 1 byte | `0` success; any other value is a failure (see below). |
| HireExpireDate | `int` | 4 bytes | Expiry (unix time), `0` if none. |
| bindOnEquipType | `ushort` | 2 bytes | Bind-on-equip type. |
| option_data | `ItemOption[5]` | 25 bytes | 5 options, each `short index`, `short value`, `byte param` (5 bytes). |
| favorite | `byte` | 1 byte | `1` if marked favorite. |
| look | `ushort` | 2 bytes | Look / view sprite ID. |
| refiningLevel | `byte` | 1 byte | Refine level. |
| grade | `byte` | 1 byte | Item grade. |

**Results** (`clif_additem`, checked 2026-11-22, SPEC-0011 T0.1): on a failure every field is 0 except `Index` (2) and `result`. `6`: refused before the item is given (`clif_parse_TakeItem`: dead, item gone or on another map, hiding and similar states, cannot act, too far, no permission, loot reserved for someone else). From `pc_additem` (through `party_share_loot`): `1` invalid item or amount, `2` overweight, `4` no free inventory slot, `5` maximum amount, `7` stack limit or bound item.

**Parsing:** all fields are fixed; `ItemOption` is a 5-byte record. Allocation-free if read into a fixed struct.
