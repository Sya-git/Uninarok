# GLOBAL_MESSAGE

| | |
|---|---|
| **Direction** | Map server to client |
| **Hercules packet name** | `ZC_NOTIFY_CHAT` |
| **Packet ID** | `0x008D` |
| **Declared size** | Variable (`Size = -1`) |
| **Status** | documented |
| **Frequency** | warm (chat) |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no |
| **Allocates** | no (decode into a FixedString) |

**Hercules usage:** Sent by `clif_GlobalMessage` and `clif_process_message`.

**Description:** Public chat bubble and chat-window line from a unit in range.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Header, `0x008D`. |
| PacketLength | `ushort` | 2 bytes | Total length including header (8 + message bytes). |
| UnitId | `uint` | 4 bytes | Object ID of the speaker. |
| Message | text | n bytes | NUL-terminated text, `PacketLength - 8` bytes including the NUL. |

**Parsing:** variable packet; `Message` is the rest of the payload up to the NUL. The text may contain `Name : text` for players. Text encoding is Windows-1252 by default (spec section 6). **Allocates:** reading into `FixedString512Bytes` avoids a `string`.
