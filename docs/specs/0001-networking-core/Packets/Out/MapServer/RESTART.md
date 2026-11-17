# RESTART

| | |
|---|---|
| **Direction** | Client to map server |
| **Hercules packet name** | `CZ_RESTART` |
| **Packet ID** | `0x00B2` |
| **Declared size** | 3 bytes total, including the 2-byte packet ID |
| **Status** | documented |
| **Frequency** | cold |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no; handler `clif->pRestart` registered for this ID in `packets_shuffle_main.h` |
| **Allocates** | no |

**Hercules usage:** Handled by `clif_parse_Restart` (offset 2).

**Description:** Respawn or return to character select after death.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Header, `0x00B2`. |
| Type | `byte` | 1 byte | `0` respawn at save point, `1` return to character select. |
