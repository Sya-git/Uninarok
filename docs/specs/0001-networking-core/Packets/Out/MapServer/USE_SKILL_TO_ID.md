# USE_SKILL_TO_ID

| | |
|---|---|
| **Direction** | Client to map server |
| **Hercules packet name** | `CZ_USE_SKILL` |
| **Packet ID** | `0x0438` |
| **Declared size** | 10 bytes total, including the 2-byte packet ID |
| **Status** | documented |
| **Frequency** | hot |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no; handler `clif->pUseSkillToId` registered for this ID in `packets_shuffle_main.h` |
| **Allocates** | no |

**Hercules usage:** Handled by `clif_parse_UseSkillToId` (offsets 2, 4, 6).

**Description:** Casts a skill on a unit.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Header, `0x0438`. |
| SkillLevel | `ushort` | 2 bytes | Skill level used. |
| SkillId | `ushort` | 2 bytes | Skill ID. |
| TargetId | `uint` | 4 bytes | Target object ID. |
