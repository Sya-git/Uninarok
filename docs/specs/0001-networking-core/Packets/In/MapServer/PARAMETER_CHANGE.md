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

**Description:** A status value of the own character. SPEC-0010 reads `VarId` 0 (walk speed); SPEC-0011 adds 5, 6, 7, 8, 11 and 55. While the character is dead, `5` (HP) carries the HP it will have after a restart (`display_fake_hp_when_dead`), not 0: death is never taken from it.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Header, `0x00B0`. |
| VarId | `ushort` | 2 bytes | Which value (`enum status_point_types`): `0` walk speed (ms per cell), `5` HP, `6` max HP, `7` SP, `8` max SP, `11` base level, `55` job level. Others are ignored. EXP and zeny use `LONG_PARAMETER_CHANGE` (`0x0ACB`). |
| Value | `int` | 4 bytes | New value. |

**Parsing:** read fields in order; ends with `Remaining == 0`.
