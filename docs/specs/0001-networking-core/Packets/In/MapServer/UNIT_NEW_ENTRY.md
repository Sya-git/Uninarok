# UNIT_NEW_ENTRY

| | |
|---|---|
| **Direction** | Map server to client |
| **Hercules packet name** | struct `packet_spawn_unit` |
| **Packet ID** | `0x09FE` |
| **Declared size** | Variable (`Size = -1`); Hercules 20220406 always sends 107 bytes |
| **Status** | implemented |
| **Frequency** | hot |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no; layout from `packets_struct.h` (`packet_spawn_unit`) |
| **Allocates** | no |

**Hercules usage:** Sent by `clif_spawn_unit`.

**Description:** A unit spawns in view (just appeared: login, warp in, monster spawn).

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Header. |
| PacketLength | `ushort` | 2 bytes | Total packet length including this header. |
| objecttype | `byte` | 1 byte | Unit kind (see `UNIT_MOVE_ENTRY`). |
| AID | `uint` | 4 bytes | Account ID or object ID. |
| GID | `uint` | 4 bytes | Object ID. |
| speed | `short` | 2 bytes | Walk speed. |
| bodyState | `short` | 2 bytes | Body state. |
| healthState | `short` | 2 bytes | Health state. |
| effectState | `int` | 4 bytes | Option / effect flags. |
| job | `short` | 2 bytes | Job or sprite class. |
| head | `ushort` | 2 bytes | Head sprite. |
| weapon | `uint` | 4 bytes | Weapon sprite. |
| shield | `uint` | 4 bytes | Shield sprite. |
| accessory | `ushort` | 2 bytes | Lower head sprite. |
| accessory2 | `ushort` | 2 bytes | Top head sprite. |
| accessory3 | `ushort` | 2 bytes | Mid head sprite. |
| headpalette | `short` | 2 bytes | Hair colour. |
| bodypalette | `short` | 2 bytes | Body colour. |
| headDir | `short` | 2 bytes | Head direction. |
| robe | `ushort` | 2 bytes | Garment sprite. |
| GUID | `uint` | 4 bytes | Guild ID. |
| GEmblemVer | `short` | 2 bytes | Emblem version. |
| honor | `short` | 2 bytes | Honor. |
| virtue | `int` | 4 bytes | Virtue. |
| isPKModeON | `byte` | 1 byte | PK flag. |
| sex | `byte` | 1 byte | Sex. |
| PosDir | `UninarokPosition` | 3 bytes | Tile and facing. [`UninarokPosition`](../../../../../data/UninarokPosition.md). |
| xSize | `byte` | 1 byte | Collision width hint. |
| ySize | `byte` | 1 byte | Collision height hint. |
| clevel | `short` | 2 bytes | Base level. |
| font | `short` | 2 bytes | Chat font. |
| maxHP | `int` | 4 bytes | Max HP. |
| HP | `int` | 4 bytes | Current HP. |
| isBoss | `byte` | 1 byte | Boss flag. |
| body | `short` | 2 bytes | Body style. |
| name | fixed ASCII string | 24 bytes | Unit name. |
