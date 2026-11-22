# Fixtures of SPEC-0010

## Reference paths: `paths-<map>.bin` (T3.4, FR-10)

The paths Hercules itself finds between random cells, written by the plugin `uninarok_pathdump` (`Hercules-2026.09/src/plugins/uninarok_pathdump/`) that calls the server's `path->search` (A* and easy path) and `path->search_long`.

| File | Map | Seed | Pairs | Server |
|---|---|---:|---:|---|
| `paths-prontera.bin` | prontera, 312 × 392 | 1001 | 10,000 | `Hercules-2026.09/` of this repository, sources as of commit `6daf337f8`, debug build of 2026-11-12, `PACKETVER` 20220406, renewal map cache of the client data |
| `paths-izlude.bin` | izlude, 268 × 300 | 1002 | 10,000 | same |
| `paths-prt_fild08.bin` | prt_fild08, 400 × 400 | 1003 | 10,000 | same |

Made on 2026-11-22, per map (from `Hercules-2026.09/`; the server runs in minimal mode, needs no database and exits):

```powershell
bin/map-server.exe --load-plugin uninarok_pathdump --uninarok-paths prontera,1001,10000,../docs/specs/0010-world-session/fixtures/paths-prontera.bin
```

Results of the runs: prontera 6,791 found, 371 without a path to a passable cell, 2,564 too long; izlude 5,490 / 66 / 1,794; prt_fild08 8,224 / 218 / 2,512.

Build: `ninja plugin.uninarok_pathdump` in `Hercules-2026.09/build` (Visual Studio environment), then copy `build/src/plugins/uninarok_pathdump/uninarok_pathdump.dll` to `bin/herc-plugins/`. The plugin is not in `conf/plugins.conf`: a normal start does not load it.

Format (little-endian): `UNPF`, version 1 (`u32`), map name (16 B), width, height (`u16`), seed, pairs (`u32`), FNV-1a over one byte per cell, 1 = passable (`u32`). Per pair: x0, y0, x1, y1 (`u16`), flags (`u8`: 1 destination passable, 2 A* found, 4 easy path found, 8 straight line free, 16 the server accepts the walk: 17 cells, 14 without a straight line), A* length, easy length (`u8`), the A* steps, the easy steps (directions 0 to 7, one byte each).
