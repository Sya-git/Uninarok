# UNIT_VANISH

| | |
|---|---|
| **Direction** | Map server to client |
| **Hercules packet name** | `ZC_NOTIFY_VANISH` |
| **Packet ID** | `0x0080` |
| **Declared size** | 7 bytes total, including the 2-byte packet ID |
| **Status** | documented |
| **Frequency** | hot (every unit leaving view) |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no |
| **Allocates** | no |

**Hercules usage:** Sent by `clif_clearunit_single` / `clif_clearunit_area`.

**Description:** Removes a unit from the client's world.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Header, `0x0080`. |
| UnitId | `uint` | 4 bytes | Object ID of the unit (player, NPC, monster, pet, homunculus). |
| Type | `byte` | 1 byte | Why the unit disappears: `0` out of sight, `1` died, `2` logged out / respawn, `3` teleported, `4` trick dead (`enum clr_type`). |

**Parsing:** read `UnitId` and `Type`. Dead monsters play their death animation on `Type = 1` (the client decides).
