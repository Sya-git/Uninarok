# PARAMETER_CHANGE

| | |
|---|---|
| **Direction** | Map server to client |
| **Hercules packet name** | `ZC_PAR_CHANGE` |
| **Packet ID** | `0x00B0` |
| **Declared size** | 8 bytes total, including the 2-byte packet ID |
| **Status** | verified |
| **Frequency** | warm |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406; layout read in `packets_struct.h` `PACKET_ZC_PAR_CHANGE`, `src/map/clif.cpp` `clif_updatestatus` (checked 2026-11-22, SPEC-0010 T0.2). Capture: yes (`Map-20261117-124252.bin`) |
| **Allocates** | no |

**Hercules usage:** Sent by `clif_updatestatus` when a status value of the own character changes.

**Description:** A status value of the own character. SPEC-0010 reads `VarId` 0 (walk speed, ms per cell) only.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Header, `0x00B0`. |
| VarId | `ushort` | 2 bytes | Which value (`enum status_point_types`): `0` walk speed. |
| Value | `int` | 4 bytes | New value. |

**Parsing:** read fields in order; ends with `Remaining == 0`.
