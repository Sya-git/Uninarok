# SELECT_CHARACTER

| | |
|---|---|
| **Direction** | Client to character server |
| **Hercules packet name** | no named constant in the Hercules source |
| **Packet ID** | `0x0066` |
| **Declared size** | 3 bytes, including the 2-byte packet ID |
| **Status** | implemented |
| **Frequency** | cold |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no |
| **Allocates** | no |

**Description:** Selects a character slot.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Packet identifier `0x0066`. |
| Slot | `byte` | 1 byte | Index of the character slot to select. |

**Serialization:** writes 3 bytes in table order.
