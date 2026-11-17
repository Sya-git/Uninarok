# MAP_ENTRY_REFUSAL

| | |
|---|---|
| **Direction** | Map server to client |
| **Hercules packet name** | `ZC_REFUSE_ENTER` |
| **Packet ID** | `0x0074` |
| **Declared size** | 3 bytes, including the 2-byte packet ID |
| **Status** | implemented |
| **Frequency** | cold |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no; `clif_authrefuse`, `clif.cpp` (3 bytes) |
| **Allocates** | no |

**Hercules usage:** Sent by Hercules when a map-server connection attempt is refused.

**Description:** Reports that the map server refused the client's connection attempt.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Packet identifier `0x0074`. |
| ErrorCode | `byte` | 1 byte | Refusal code. Hercules documents values 0–4 for client mismatch, ID mismatch, or mobile-client states. |

**Parsing:** The local parser reads `ErrorCode` from the payload after the packet ID.
