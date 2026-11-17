# CHARACTER_SLOTS_INFO

| | |
|---|---|
| **Direction** | Character server to client |
| **Hercules packet name** | no named constant in the Hercules source |
| **Packet ID** | `0x082D` |
| **Declared size** | Variable (`Size = -1`); the parser reads 25 payload bytes |
| **Status** | verified |
| **Frequency** | cold |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no Real server 2026-11-17 (Hercules built for 20220406): decoded from the captures `Login/Character/Map-20261117-124252.bin` with nothing left over (`RealCaptureDecodeTests`). |
| **Allocates** | cold packet: may allocate (arrays); see parsing note |

**Description:** Reports character-slot counts and a 20-byte region whose meaning is not defined.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Packet identifier `0x082D`. |
| PacketLength | `ushort` | 2 bytes | Total variable packet length, including this 4-byte framing header. |
| CharSlots | `byte` | 1 byte | Slot count read from the first payload byte. |
| AvailableCharSlots | `byte` | 1 byte | Available slot count. |
| Unknown | `byte` | 1 byte | Payload value with no defined meaning. |
| CharSlots2 | `byte` | 1 byte | Additional slot-related value; its exact meaning is unspecified. |
| CharSlots3 | `byte` | 1 byte | Additional slot-related value; its exact meaning is unspecified. |
| UnusedBytes | `byte[20]` | 20 bytes | Not interpreted; skipped by the parser and not stored. |

**Parsing:** The constructor receives payload bytes after the four-byte variable-length header.
