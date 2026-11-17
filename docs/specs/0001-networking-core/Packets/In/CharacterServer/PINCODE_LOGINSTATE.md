# PINCODE_LOGINSTATE

| | |
|---|---|
| **Direction** | Character server to client |
| **Hercules packet name** | no named constant in the Hercules source |
| **Packet ID** | `0x08B9` |
| **Declared size** | 12 bytes, including the 2-byte packet ID |
| **Status** | verified |
| **Frequency** | cold |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no Real server 2026-11-17 (Hercules built for 20220406): decoded from the captures `Login/Character/Map-20261117-124252.bin` with nothing left over (`RealCaptureDecodeTests`). |
| **Allocates** | no |

**Description:** Reports PIN-code login state and its associated seed and account identifier.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Packet identifier `0x08B9`. |
| PincodeSeed | `int` | 4 bytes | Seed value associated with PIN-code handling. |
| AccountId | `int` | 4 bytes | Account identifier. |
| State | `short` | 2 bytes | PIN-code state value; meanings are not yet documented. |

**Parsing:** The constructor receives the payload after the packet ID and reads fields in table order.
