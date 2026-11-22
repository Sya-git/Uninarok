# MAP_CHANGE

| | |
|---|---|
| **Direction** | Map server to client |
| **Hercules packet name** | `ZC_NPCACK_MAPMOVE` |
| **Packet ID** | `0x0091` |
| **Declared size** | 22 bytes total, including the 2-byte packet ID |
| **Status** | verified |
| **Frequency** | cold |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406; layout read in `src/map/clif.cpp` `clif_changemap` (checked 2026-11-22, SPEC-0010 T0.2). Capture: yes (`Map-20261117-124252.bin`: `iz_int.gat`, 18, 27) |
| **Allocates** | no |

**Hercules usage:** Sent by `pc_setpos` (warp, teleport, server move), also when the target is on the same map, and right after `MAP_LOADED` when a script moved the player while it was loading (`state.rewarp`). `pc_authok` also sends one right after `AUTHENTICATION_ACCEPTED`, to the very place of the entry: the client drops it (PLAN-0010 D23).

**Description:** The player is moved to a map of the same map server. The server has already removed the player from the old place; the client loads the map and answers `MAP_LOADED`.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Header, `0x0091`. |
| MapName | fixed ASCII string | 16 bytes | Map with the `.gat` suffix, NUL padded. |
| X | `ushort` | 2 bytes | Cell X. |
| Y | `ushort` | 2 bytes | Cell Y. |

**Parsing:** name (`FixedString32Bytes`), `X`, `Y`; ends with `Remaining == 0`. No facing: Hercules turns the player north (`player_warp_keep_direction` off).
