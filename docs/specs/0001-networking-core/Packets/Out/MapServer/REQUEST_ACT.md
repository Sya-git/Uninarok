# REQUEST_ACT

| | |
|---|---|
| **Direction** | Client to map server |
| **Hercules packet name** | `CZ_REQUEST_ACT` |
| **Packet ID** | `0x0437` |
| **Declared size** | 7 bytes total, including the 2-byte packet ID |
| **Status** | implemented |
| **Frequency** | hot |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no; handler `clif->pActionRequest` registered for this ID in `packets_shuffle_main.h` |
| **Allocates** | no |

**Hercules usage:** Handled by `clif_parse_ActionRequest` (offsets 2 and 6).

**Description:** Starts an attack on a unit, or sits / stands.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Header, `0x0437`. |
| TargetId | `uint` | 4 bytes | Object ID to attack; `0` for sit/stand. |
| Action | `byte` | 1 byte | `0` attack once, `7` attack continuously, `2` sit, `3` stand. |
