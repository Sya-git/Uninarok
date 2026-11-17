# MAP_ENTER_REQUEST_REFUSED

| | |
|---|---|
| **Direction** | Character server to client |
| **Hercules packet name** | no named constant (`chr->auth_error`, `char.cpp`) |
| **Packet ID** | `0x006C` |
| **Declared size** | 3 bytes total, including the 2-byte packet ID |
| **Status** | implemented |
| **Frequency** | cold |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no; layout from `char_auth_error` |
| **Allocates** | no |

**Hercules usage:** Sent by the character server when it refuses the request to enter the map after a character was selected (`SELECT_CHARACTER`).

**Description:** The request to enter the map was refused. In SPEC-0001 (plan 4.9) this is `CharacterManager.MapEnterRefused`: the flow returns from `SelectingCharacter` to `CharSelection` and keeps the reason. It is not a character-server entry refusal and not a failure state.

**Correction (2026-11-17):** earlier text described this as a refusal of entry to the character server (`Failed(CharServerRefused)`); that was wrong.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Header, `0x006C`. |
| ErrorCode | `byte` | 1 byte | Hercules always sends `0` ("rejected from server"). |

**Parsing:** the total size is 3 (header plus 1 byte).
