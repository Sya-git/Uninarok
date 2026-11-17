# MOBILE_OTP_REQUEST

| | |
|---|---|
| **Direction** | Login server to client |
| **Hercules packet name** | `AC_REQ_MOBILE_OTP` |
| **Packet ID** | `0x09A2` |
| **Declared size** | 6 bytes, fixed |
| **Status** | documented |
| **Frequency** | cold |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no |
| **Allocates** | no |

**Hercules usage:** Defined by Hercules, but no call site for its send helper was found in the login server source; it is not sent by the current default flow.

**Description:** Requests mobile OTP confirmation for an account.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `int16` | 2 bytes | Packet identifier `0x09A2`. |
| Aid | `uint32` | 4 bytes | Account identifier for the OTP request. |
