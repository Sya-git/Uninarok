# NPC_CLICK

| | |
|---|---|
| **Direction** | Client to map server |
| **Hercules packet name** | `CZ_CONTACTNPC` |
| **Packet ID** | `0x0090` |
| **Declared size** | 7 bytes total, including the 2-byte packet ID |
| **Status** | documented |
| **Frequency** | cold |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no; handler `clif->pNpcClicked` registered for this ID in `packets_shuffle_main.h` |
| **Allocates** | no |

**Hercules usage:** Handled by `clif_parse_NpcClicked` (offset 2).

**Description:** Starts a dialogue with an NPC.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Header, `0x0090`. |
| NpcId | `uint` | 4 bytes | Object ID of the NPC. |
| Type | `byte` | 1 byte | Click type (`1` talk). |
