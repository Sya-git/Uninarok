# CHARACTER_CREATION_FAILED

| | |
|---|---|
| **Direction** | Character server to client |
| **Hercules packet name** | no named constant (`chr->creation_failed`, `char.cpp`) |
| **Packet ID** | `0x006E` |
| **Declared size** | 3 bytes total, including the 2-byte packet ID |
| **Status** | implemented |
| **Frequency** | cold |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no; codes from `char_creation_failed` |
| **Allocates** | no |

**Hercules usage:** Sent when `CREATE_CHARACTER` fails.

**Description:** Character creation failed; `ErrorCode` says why. In SPEC-0001 (FR-13) this returns the flow to character selection with a reason.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Header, `0x006E`. |
| ErrorCode | `byte` | 1 byte | See the table below. |

| ErrorCode | Meaning |
|---:|---|
| `0x00` | Character name already exists. |
| `0x01` | You are underaged. |
| `0x02` | Symbols in character names are forbidden. |
| `0x03` | You are not eligible to open the character slot. |
| `0xFF` | Character creation denied. |

Other client-side codes exist (`0x0B` premium only, `0x0C` invalid name) but Hercules does not send them.
