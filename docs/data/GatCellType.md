# GatCellType

- **Kind:** Shared enum
- **Namespace:** `Uninarok.Map` (runtime assembly `Uninarok.Map`)
- **Underlying type:** `byte`
- **Source of the values:** the `GatType` enum of `GRF.dll` (`GRF.FileFormats.GatFormat.GatType`, `Assets/Editor/Library/GRF.dll`), which is how the GAT file's 32-bit terrain type is defined for the client. The names below keep GRF's names so the two can be compared at a glance.
- **Description:** The terrain type of one GAT cell as stored in the map data asset. The raw GAT value is a 32-bit number; its low bits are the type below, and bit 31 (`0x80000000`) is the **water flag** of GAT 1.3 and later (GRF lists these as `Weird0` to `Weird9`, `0x80000000` plus the type; its separate `SpecialCellTypes.Water` is the same idea). The water flag is stored as a separate cell flag, not as a type.

| Name | Value | Walkable | Snipable | Description |
|---|---:|:---:|:---:|---|
| `Walkable` | `0` | yes | n/a | Regular walkable terrain. |
| `NoWalkable` | `1` | no | no | Obstructed terrain. |
| `NoWalkableNoSnipable` | `2` | no | no | Obstructed terrain that attacks do not pass. |
| `Walkable2` | `3` | yes | n/a | Walkable terrain (a second walkable type; GRF name `Walkable2`). |
| `Unknown` | `4` | no | no | Unknown meaning in GRF; treated as blocked until the data shows otherwise. |
| `NoWalkableSnipable` | `5` | no | yes | Obstructed terrain that attacks and spells pass over (cliff). |
| `Walkable3` | `6` | yes | n/a | Walkable terrain (a third walkable type; GRF name `Walkable3`). |
| `Other` | `255` | no | no | Any other raw value (including the absent cell, raw `-1` / `NoGat`); the raw value is kept in the cell's raw byte and reported once per map. |

## Cell flags (separate from the type)

| Flag | Bit | Description |
|---|---:|---|
| Walkable | 0 | Derived from the type: `Walkable`, `Walkable2`, `Walkable3`. |
| Snipable | 1 | Derived from the type: `NoWalkableSnipable`. |
| Water | 2 | Raw bit 31 (GAT 1.3 and later). A walkable cell with this flag is **walkable water**. |

## Notes

- The set above is read from GRF's own definition; the walkable and snipable columns are the project's reading of the names and are pinned by the `RoGatRulesTests` of SPEC-0005. If the data shows a type behaving differently (checked in T0.2 of TASKS-0005), this file and the table in `RoGatRules` change together.
- Consumers: `MapData` (SPEC-0005), the gizmos of the map data, and gameplay movement rules.
