# CHARACTER_CREATION_SUCCESS

| | |
|---|---|
| **Direction** | Character server to client |
| **Hercules packet name** | `HC_ACCEPT_MAKECHAR` |
| **Packet ID** | `0x006D` |
| **Declared size** | 177 bytes, including the 2-byte packet ID (the Hercules length table says 157; see the correction below) |
| **Status** | implemented |
| **Frequency** | cold |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no |
| **Allocates** | no |

**Description:** Reports successful character creation and returns the new character's `CharInfo` record.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Packet identifier `0x006D`. |
| CharInfo | `CharInfo` | 175 bytes | Character record parsed from the packet payload; see [`../../../../../data/CharInfo.md`](../../../../../data/CharInfo.md). |

**Parsing:** The constructor receives the payload after the packet ID and passes it to the `CharInfo` parser.

**Length correction (2026-11-17):** the Hercules 2022 length table lists `0x006D` as 157 bytes, but `char_creation_ok` writes `2 + MAX_CHAR_BUF` = 177 bytes for this version, because the embedded `CharInfo` is 175 bytes (see `data/CharInfo.md`). The framer reads lengths from the table, so the table fixture carries the corrected value 177 (`_corrections` in `fixtures/hercules-packetlen-20220406.json`). Found by reading the server source after a real run exposed the 175-byte record; not yet seen in a real capture of a character creation.
