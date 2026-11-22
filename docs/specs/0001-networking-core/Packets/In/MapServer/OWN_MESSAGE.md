# OWN_MESSAGE

| | |
|---|---|
| **Direction** | Map server to client |
| **Hercules packet name** | `ZC_NOTIFY_PLAYERCHAT` |
| **Packet ID** | `0x008E` |
| **Declared size** | Variable (`Size = -1`) |
| **Status** | implemented |
| **Frequency** | cold |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-22). Capture: no; layout from `clif_displaymessage`. Real server 2026-11-22 (Hercules built for 20220406): sent and received on the local server (SPEC-0010 T8.6). |
| **Allocates** | cold packet: it allocates its text |

**Hercules usage:** Sent by `clif_displaymessage` (answers of commands, notices) and with the own chat line.

**Description:** A line for the own chat window.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Header, `0x008E`. |
| PacketLength | `ushort` | 2 bytes | Total length. |
| Message | text | n bytes | NUL-terminated text. Read as ASCII so far. |
