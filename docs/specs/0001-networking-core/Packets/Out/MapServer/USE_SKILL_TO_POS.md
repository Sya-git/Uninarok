# USE_SKILL_TO_POS

| | |
|---|---|
| **Direction** | Client to map server |
| **Hercules packet name** | `CZ_USE_SKILL_TOGROUND` |
| **Packet ID** | `0x0366` |
| **Declared size** | 10 bytes total, including the 2-byte packet ID |
| **Status** | documented |
| **Frequency** | warm |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no; handler `clif->pUseSkillToPos` registered for this ID in `packets_shuffle_main.h` |
| **Allocates** | no |

**Hercules usage:** Handled by `clif_parse_UseSkillToPos` (offsets 2, 4, 6, 8).

**Description:** Casts a ground-targeted skill.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Header, `0x0366`. |
| SkillLevel | `ushort` | 2 bytes | Skill level. |
| SkillId | `ushort` | 2 bytes | Skill ID. |
| X | `ushort` | 2 bytes | Target tile X. |
| Y | `ushort` | 2 bytes | Target tile Y. |
