# STATUS_CHANGE

| | |
|---|---|
| **Direction** | Map server to client |
| **Hercules packet name** | struct `packet_status_change` |
| **Packet ID** | `0x0983` |
| **Declared size** | 29 bytes total, including the 2-byte packet ID |
| **Status** | documented |
| **Frequency** | warm |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no; layout from `packets_struct.h` (`packet_status_change`) |
| **Allocates** | no |

**Hercules usage:** Sent by `clif_status_change` and related.

**Description:** A status effect (buff, debuff, ailment) starts or ends on a unit, with remaining time.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Header. |
| index | `short` | 2 bytes | Status icon ID (`SI_*`). |
| AID | `uint` | 4 bytes | Unit ID. |
| state | `byte` | 1 byte | `1` start, `0` end. |
| Total | `uint` | 4 bytes | Total duration (ms). |
| Left | `uint` | 4 bytes | Remaining duration (ms). |
| val1 | `int` | 4 bytes | Status value 1. |
| val2 | `int` | 4 bytes | Status value 2. |
| val3 | `int` | 4 bytes | Status value 3. |
