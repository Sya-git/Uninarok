# CHARACTER_SERVER_PING

| | |
|---|---|
| **Direction** | Client to character server |
| **Hercules packet name** | `CH_PING` (comment "client keep-alive packet (every 12 seconds)" in `char.cpp`) |
| **Packet ID** | `0x0187` |
| **Declared size** | 6 bytes total, including the 2-byte packet ID |
| **Status** | documented |
| **Frequency** | warm (every 12 seconds while connected to the character server) |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no; handler `char_parse_char_ping` at 0x0187 in `char.cpp` |
| **Allocates** | no |

**Hercules usage:** Handled by `chr->parse_char_ping`. It keeps the connection from hitting `stall_time` (60 s in `conf/common/socket.conf`) while the player sits on the character selection screen.

**Description:** Keepalive for the character server. It is not a map-server packet: the map keepalive is `HEARTBEAT_REQUEST` (0x0360).

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Header, `0x0187`. |
| AccountId | `uint` | 4 bytes | Account ID from the login (`ACCEPT_LOGIN`). |

**Serialization:** `WireSize = 6`. Send every 12 seconds from `CharSession.Tick` while the flow is in a character-server state; stop when leaving it.
