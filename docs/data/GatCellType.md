# GatCellType

- **Kind:** Shared enums (`MapCellType`, `MapCellFlags`)
- **Namespace:** `Uninarok.Map` (runtime assembly `Uninarok.Map`)
- **Underlying type:** `byte`
- **Source of the values:** the Hercules map server, `map_gat2cell` in `src/map/map.cpp` (the Hercules tree in this repository), which turns the GAT file's 32-bit terrain type into the cell flags the server checks movement and attacks against. The names are the server's: its cell flags `walkable`, `shootable` and `water` (`struct mapcell`), and its cell checks and comments for the types (walkable ground, wall `CELL_CHKWALL`, walkable water `CELL_CHKWATER`, cliff `CELL_CHKCLIFF`). Types 2, 4 and 6 have no name on the server ("???") and are called `Type2`, `Type4`, `Type6`. Decided by the owner in SPEC-0005 (plan D2, 2026-11-18).
- **Description:** The terrain type of one GAT cell as stored in the map data asset, and the flags derived from it. The raw GAT value is a 32-bit number. Values 0 to 6 are the types below; any other value (the absent cell `-1`, a value above 6, or a value with bit 31 set, which GRF calls `Weird0` to `Weird9` and the format document calls the water flag of GAT 1.3) is "unrecognized" on the server and gets no flag.

## MapCellType

| Name | Value | Walkable | Shootable | Water | Description |
|---|---:|:---:|:---:|:---:|---|
| `Walkable` | `0` | yes | yes | no | Walkable ground. |
| `Wall` | `1` | no | no | no | Wall: neither walking nor attacks pass. |
| `Type2` | `2` | yes | yes | no | No name on the server; behaves as walkable ground. |
| `Water` | `3` | yes | yes | yes | Walkable water. |
| `Type4` | `4` | yes | yes | no | No name on the server; behaves as walkable ground. |
| `Cliff` | `5` | no | yes | no | Cliff or gap: not walkable, attacks and spells pass over (snipable). |
| `Type6` | `6` | yes | yes | no | No name on the server; behaves as walkable ground. |
| `Other` | `255` | no | no | no | Any other raw value; the raw value's low byte is kept in the cell's raw byte, and the map reports `GatUnknownType` once with the values and a count. |

## MapCellFlags (stored per cell)

| Flag | Bit | Description |
|---|---:|---|
| `Walkable` | 0 | The server's `walkable`: a character can stand on and move through the cell. |
| `Shootable` | 1 | The server's `shootable`: ranged attacks and spells pass over the cell. |
| `Water` | 2 | The server's `water`: the cell is water (`Water` type). |

## Notes

- The table is `map_gat2cell` exactly and is pinned by the `RoGatRulesTests` of SPEC-0005. If the server's table changes, this file, `RoGatRules` and the tests change together.
- The server also marks a `Walkable` (type 0) cell as water when its first height is below the map's water level from the RSW (`map_readgat`). That rule needs the RSW and belongs to SPEC-0006.
- The reference maps (prontera, izlude, prt_fild08, all GAT 1.2) use only types 0, 1 and 5.
- Consumers: `MapData` (SPEC-0005), the gizmos of the map data, and gameplay movement rules.
