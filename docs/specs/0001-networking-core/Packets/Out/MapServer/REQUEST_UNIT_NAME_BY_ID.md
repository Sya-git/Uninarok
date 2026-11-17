# REQUEST_UNIT_NAME_BY_ID

| | |
|---|---|
| **Direction** | Client to map server |
| **Hercules packet name** | `CZ_REQNAME_BYGID` |
| **Packet ID** | `0x0369` |
| **Declared size** | 6 bytes total, including the 2-byte packet ID |
| **Status** | documented |
| **Frequency** | warm |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no; handler `clif->pSolveCharName` registered for this ID in `packets_shuffle_main.h` |
| **Allocates** | no |

**Hercules usage:** Handled by `clif_parse_SolveCharName`. Answer: `ZC_ACK_REQNAME_BYGID`.

**Description:** Asks the name of a character by character ID (not object ID).

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Header, `0x0369`. |
| CharId | `uint` | 4 bytes | Character ID whose name is wanted. |
