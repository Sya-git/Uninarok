# ATTACK_RANGE

| | |
|---|---|
| **Direction** | Map server to client |
| **Hercules packet name** | `ZC_ATTACK_RANGE` |
| **Packet ID** | `0x013A` |
| **Declared size** | 4 bytes total, including the 2-byte packet ID |
| **Status** | verified |
| **Frequency** | cold |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406; layout from `packets_struct.h` (`PACKET_ZC_ATTACK_RANGE`) (checked 2026-11-22, SPEC-0011 T0.1). Capture: yes, SPEC-0010 `world-*.bin` (6, range 1 bare-handed) |
| **Allocates** | no |

**Hercules usage:** Sent by `clif_updatestatus` (`SP_ATTACKRANGE`) at map entry and when equipment changes the range.

**Description:** The own character's basic attack range in cells.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Header, `0x013A`. |
| Range | `short` | 2 bytes | Attack range in cells (`currentAttRange`). |

**Parsing:** read the field; ends with `Remaining == 0`.
