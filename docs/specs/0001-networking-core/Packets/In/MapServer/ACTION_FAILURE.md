# ACTION_FAILURE

| | |
|---|---|
| **Direction** | Map server to client |
| **Hercules packet name** | `ZC_ACTION_FAILURE` |
| **Packet ID** | `0x013B` |
| **Declared size** | 4 bytes total, including the 2-byte packet ID |
| **Status** | documented |
| **Frequency** | cold |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406; layout read in `src/map/clif.cpp` `clif_arrow_fail` (no struct) (checked 2026-11-22, SPEC-0011 T0.1). Capture: no |
| **Allocates** | no |

**Hercules usage:** Sent by `clif_arrow_fail`. Type 0 comes from `battle_check_arrows` when a bow user attacks without arrows, and the attack stops. Other types come from skill code (SPEC-0012).

**Description:** An action failed for lack of ammunition or a similar reason.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Header, `0x013B`. |
| Type | `ushort` | 2 bytes | `0` no arrows equipped (the only one SPEC-0011 handles); other values are sent by skills. |

**Parsing:** read the field; ends with `Remaining == 0`.
