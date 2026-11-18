# RoImport test fixtures (SPEC-0002)

Tiny source files for the importer tests (TASKS-0002 T0.3). The folder name ends with `~`, so Unity does not import it: no `.meta`, no assets, nothing in builds (FR-15).

`data/` is a fixture **data root**: Ragnarok paths are relative to it, as with `Assets/Editor/GRF Resources~/data`.

## Regenerating

```
python make_fixtures.py
```

The script uses only the Python standard library and is deterministic, so regenerating produces byte-identical files. Change the script, never the files by hand.

## Files

Pixels are listed **top row first**, left to right, as RGB (alpha 255) or RGBA. "Magenta" is the color key `(255, 0, 255)`; "near-key" is `(250, 5, 250)`, within the key tolerance of 10 (plan §4.4).

| Ragnarok path | Format | Size | Pixels | Used for |
|---|---|---|---|---|
| `texture/a.bmp` | BMP 24-bit, bottom-up | 3 x 2 | row 0: magenta, red, magenta. row 1: green, near-key, blue | lookup by `data\texture\A.BMP` etc. (FR-1); 24-bit decode with row padding (odd width); keying incl. tolerance (FR-8) |
| `texture/KEY8.BMP` | BMP 8-bit paletted, 4-color palette | 4 x 4 | magenta border; center 2 x 2: row 1: white, black. row 2: yellow, white | paletted decode; upper-case name on disk as in the real data; keying of a palette color |
| `texture/alpha32.tga` | TGA type 2, 32-bit, 8 alpha bits, bottom-left origin | 2 x 2 | row 0: (255,0,0,255), (0,255,0,128). row 1: (0,0,255,0), (255,0,255,255) | real alpha: `SourceHadAlpha` true, classified `Partial`, the opaque magenta is **not** keyed |
| `texture/rgba.png` | PNG RGBA 8-bit | 2 x 2 | row 0: (255,255,255,255), (0,0,0,64). row 1: (255,0,255,255), (10,20,30,200) | PNG decode; real alpha, not keyed |
| `texture/unsupported.dds` | DDS, uncompressed A8R8G8B8 | 1 x 1 | one green pixel | valid file in an unsupported format: one `UnsupportedTextureFormat` warning and the placeholder |
| `texture/나무잡초꽃/leaf.bmp` | BMP 24-bit | 2 x 2 | row 0: green, magenta. row 1: magenta, green | Korean folder name (NFC) found by lookup (FR-1); green-on-magenta bleed check |
| `texture/유저인터페이스/button.bmp` | BMP 24-bit | 2 x 2 | row 0: white, magenta. row 1: magenta, black | path-based usage: `Ui` (plan §4.4, D8) |
| `texture/corrupt.bmp` | BMP with headers for 16 x 16 x 24-bit | (64 bytes) | headers intact, only 10 of 768 pixel bytes | corrupt input: read fails with a reason, never throws; one `CorruptFile` warning |

Truncated copies of the good files, and folders with names that differ only by case, are made by the tests themselves in a temporary folder (some file systems cannot hold case-only duplicates).

## Checked

Decoded with Windows `System.Drawing` (BMP, PNG): pixels as in the table; `corrupt.bmp` is rejected. TGA and DDS headers checked byte by byte. T0.4 loads every file through the GRF library.
