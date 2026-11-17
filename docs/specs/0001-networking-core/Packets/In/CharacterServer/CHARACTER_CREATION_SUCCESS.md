# CHARACTER_CREATION_SUCCESS

| | |
|---|---|
| **Direction** | Character server to client |
| **Hercules packet name** | `HC_ACCEPT_MAKECHAR` |
| **Packet ID** | `0x006D` |
| **Declared size** | 157 bytes, including the 2-byte packet ID |
| **Status** | documented |
| **Frequency** | cold |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no |
| **Allocates** | no |

**Description:** Reports successful character creation and returns the new character's `CharInfo` record.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Packet identifier `0x006D`. |
| CharInfo | `CharInfo` | 155 bytes | Character record parsed from the packet payload; see [`../../../../../data/CharInfo.md`](../../../../../data/CharInfo.md). |

**Parsing:** The constructor receives the payload after the packet ID and passes it to the `CharInfo` parser.
