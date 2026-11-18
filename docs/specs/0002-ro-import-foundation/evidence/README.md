# Evidence: SPEC-0002

Manual checks of TASKS-0002, recorded as the tasks ask.

## T7.1 Import window (2026-11-17)

- `T7.1-import-window.png`: `Tools/Ragnarok/Import Window`, source `Assets/Editor/GRF Resources~/data`, kind Texture, path `texture/`, Convert.
  Result: Created 383, Updated 0, Skipped 0, Failed 1. The failure is the planned collision rule (plan 4.5): `texture/izlude/iz_rookie_06.bmp` and `iz_rookie_06.tga` both map to `Art/Imported/texture/izlude/iz_rookie_06.png`; the first in path order (the `.bmp`) is kept and the `.tga` is listed once.
- Script check with the window closed, through the Unity MCP: `RoImport.Texture("data\texture\BACKSIDE.BMP")` returned `Skipped` with `Assets/Art/Imported/texture/backside.png`, the same GUID (`d59e0cf50a0ec0b4ca3bd5a1f776f939`) as the window's output, and the file untouched.
