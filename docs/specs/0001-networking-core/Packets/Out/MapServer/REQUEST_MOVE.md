# REQUEST_MOVE

| | |
|---|---|
| **Direction** | Client to map server |
| **Hercules packet name** | `CZ_REQUEST_MOVE` |
| **Packet ID** | `0x035F` |
| **Declared size** | 5 bytes total, including the 2-byte packet ID |
| **Status** | documented |
| **Frequency** | warm |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no; handler `clif->pWalkToXY` registered for this ID in `packets_shuffle_main.h` |
| **Allocates** | no |

**Hercules usage:** Handled by `clif_parse_WalkToXY` (offset 2).

**Description:** Asks the server to walk the player to a tile.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Header, `0x035F`. |
| Position | `UninarokPosition` | 3 bytes | Destination tile; the direction bits are ignored by Hercules. [`UninarokPosition`](../../../../../data/UninarokPosition.md). |

**Serialization:** write the header, then `UninarokPosition.Write` with the target X and Y (direction `0`). Server answers `PLAYER_MOVE` or `UNIT_STOP_MOVE`.
