# MAP_LOADED

| | |
|---|---|
| **Direction** | Client to map server |
| **Hercules packet name** | `CZ_NOTIFY_ACTORINIT` (handler `clif_parse_LoadEndAck`) |
| **Packet ID** | `0x007D` |
| **Declared size** | 2 bytes total (header only, no payload) |
| **Status** | verified |
| **Frequency** | cold (once per map load) |
| **Verified** | no (Hercules length table 20220406 lists 0x007D = 2; confirm with a capture) Real server 2026-11-17 (Hercules built for 20220406): accepted by the server during a full login to map and 62 s in game (`Captures/`). |
| **Allocates** | no |

**Hercules usage:** Sent by the client when it has finished loading the map. The server then spawns the character and starts sending the surroundings. Without it the character never appears.

**Description:** Empty notification. In SPEC-0001 it is sent when the flow leaves `LoadingMap`, which moves the flow to `InGame` (spec section 6).

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Header, `0x007D`. |

**Serialization:** `WireSize = 2`; write the ID only.
