# ITEM_DISAPPEAR

| | |
|---|---|
| **Direction** | Map server to client |
| **Hercules packet name** | `ZC_ITEM_DISAPPEAR` |
| **Packet ID** | `0x00A1` |
| **Declared size** | 6 bytes total, including the 2-byte packet ID |
| **Status** | implemented |
| **Frequency** | hot |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no |
| **Allocates** | no |

**Hercules usage:** Sent by `clif_clearflooritem`.

**Description:** Removes a dropped item from the ground (picked up or expired).

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Header, `0x00A1`. |
| ItemObjectId | `uint` | 4 bytes | Object ID of the dropped item on the ground (same as `ItemObjectId` in `ITEM_FALL_ENTRY`). |
