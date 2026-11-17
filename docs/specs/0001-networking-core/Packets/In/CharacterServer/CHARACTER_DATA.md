# CHARACTER_DATA

| | |
|---|---|
| **Direction** | Character server to client |
| **Hercules packet name** | no named constant in the Hercules source |
| **Packet ID** | `0x006B` |
| **Declared size** | Variable (`Size = -1`) |
| **Status** | documented |
| **Frequency** | cold |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no |
| **Allocates** | cold packet: may allocate (arrays); see parsing note |

**Description:** Contains character-list metadata followed by zero or more fixed-width character records. Each record is 155 bytes and is documented separately in [`../../../../../data/CharInfo.md`](../../../../../data/CharInfo.md).

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Packet identifier `0x006B`. |
| PacketLength | `ushort` | 2 bytes | Total variable packet length, including this 4-byte framing header. |
| MaxChars | `byte` | 1 byte | Maximum character count. |
| AvailableSlots | `byte` | 1 byte | Available character-slot count. |
| PremiumSlots | `byte` | 1 byte | Premium slot count. |
| UnknownBytes | `byte[20]` | 20 bytes | Bytes retained without interpretation. |
| CharsInfo | `CharInfo[]` | 155 bytes per record | Remaining payload divided into 155-byte character records; the count is derived from the remaining byte length. |

**Parsing:** The constructor receives payload bytes after the four-byte variable-length header. The parser derives the record count by dividing the bytes after the 23-byte metadata payload by `CharInfo.Size`.
