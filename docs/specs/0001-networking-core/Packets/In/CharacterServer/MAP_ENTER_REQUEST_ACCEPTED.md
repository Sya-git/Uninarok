# MAP_ENTER_REQUEST_ACCEPTED

| | |
|---|---|
| **Direction** | Character server to client |
| **Hercules packet name** | no named constant in the Hercules source |
| **Packet ID** | `0x0AC5` |
| **Declared size** | 156 bytes, including the 2-byte packet ID |
| **Status** | implemented |
| **Frequency** | cold |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no |
| **Allocates** | no |

**Description:** Provides the character ID and network endpoint used to enter the map server.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Packet identifier `0x0AC5`. |
| CharId | `int` | 4 bytes | Character identifier. |
| MapName | fixed byte string | 16 bytes | Map name. |
| IpAddress | `byte[4]` | 4 bytes | IPv4 address octets. Stored as `uint IpAddress` (first octet in the lowest byte); `FormatIpAddress()` returns the dotted text (allocates, call once per session). |
| Port | `ushort` | 2 bytes | Server port. Read as `ushort`: a `short` makes ports above 32767 negative (FR-12). |
| DnsHost | fixed byte string | 128 bytes | DNS host value. Needs `FixedString512Bytes`: 128 wire bytes can hold 127 characters, more than `FixedString128Bytes` capacity (125). |

**Parsing:** The constructor receives the payload after the packet ID and reads fields in table order.
