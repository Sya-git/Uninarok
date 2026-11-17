# UNIT_HP

| | |
|---|---|
| **Direction** | Map server to client |
| **Hercules packet name** | struct `packet_monster_hp` |
| **Packet ID** | `0x0977` |
| **Declared size** | 14 bytes total, including the 2-byte packet ID |
| **Status** | documented |
| **Frequency** | hot |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no; layout from `packets_struct.h` (`packet_monster_hp`) |
| **Allocates** | no |

**Hercules usage:** Sent by `clif_monster_hp_bar`.

**Description:** Current and maximum HP of a monster (HP bar).

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Header. |
| GID | `uint` | 4 bytes | Monster object ID. |
| HP | `int` | 4 bytes | Current HP. |
| MaxHP | `int` | 4 bytes | Maximum HP. |
