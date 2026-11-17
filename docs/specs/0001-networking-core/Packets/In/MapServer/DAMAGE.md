# DAMAGE

| | |
|---|---|
| **Direction** | Map server to client |
| **Hercules packet name** | `ZC_NOTIFY_ACT3` (struct `packet_damage`) |
| **Packet ID** | `0x08C8` |
| **Declared size** | 34 bytes total, including the 2-byte packet ID |
| **Status** | documented |
| **Frequency** | hot |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16). Capture: no; layout from `packets_struct.h` (`packet_damage`) |
| **Allocates** | no |

**Hercules usage:** Sent by `clif_damage` and skill code.

**Description:** A unit attacks or takes damage (also skill damage, healing display, sit/stand animation via `action`).

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Header. |
| GID | `uint` | 4 bytes | Source unit ID. |
| targetGID | `uint` | 4 bytes | Target unit ID. |
| startTime | `uint` | 4 bytes | Server tick of the action. |
| attackMT | `int` | 4 bytes | Attacker motion time (ms). |
| attackedMT | `int` | 4 bytes | Target motion time (ms). |
| damage | `int` | 4 bytes | Damage amount. |
| is_sp_damaged | `byte` | 1 byte | `1` if the damage is to SP. |
| count | `short` | 2 bytes | Number of hits. |
| action | `byte` | 1 byte | Action type (`0` normal, `8` multi-hit, `10` critical, ...). |
| leftDamage | `int` | 4 bytes | Second-hand damage. |
