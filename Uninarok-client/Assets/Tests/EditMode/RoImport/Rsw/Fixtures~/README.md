# Map scene test fixtures (SPEC-0006)

Tiny synthetic source files for the map scene tests (TASKS-0006 T5.1). The folder name ends with `~`, so Unity does not import it. `data/` is a fixture data root: the tests copy it into a temporary data folder and add the synthetic `.gnd`, `.gat` and `.rsw` files they build in code.

## Regenerating

```
python make_fixtures.py
```

Standard library only and deterministic. Change the script, never the files by hand.

## Files

| Ragnarok path | Contents | Used for |
|---|---|---|
| `model/외부소품/box.rsm` | RSM 1.4, one node `box`: a 10-unit cube centred on its origin, texture `a.bmp`, no animation | static model instances (prefab instances, static flags, placement) |
| `model/외부소품/spin.rsm` | as `box`, with two rotation keys (identity at 0, a quarter turn about Y at 1000 ms), animation length 1000 | animated instances (`Animator` speed and enabled state, no batching flag) |
| `texture/a.bmp` | BMP 24-bit, 1 x 1, grey | the texture of both models and of the synthetic ground |
