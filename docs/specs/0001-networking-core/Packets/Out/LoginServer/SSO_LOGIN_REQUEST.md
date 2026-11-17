# SSO_LOGIN_REQUEST

| | |
|---|---|
| **Direction** | Client to login server |
| **Hercules packet name** | `CA_SSO_LOGIN_REQ` |
| **Packet ID** | `0x0825` |
| **Declared size** | Variable; fixed prefix is 92 bytes including the packet ID and length, followed by a variable token |
| **Status** | documented |
| **Frequency** | cold |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no |
| **Allocates** | no |

**Hercules usage:** Registered and processed by the login server; the variable `T1` token is used as the credential.

**Description:** Sends an SSO login token with client and account metadata.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `int16` | 2 bytes | Packet identifier `0x0825`. |
| PacketLength | `int16` | 2 bytes | Total packet length, including this header. |
| Version | `uint32` | 4 bytes | Client version. |
| ClientType | `uint8` | 1 byte | Client type. |
| Id | fixed byte array | 24 bytes | Account username. |
| Password | fixed byte array | 27 bytes | Password field present in the packet structure; the login handler instead authenticates using the variable `T1` token. |
| MacAddress | fixed byte array | 17 bytes | MAC address field. |
| Ip | fixed byte array | 15 bytes | IP address field. |
| T1 | variable byte sequence | Remaining bytes | SSO login token. The login handler derives its length from the packet remainder and uses this field as the credential. |

The packet length is `92 + T1.Length` bytes.
