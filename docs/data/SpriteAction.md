# SpriteAction

- **Kind:** Shared enums (animation types of a sprite), one per sprite family
- **Namespace:** decided by the plan that first uses the names (SPEC-0007 addresses animations by number)
- **Underlying type:** `byte`
- **Source of the values:** the ACT files themselves, read through GRF (survey in [act-reference.md](../specs/0007-spr-act-pal-sprites/act-reference.md) §1). Player names follow the owner's reference viewer (Act Editor list); monster names are confirmed by the sound events of `몬스터/poring.act` (walk carries `poring_move.wav`, attack `poring_attack.wav`, hurt `poring_damage.wav`, die `poring_die.wav`).
- **Description:** The animation number of an ACT action. For a directional sprite, **action index = animation × 8 + direction**, the direction being the sprite direction, 0 = front, clockwise ([UninarokPosition](UninarokPosition.md), not the network value); for a non-directional sprite (ground item, emote) the action index is the animation. Files may have fewer animations than the family defines; a missing animation is not an error (SPEC-0007 FR-5 says what is shown instead).

## PcAnimation (player bodies, heads, headgears, garments, weapons, shields)

104 actions = 13 animations × 8 directions in almost every file.

| Name | Value | First action index | Description |
|---|---:|---:|---|
| `Idle` | `0` | 0 | Standing. |
| `Walk` | `1` | 8 | Walking. Action 8 is walk facing front. |
| `Sit` | `2` | 16 | Sitting. |
| `PickUp` | `3` | 24 | Picking up an item. |
| `Standby` | `4` | 32 | Ready stance after being hit or while in combat ("After Hit"). |
| `Attack1` | `5` | 40 | Attack (default). |
| `Hurt` | `6` | 48 | Taking damage ("Hit"). |
| `Freeze1` | `7` | 56 | Frozen pose 1 (one frame). |
| `Dead` | `8` | 64 | Lying dead. |
| `Freeze2` | `9` | 72 | Frozen pose 2 (one frame; heads use one image for all 8 directions). |
| `Attack2` | `10` | 80 | Attack without weapon. |
| `Attack3` | `11` | 88 | Attack with weapon. |
| `Cast` | `12` | 96 | Casting a spell. |

## MonsterAnimation (monsters, most NPCs with more than one animation, homunculi, mercenaries)

| Name | Value | First action index | Description |
|---|---:|---:|---|
| `Idle` | `0` | 0 | Standing. |
| `Walk` | `1` | 8 | Moving. |
| `Attack` | `2` | 16 | Attack. |
| `Hurt` | `3` | 24 | Taking damage. |
| `Die` | `4` | 32 | Dying; holds the last frame. |
| `Extra5`… | `5`+ | 40+ | Present in files with 48, 56, 64 or 72 actions (extra attacks or skills). Meaning per file is not known yet. |

## Single-animation sprites

| Family | Actions | Meaning |
|---|---|---|
| NPC (most) | 8 | `Idle` in 8 directions. |
| Ground item | 1 | One animation, no direction. |
| Emote (`이팩트/emotion`) | 98 | One action per emote; action index = emote id (`ET_*` of `emotionlist.lub`). Candidate. |

## Notes

- Action counts found (whole data set, sampled): players 104 (one body has 112); monsters 40, 64, 8, 48, 56, 72, 10, 16, 1; NPCs 8, 40, 48, 16, 10; homunculi 64, 56.
- A file whose action count is not a multiple of 8 (1, 10, 41, 47 …) is treated as non-directional unless the plan decides otherwise.
- Consumers: the sprite import (SPEC-0007) and the later character, monster and NPC display specs.
