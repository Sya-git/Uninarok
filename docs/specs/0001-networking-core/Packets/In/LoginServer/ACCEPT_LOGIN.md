# ACCEPT_LOGIN

| | |
|---|---|
| **Direction** | Login server to client |
| **Hercules packet name** | `AC_ACCEPT_LOGIN2` |
| **Packet ID** | `0x0AC4` |
| **Declared size** | Variable; fixed prefix is 64 bytes, followed by one or more server entries |
| **Status** | documented |
| **Frequency** | cold |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no |
| **Allocates** | cold packet: may allocate (arrays); see parsing note |

**Packet ID note:** `0x0AC4` (`AC_ACCEPT_LOGIN2` in Hercules)

**Hercules usage:** Sent by the login server after successful authentication.

**Description:** Confirms login and returns account authentication values and the available character-server endpoints.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `int16` | 2 bytes | Packet identifier `0x0AC4`. |
| PacketLength | `int16` | 2 bytes | Total packet length, including this header. |
| AuthCode | `int32` | 4 bytes | Authentication code. |
| Aid | `uint32` | 4 bytes | Account identifier. |
| UserLevel | `uint32` | 4 bytes | Account user level. |
| LastLoginIp | `uint32` | 4 bytes | Last login IP field; Hercules currently sends zero because it is unused. |
| LastLoginTime | fixed byte array | 26 bytes | Last-login timestamp field; Hercules currently sends zero-filled bytes because it is unused. |
| Sex | `uint8` | 1 byte | Account sex value. |
| AuthToken | `byte[16]` | 16 bytes | Authentication token. |
| TwitterFlag | `uint8` | 1 byte | Twitter-related flag. |
| ServerList | `ServerEndpoint[]` | 160 bytes per entry | Variable list of character-server endpoints. |

Each `ServerEndpoint` is serialized as follows:

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| Ip | IPv4 address bytes | 4 bytes | Server IPv4 address; Hercules writes the address in network byte order. |
| Port | `int16` | 2 bytes | Character-server port. |
| Name | fixed byte array | 20 bytes | Character-server name. |
| UserCount | `uint16` | 2 bytes | Online user count. |
| State | `uint16` | 2 bytes | Server state. |
| Property | `uint16` | 2 bytes | Server property. |
| Unknown2 | fixed byte array | 128 bytes | Opaque field whose meaning is not defined by the packet source. |

The total length is `64 + 160 * ServerList.Length` bytes. Hercules confirms the fixed 128-byte `Unknown2` region; read it with `Skip(128)`.
