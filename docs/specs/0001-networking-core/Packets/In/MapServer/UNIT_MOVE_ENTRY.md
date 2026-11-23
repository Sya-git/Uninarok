# UNIT_MOVE_ENTRY

| | |
|---|---|
| **Direction** | Map server to client |
| **Hercules packet name** | struct `packet_unit_walking` |
| **Packet ID** | `0x09FD` |
| **Declared size** | Variable (`Size = -1`); Hercules 20220406 always sends 114 bytes |
| **Status** | implemented |
| **Frequency** | hot |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no; layout from `packets_struct.h` (`packet_unit_walking`) |
| **Allocates** | no |

**Hercules usage:** Sent by `clif_set_unit_walking` (when a walking unit enters view, and by `clif_move` when its walk speed changed). From PACKETVER 20131223 it is also how a monster's HP reaches the client: `mob_damage` and `mob_heal` re-send it to the area after each change while `clif_show_monster_hp_bar` holds (HP below max HP, `show_monster_hp_bar` set, not a boss, Emperium or guardian); the killing blow is re-sent with `HP` 0. A heal to full HP sends nothing (SPEC-0011 H10). In all three entry packets `maxHP` and `HP` are `-1` when that rule does not hold (checked 2026-11-22, SPEC-0011 T1.1).

**Description:** A unit that is already walking comes into view. Same as `UNIT_STAND_ENTRY` but with a walk path and start time instead of a position.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Header. |
| PacketLength | `ushort` | 2 bytes | Total packet length including this header. |
| objecttype | `byte` | 1 byte | Unit kind, see the table below. |
| AID | `uint` | 4 bytes | Account ID (players) or object ID. |
| GID | `uint` | 4 bytes | Character ID of a player; `0` for every other unit. |
| speed | `short` | 2 bytes | Walk speed in ms per tile. |
| bodyState | `short` | 2 bytes | Body state flags. |
| healthState | `short` | 2 bytes | Health state flags. |
| effectState | `int` | 4 bytes | Option / effect flags. |
| job | `short` | 2 bytes | Job or sprite class ID. |
| head | `ushort` | 2 bytes | Head sprite. |
| weapon | `uint` | 4 bytes | Weapon sprite. |
| shield | `uint` | 4 bytes | Shield sprite. |
| accessory | `ushort` | 2 bytes | Lower head sprite. |
| moveStartTime | `uint` | 4 bytes | Server tick when the packet is sent: the walk goes on from the source cell at this time. |
| accessory2 | `ushort` | 2 bytes | Top head sprite. |
| accessory3 | `ushort` | 2 bytes | Mid head sprite. |
| headpalette | `short` | 2 bytes | Hair colour. |
| bodypalette | `short` | 2 bytes | Body (cloth) colour. |
| headDir | `short` | 2 bytes | Head direction: `0` straight, `1` right, `2` left. |
| robe | `ushort` | 2 bytes | Garment sprite. |
| GUID | `uint` | 4 bytes | Guild ID. |
| GEmblemVer | `short` | 2 bytes | Guild emblem version. |
| honor | `short` | 2 bytes | Honor. |
| virtue | `int` | 4 bytes | Virtue. |
| isPKModeON | `byte` | 1 byte | PK mode flag. |
| sex | `byte` | 1 byte | Sex (`0` female, `1` male). |
| MoveData | `UninarokMoveData` | 6 bytes | Source (the server cell when the packet is sent) and destination tile. [`UninarokMoveData`](../../../../../data/UninarokMoveData.md). |
| xSize | `byte` | 1 byte | Collision width hint. |
| ySize | `byte` | 1 byte | Collision height hint. |
| clevel | `short` | 2 bytes | Base level. |
| font | `short` | 2 bytes | Chat font. |
| maxHP | `int` | 4 bytes | Max HP; `-1` when not shown. |
| HP | `int` | 4 bytes | Current HP; `-1` when not shown. |
| isBoss | `byte` | 1 byte | Boss flag. |
| body | `ushort` | 2 bytes | Body style. |
| name | fixed ASCII string | 24 bytes | Unit name. |

**Unit kinds** (`objecttype`: `enum clif_unittype`, `src/map/clif.h`; `clif_bl_type`, checked 2026-11-22):

| Value | Kind |
|---:|---|
| 0 | player (`GID` not 0); with `GID` 0, a monster, pet or homunculus that looks like a player class |
| 1 | NPC (disguised players) |
| 2 | item |
| 3 | skill unit |
| 4 | unknown (chat) |
| 5 | monster |
| 6 | event NPC (every NPC at this version) |
| 7 | pet |
| 8 | homunculus |
| 9 | mercenary |
| 10 | elemental |

**State:** this packet and `UNIT_NEW_ENTRY` carry no state (the unit stands); only `UNIT_STAND_ENTRY` does.

**Weapon and shield** are the item's view id when it has one, else the item id (see [`UNIT_LOOK_CHANGE`](UNIT_LOOK_CHANGE.md)).

**Parsing:** `PacketLength` is read by the framing layer; the payload is the fixed layout above (108 bytes). `name` is a 24-byte NUL-padded string (`FixedString32Bytes`).
