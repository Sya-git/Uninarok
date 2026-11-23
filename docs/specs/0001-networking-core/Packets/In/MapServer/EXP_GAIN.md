# EXP_GAIN

| | |
|---|---|
| **Direction** | Map server to client |
| **Hercules packet name** | `ZC_NOTIFY_EXP2` |
| **Packet ID** | `0x0ACC` |
| **Declared size** | 18 bytes total, including the 2-byte packet ID |
| **Status** | documented |
| **Frequency** | warm |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406; layout read in `src/map/clif.cpp` `clif_displayexp` (no struct) (checked 2026-11-22, SPEC-0011 T0.1). Capture: yes, SPEC-0010 `world-prt_fild08.bin` (16) |
| **Allocates** | no |

**Hercules usage:** Sent by `clif_displayexp` from `pc_gainexp` on every gain, one packet for base EXP and one for job EXP (each only when not 0).

**Description:** EXP gained by the own character. It is for display only; the totals come from `LONG_PARAMETER_CHANGE`.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Header, `0x0ACC`. |
| UnitId | `uint` | 4 bytes | Own object ID. |
| Amount | `long` | 8 bytes | EXP gained. |
| VarId | `ushort` | 2 bytes | `1` base EXP, `2` job EXP. |
| Quest | `ushort` | 2 bytes | `1` quest EXP (shown in another colour), `0` otherwise. |

**Parsing:** read fields in order; ends with `Remaining == 0`.
