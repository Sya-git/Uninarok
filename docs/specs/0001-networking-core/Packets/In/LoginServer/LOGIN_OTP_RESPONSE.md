# LOGIN_OTP_RESPONSE

| | |
|---|---|
| **Direction** | Login server to client |
| **Hercules packet name** | `AC_LOGIN_OTP` |
| **Packet ID** | `0x0AE3` |
| **Declared size** | Variable; fixed prefix is 28 bytes |
| **Status** | documented |
| **Frequency** | cold |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no |
| **Allocates** | cold packet: may allocate (arrays); see parsing note |

**Hercules usage:** Sent by the login server in the OTP login response flow.

**Description:** Returns the OTP login response and token.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `int16` | 2 bytes | Packet identifier `0x0AE3`. |
| PacketLength | `int16` | 2 bytes | Total packet length, including this header. |
| LoginFlag | `int32` | 4 bytes | Login result flag; Hercules sends zero for its normal-login response. |
| LoginFlag2 | fixed byte array | 20 bytes | Additional login status value; Hercules sends `S1000` in its normal-login response and zero-fills the remaining bytes. |
| Token | variable byte sequence | Remaining bytes | Login token; the current Hercules response writes `token` including a terminating zero byte. |
