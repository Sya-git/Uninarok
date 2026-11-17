# SKILL_UNIT_ENTRY

| | |
|---|---|
| **Direction** | Map server to client |
| **Hercules packet name** | struct `packet_skill_entry` |
| **Packet ID** | `0x09CA` |
| **Declared size** | Variable (`Size = -1`); Hercules 20220406 always sends 23 bytes |
| **Status** | documented |
| **Frequency** | warm |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no; layout from `packets_struct.h` (`packet_skill_entry`) |
| **Allocates** | no |

**Hercules usage:** Sent by `clif_skill_setunit`.

**Description:** A skill ground unit (trap, area skill) appears.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Header. |
| PacketLength | `ushort` | 2 bytes | Total packet length including this header. |
| AID | `uint` | 4 bytes | Skill unit object ID. |
| creatorAID | `uint` | 4 bytes | Creator ID. |
| xPos | `short` | 2 bytes | Tile X. |
| yPos | `short` | 2 bytes | Tile Y. |
| job | `int` | 4 bytes | Skill unit type ID. |
| RadiusRange | `sbyte` | 1 byte | Radius in tiles. |
| isVisible | `byte` | 1 byte | `1` visible. |
| level | `byte` | 1 byte | Skill level. |
