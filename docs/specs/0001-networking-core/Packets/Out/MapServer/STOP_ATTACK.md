# STOP_ATTACK

| | |
|---|---|
| **Direction** | Client to map server |
| **Hercules packet name** | `CZ_CANCEL_LOCKON` |
| **Packet ID** | `0x0118` |
| **Declared size** | 2 bytes total, including the 2-byte packet ID |
| **Status** | documented |
| **Frequency** | warm |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406; handler `clif->pStopAttack` in `packets.h`, and `clif_parse_StopAttack` reads nothing (checked 2026-11-22, SPEC-0011 T0.1). Capture: no |
| **Allocates** | no |

**Hercules usage:** Handled by `clif_parse_StopAttack`, which calls `pc_stop_attack`. No answer.

**Description:** Stops the own character's continuous attack.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Header, `0x0118`. No payload. |
