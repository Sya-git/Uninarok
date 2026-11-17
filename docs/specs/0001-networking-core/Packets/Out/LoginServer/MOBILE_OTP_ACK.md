# MOBILE_OTP_ACK

| | |
|---|---|
| **Direction** | Client to login server |
| **Hercules packet name** | `CA_ACK_MOBILE_OTP` |
| **Packet ID** | `0x09A3` |
| **Declared size** | Variable; the defined fields occupy at least 14 bytes including the framing header |
| **Status** | documented |
| **Frequency** | cold |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no |
| **Allocates** | no |

**Hercules usage:** Registered, but the login-side handler currently has a TODO and does not parse the fields.

**Description:** Acknowledges mobile OTP using an account identifier and a fixed-width code.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `int16` | 2 bytes | Packet identifier `0x09A3`. |
| PacketLength | `int16` | 2 bytes | Total variable packet length, including this header. |
| Aid | `uint32` | 4 bytes | Account identifier. |
| Code | fixed byte array | 6 bytes | Mobile OTP code. |

**Implementation note:** The generated 20220406 packet-length table marks this packet as variable. The packet struct defines the fields above; any additional variable bytes are unspecified. Hercules' login-side packet registration currently uses the struct size as a fixed length, and its handler still has a TODO for parsing the packet data.
