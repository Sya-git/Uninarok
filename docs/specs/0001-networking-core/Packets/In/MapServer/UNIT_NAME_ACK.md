# UNIT_NAME_ACK

| | |
|---|---|
| **Direction** | Map server to client |
| **Hercules packet name** | `ZC_ACK_REQNAMEALL` (`ZC_ACK_REQNAMEALL2`) |
| **Packet ID** | `0x0A30` |
| **Declared size** | 106 bytes total, including the 2-byte packet ID |
| **Status** | documented |
| **Frequency** | warm |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no; layout from `packets_struct.h` (`PACKET_ZC_ACK_REQNAMEALL`) |
| **Allocates** | no |

**Hercules usage:** Sent by `clif_pcname_ack`. Non-player units are answered with `ZC_ACK_REQNAME` (0x0095, 30 bytes: `UnitId` + 24-byte name) or the NPC variant.

**Description:** Reply to `REQUEST_UNIT_NAME` for a player: name plus party, guild and guild position names.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Header. |
| gid | `int` | 4 bytes | Object ID asked about. |
| name | fixed ASCII string | 24 bytes | Character name. |
| party_name | fixed ASCII string | 24 bytes | Party name (empty if none). |
| guild_name | fixed ASCII string | 24 bytes | Guild name (empty if none). |
| position_name | fixed ASCII string | 24 bytes | Guild position (empty if none). |
| title_id | `int` | 4 bytes | Title ID. |
