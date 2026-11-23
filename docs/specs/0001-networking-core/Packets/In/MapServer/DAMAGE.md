# DAMAGE

| | |
|---|---|
| **Direction** | Map server to client |
| **Hercules packet name** | `ZC_NOTIFY_ACT3` (struct `packet_damage`) |
| **Packet ID** | `0x08C8` |
| **Declared size** | 34 bytes total, including the 2-byte packet ID |
| **Status** | verified |
| **Frequency** | hot |
| **Verified** | ID and size match Hercules 2026.09 length table for PACKETVER 20220406 (checked 2026-11-16); layout from `packets_struct.h` (`packet_damage`) and `clif_damage` (checked 2026-11-22, SPEC-0011 T0.1). Capture: yes, SPEC-0010 `world-prt_fild08.bin` (119: types 0 and 11) |
| **Allocates** | no |

**Hercules usage:** Sent by `clif_damage` to the area (basic attacks and some skill damage). One packet per blow, at its start; Hercules applies the damage `attackMT` later (`delay_battle_damage`). `clif_calc_delay` turns a hit with `attackedMT` 0 and damage > 0 into type 4 (endure), and a multi-hit into 9. `is_sp_damaged` is always 0. Self damage (poison and others) has source = target, both times 0 and often `count` 0; `@heal` sends negative damage the same way; an absorbed blow is -1 (SPEC-0011 H11).

**Description:** A unit attacks or takes damage (also skill damage, healing display, sit/stand animation via `action`).

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Header. |
| GID | `uint` | 4 bytes | Source unit ID. |
| targetGID | `uint` | 4 bytes | Target unit ID. |
| startTime | `uint` | 4 bytes | Server tick of the action. |
| attackMT | `int` | 4 bytes | Attacker motion time in ms (`amotion`, uncapped). |
| attackedMT | `int` | 4 bytes | Target motion time in ms (`dmotion`, sent raw: `WALKDELAY_SYNC` is not defined). |
| damage | `int` | 4 bytes | Damage amount. |
| is_sp_damaged | `byte` | 1 byte | `1` if the damage is to SP. |
| count | `short` | 2 bytes | Number of hits. |
| action | `byte` | 1 byte | Damage type (`enum battle_dmg_type`): `0` normal, `4` endure, `5` splash, `6` skill, `8` multi-hit, `9` endured multi-hit, `10` critical, `11` lucky dodge. A miss is type 0 with damage 0. |
| leftDamage | `int` | 4 bytes | Second-hand damage. |
