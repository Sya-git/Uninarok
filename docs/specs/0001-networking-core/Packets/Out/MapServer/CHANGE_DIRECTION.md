# CHANGE_DIRECTION

| | |
|---|---|
| **Direction** | Client to map server |
| **Hercules packet name** | `CZ_CHANGE_DIRECTION` |
| **Packet ID** | `0x0361` |
| **Declared size** | 5 bytes total, including the 2-byte packet ID |
| **Status** | implemented |
| **Frequency** | warm |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no; handler `clif->pChangeDir` registered for this ID in `packets_shuffle_main.h` |
| **Allocates** | no |

**Hercules usage:** Handled by `clif_parse_ChangeDir` (offsets 2 and 4).

**Description:** Turns the player in place.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Header, `0x0361`. |
| HeadDirection | `ushort` | 2 bytes | Head direction: `0` front, `1` right, `2` left. |
| Direction | `byte` | 1 byte | Body direction (0 to 7, see `UninarokDirection`). |
