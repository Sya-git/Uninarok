# CHARACTER_CREATION_SUCCESS

| | |
|---|---|
| **Direction** | Character server to client |
| **Hercules packet name** | `HC_ACCEPT_MAKECHAR` |
| **Packet ID** | `0x0B6F` (`0x006D` before `PACKETVER_MAIN_NUM 20201007`) |
| **Declared size** | 177 bytes, including the 2-byte packet ID |
| **Status** | implemented |
| **Frequency** | cold |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no |
| **Allocates** | no |

**Description:** Reports successful character creation and returns the new character's `CharInfo` record.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Packet identifier `0x0B6F`. |
| CharInfo | `CharInfo` | 175 bytes | Character record parsed from the packet payload; see [`../../../../../data/CharInfo.md`](../../../../../data/CharInfo.md). |

**Parsing:** The constructor receives the payload after the packet ID and passes it to the `CharInfo` parser.

**ID by version (found 2026-11-17 against a real server):** `src/char/packets_hc_struct.h` defines `HC_ACCEPT_MAKECHAR` as `0x0b6f` for `PACKETVER_MAIN_NUM >= 20201007` and as `0x006d` before. `0x0b6f` is 177 bytes in the Hercules 2022 table (`2 + MAX_CHAR_BUF`, with the 175-byte `CharInfo`), so the table needs no correction. A first version of this document used the old ID `0x006d`: against a server built for 20220406 the creation reply (`0x0b6f`) was skipped by length as an unhandled packet and the flow waited in `CreatingCharacter` until its reply timeout. The same version switch also moves `HC_ACK_CHANGE_CHARACTER_SLOT` (`0x0b70`) and `HC_UPDATE_CHARINFO` (`0x0b71`); they are not implemented yet.
