# UNIT_LOOK_CHANGE

| | |
|---|---|
| **Direction** | Map server to client |
| **Hercules packet name** | `ZC_SPRITE_CHANGE2` (struct `PACKET_ZC_SPRITE_CHANGE`) |
| **Packet ID** | `0x01D7` |
| **Declared size** | 15 bytes total, including the 2-byte packet ID |
| **Status** | documented |
| **Frequency** | warm |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406; layout read in `src/map/clif.cpp` `clif_sendlook`, `clif_changelook`; `packets_struct.h` (checked 2026-11-22, SPEC-0010 T0.2). Capture: yes (`Map-20261117-124252.bin`: weapon 1201) |
| **Allocates** | no |

**Hercules usage:** Sent by `clif_changelook` / `clif_refreshlook` when a part of a unit's look changes, and after entry for the own character. NPCs are sent again (`npc_refresh`: vanish and spawn) instead.

**Description:** One part of a unit's look changed.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Header, `0x01D7`. |
| UnitId | `uint` | 4 bytes | Object ID of the unit. |
| Type | `byte` | 1 byte | What changed, see below. |
| Value | `uint` | 4 bytes | New value. |
| Value2 | `uint` | 4 bytes | Shield for type `2`, else `0`. |

**Types** (`enum look`, `src/map/map.h`):

| Type | Name | Value |
|---:|---|---|
| 0 | base | job id |
| 1 | hair | hair style |
| 2 | weapon | `Value` weapon, `Value2` shield (a shield change is sent as this type) |
| 3 | head bottom | view id |
| 4 | head top | view id |
| 5 | head mid | view id |
| 6 | hair colour | colour |
| 7 | cloth colour | colour |
| 8 | shield | never sent (folded into 2) |
| 9 | shoes | not shown |
| 10 | body | unknown use |
| 11 | floor | unknown use |
| 12 | robe | view id |
| 13 | body2 | costume style |

Weapon and shield are the item's view id when it has one, else the **item id** (`clif_get_weapon_view`).

**Parsing:** read fields in order; ends with `Remaining == 0`. Unknown types are ignored.
