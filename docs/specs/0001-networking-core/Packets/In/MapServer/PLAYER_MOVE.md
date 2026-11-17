# PLAYER_MOVE

| | |
|---|---|
| **Direction** | Map server to client |
| **Hercules packet name** | `ZC_NOTIFY_PLAYERMOVE` |
| **Packet ID** | `0x0087` |
| **Declared size** | 12 bytes total, including the 2-byte packet ID |
| **Status** | documented |
| **Frequency** | warm (every time the local player is told to walk) |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no |
| **Allocates** | no |

**Hercules usage:** Sent by `clif_walkok` after a valid `REQUEST_MOVE`.

**Description:** Confirms the local player's walk request and gives the destination. The client then interpolates the movement.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Header, `0x0087`. |
| StartTick | `uint` | 4 bytes | Server tick when the move starts. |
| MoveData | `UninarokMoveData` | 6 bytes | Source and destination tile of the path. [`UninarokMoveData`](../../../../../data/UninarokMoveData.md). |

**Parsing:** `StartTick = r.ReadUInt32(); MoveData = UninarokMoveData.Read(ref r);` ending with `Remaining == 0`. Other units use `UNIT_MOVE_ENTRY` / `UNIT_NEW_ENTRY`.
