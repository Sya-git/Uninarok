# QUIT_GAME

| | |
|---|---|
| **Direction** | Client to map server |
| **Hercules packet name** | `CZ_REQ_DISCONNECT` |
| **Packet ID** | `0x018A` |
| **Declared size** | 4 bytes total, including the 2-byte packet ID |
| **Status** | documented |
| **Frequency** | cold |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no; handler `clif->pQuitGame` registered for this ID in `packets_shuffle_main.h` |
| **Allocates** | no |

**Hercules usage:** Handled by `clif_parse_QuitGame`.

**Description:** Asks to leave the game; the server replies `ZC_ACK_REQ_DISCONNECT` (0x018B).

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Header, `0x018A`. |
| Type | `ushort` | 2 bytes | `0` quit. |
