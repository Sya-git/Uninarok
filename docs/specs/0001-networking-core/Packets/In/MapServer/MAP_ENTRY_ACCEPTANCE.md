# MAP_ENTRY_ACCEPTANCE

| | |
|---|---|
| **Direction** | Map server to client |
| **Hercules packet name** | `ZC_NOTIFY_PLAYERID` |
| **Packet ID** | `0x0283` |
| **Declared size** | 6 bytes, including the 2-byte packet ID |
| **Status** | verified |
| **Frequency** | cold |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no; `clif_authok`-time sender, `clif.cpp` (4-byte ID at offset 2, total 6) Real server 2026-11-17 (Hercules built for 20220406): decoded from the captures `Login/Character/Map-20261117-124252.bin` with nothing left over (`RealCaptureDecodeTests`). |
| **Allocates** | no |

**Hercules usage:** Sent by Hercules after accepting a map-server connection request.

**Description:** Confirms the player's map-server connection and provides the assigned character object ID.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Packet identifier `0x0283`. |
| CharacterObjectId | `uint` | 4 bytes | Player's object ID assigned by the map server. |

**Parsing:** read `CharacterObjectId` and finish with `Remaining == 0`. The packet is exactly 6 bytes (header plus the 4-byte ID); Hercules writes nothing after it, so no `Skip` is needed.
