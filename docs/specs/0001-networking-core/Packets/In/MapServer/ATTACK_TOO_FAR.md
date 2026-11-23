# ATTACK_TOO_FAR

| | |
|---|---|
| **Direction** | Map server to client |
| **Hercules packet name** | `ZC_ATTACK_FAILURE_FOR_DISTANCE` |
| **Packet ID** | `0x0139` |
| **Declared size** | 16 bytes total, including the 2-byte packet ID |
| **Status** | implemented |
| **Frequency** | warm |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406; layout from `packets_struct.h` (`PACKET_ZC_ATTACK_FAILURE_FOR_DISTANCE`) (checked 2026-11-22, SPEC-0011 T0.1). Capture: no |
| **Allocates** | no |

**Hercules usage:** Sent by `clif_movetoattack` from `unit_attack_timer_sub` when the own character's target is out of range. The server does not walk a player into range: the attack stops (SPEC-0011 §6).

**Description:** The own character's attack was refused because the target is too far. Carries both cells as the server sees them and the attack range.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Header, `0x0139`. |
| TargetId | `uint` | 4 bytes | Object ID of the target (`targetAID`). |
| TargetX | `short` | 2 bytes | Target's cell x on the server. |
| TargetY | `short` | 2 bytes | Target's cell y on the server. |
| X | `short` | 2 bytes | Own cell x on the server. |
| Y | `short` | 2 bytes | Own cell y on the server. |
| Range | `short` | 2 bytes | Current attack range (`currentAttRange`). |

**Parsing:** read fields in order; ends with `Remaining == 0`.
