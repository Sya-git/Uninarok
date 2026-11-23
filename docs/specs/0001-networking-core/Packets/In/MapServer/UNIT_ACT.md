# UNIT_ACT

| | |
|---|---|
| **Direction** | Map server to client |
| **Hercules packet name** | `ZC_NOTIFY_ACT` |
| **Packet ID** | `0x008A` |
| **Declared size** | 29 bytes total, including the 2-byte packet ID |
| **Status** | implemented |
| **Frequency** | hot |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406; layout read in `src/map/clif.cpp` `clif_sitting`, `clif_standing` (checked 2026-11-22, SPEC-0010 T0.2) and `clif_takeitem` (checked 2026-11-22, SPEC-0011 T0.1). Capture: no |
| **Allocates** | no |

**Hercules usage:** Sent to the area, the unit included, by `clif_takeitem` (action 1: a unit picked up an item, from `pc_takeitem`), `clif_sitting` (action 2) and `clif_standing` (action 3). Damage uses `DAMAGE` (`0x08C8`) at this version.

**Description:** A unit picked up an item, sat down or stood up.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Header, `0x008A`. |
| UnitId | `uint` | 4 bytes | Object ID of the unit. |
| TargetId | `uint` | 4 bytes | Object ID of the picked-up item; **valid for action 1 only** (uninitialised for 2 and 3). |
| Reserved | `bytes` | 16 bytes | Not used by these actions; Hercules sends **uninitialised** bytes. Skip. |
| Action | `byte` | 1 byte | `1` pick up, `2` sit, `3` stand. |
| Reserved2 | `bytes` | 2 bytes | As `Reserved`. |

**Parsing:** `UnitId`, `TargetId`, `Skip(16)`, `Action`, `Skip(2)`; ends with `Remaining == 0`. Never interpret the skipped bytes, nor `TargetId` unless `Action` is 1.
