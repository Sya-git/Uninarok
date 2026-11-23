# LONG_PARAMETER_CHANGE

| | |
|---|---|
| **Direction** | Map server to client |
| **Hercules packet name** | `ZC_LONGLONGPAR_CHANGE` |
| **Packet ID** | `0x0ACB` |
| **Declared size** | 12 bytes total, including the 2-byte packet ID |
| **Status** | verified |
| **Frequency** | warm |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406; layout from `packets_struct.h` (`PACKET_ZC_LONGLONGPAR_CHANGE`), vars from `clif_updatestatus` (checked 2026-11-22, SPEC-0011 T0.1). Capture: yes, SPEC-0010 `world-*.bin` (28; vars 1, 2, 22, 23) |
| **Allocates** | no |

**Hercules usage:** Sent by `clif_updatestatus` for EXP and zeny when `PACKETVER_MAIN_NUM` ≥ 20170906. It replaces `ZC_LONGPAR_CHANGE` (`0x00B1`), which is never sent at this version.

**Description:** A 64-bit status value of the own character.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Header, `0x0ACB`. |
| VarId | `ushort` | 2 bytes | Which value (`enum status_point_types`): `1` base EXP, `2` job EXP, `20` zeny, `22` base EXP to the next level, `23` job EXP to the next level. |
| Value | `long` | 8 bytes | New value. |

**Parsing:** read fields in order; ends with `Remaining == 0`. SPEC-0011 reads vars 1, 2, 22 and 23 and ignores 20.
