# MAP_SERVER_CHANGE

| | |
|---|---|
| **Direction** | Map server to client |
| **Hercules packet name** | `ZC_NPCACK_SERVERMOVE` |
| **Packet ID** | `0x0AC7` |
| **Declared size** | 156 bytes total, including the 2-byte packet ID |
| **Status** | implemented |
| **Frequency** | cold |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406; layout read in `src/map/clif.cpp` `clif_changemapserver` (checked 2026-11-22, SPEC-0010 T0.2). Capture: no |
| **Allocates** | no |

**Hercules usage:** Sent by `clif_changemapserver` when the target map is on another map server (the character server already handed the session over, `chrif_changemapserver`).

**Description:** The player is moved to another map server: the client drops this connection, connects to the given address and enters as on first entry, with the same account, character and auth code.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Header, `0x0AC7`. |
| MapName | fixed ASCII string | 16 bytes | Map with the `.gat` suffix, NUL padded. |
| X | `ushort` | 2 bytes | Cell X. |
| Y | `ushort` | 2 bytes | Cell Y. |
| Ip | 4 bytes | 4 bytes | IPv4 address, **big-endian** (`htonl`): the bytes in reading order. |
| Port | `ushort` | 2 bytes | Port, **little-endian**, 0 to 65535. |
| DnsHost | fixed ASCII string | 128 bytes | DNS name of the server, or empty. |

**Parsing:** name (`FixedString32Bytes`), `X`, `Y`, 4 address bytes, `Port`, `DnsHost` (`FixedString512Bytes`); ends with `Remaining == 0`.
