# GLOBAL_CHAT

| | |
|---|---|
| **Direction** | Client to map server |
| **Hercules packet name** | `CZ_REQUEST_CHAT` |
| **Packet ID** | `0x00F3` |
| **Declared size** | Variable (`Size = -1`) |
| **Status** | implemented |
| **Frequency** | warm |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Real server 2026-11-22 (Hercules built for 20220406): sent and received on the local server (SPEC-0010 T8.6). Capture: no; handler `clif->pGlobalMessage` registered for this ID in `packets_shuffle_main.h` |
| **Allocates** | cold packet: it holds its text |

**Hercules usage:** Handled by `clif_parse_GlobalMessage` (offsets 2 and 4).

**Description:** Sends a public chat message.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Header, `0x00F3`. |
| PacketLength | `ushort` | 2 bytes | Total length including header. |
| Message | text | n bytes | NUL-terminated `<name> : <text>`; the server checks that the prefix is the player's own name. |

**Serialization:** variable packet; `WireSize = 4 + bytes + 1`. Text encoding is Windows-1252 by default (spec section 6); the client writes ASCII so far. A text that starts with `@` or `#` after the name is a command of the server; one the account's group does not have is a chat line as any other, sent back as [OWN_MESSAGE](../../In/MapServer/OWN_MESSAGE.md).
