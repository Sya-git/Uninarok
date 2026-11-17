# CREATE_CHARACTER

| | |
|---|---|
| **Direction** | Client to character server |
| **Hercules packet name** | no named constant in the Hercules source |
| **Packet ID** | `0x0A39` |
| **Declared size** | 36 bytes, including the 2-byte packet ID |
| **Status** | implemented |
| **Frequency** | cold |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no; `char_parse_char_create_new_char` in `char.cpp`: name 2, slot 26, hair colour 27, hair style 29, job 31 (4 bytes), sex 35 |
| **Allocates** | no |

**Description:** Requests creation of a character with the supplied name, appearance, starting job class, and sex.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Packet identifier `0x0A39`. |
| Name | fixed ASCII byte string | 24 bytes | Character name; non-ASCII characters are replaced with `?`, and unused bytes are zero-filled. |
| Slot | `byte` | 1 byte | Character slot for the new character. |
| HairColor | `ushort` | 2 bytes | Requested hair color. |
| HairStyle | `ushort` | 2 bytes | Requested hair style. |
| JobClass | `uint` | 4 bytes | Starting job class ID. Verified: Hercules reads it with `RFIFOL(fd, 31)`, so it is 4 bytes, not a word. |
| Sex | `byte` | 1 byte | Character sex value. |

**Serialization:** writes 36 bytes in table order; unused text bytes are zero.
