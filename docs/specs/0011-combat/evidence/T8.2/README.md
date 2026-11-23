# T8.2 Release check (FR-8, FR-11)

Method: `PlayerBuildInterface.CompilePlayerScripts` through the Unity MCP script-execute, target StandaloneWindows64 / group Standalone, `ScriptCompilationOptions.None` (non-development), into a temp folder outside the repo. The produced `Uninarok*.dll` were loaded with `Assembly.LoadFile` and their types listed by reflection (`GetTypes`). No project setting or build target was changed.

```csharp
var s = new ScriptCompilationSettings { group = BuildTargetGroup.Standalone,
  target = BuildTarget.StandaloneWindows64, options = ScriptCompilationOptions.None };
PlayerBuildInterface.CompilePlayerScripts(s, outDir);
// then for each Uninarok*.dll: Assembly.LoadFile(f).GetTypes(); print types whose namespace
// starts with "Uninarok.World.Dev" or whose name is "RestartPromptView"
```

## Result (release, `options = None`)

```
Uninarok.CameraRig.dll types=6
Uninarok.Map.dll types=13
Uninarok.Networking.Core.dll types=22
Uninarok.Networking.dll types=114
Uninarok.SpriteCatalog.dll types=34
Uninarok.Sprites.dll types=12
Uninarok.Units.dll types=54
Uninarok.World.dll types=54
Uninarok.World.UI.dll types=3
  Uninarok.World.UI.RestartPromptView
Uninarok.World.View.dll types=31
```

- `Uninarok.World.Dev.dll` is not produced at all: no type in namespace `Uninarok.World.Dev` (FR-11 holds).
- `Uninarok.World.UI.RestartPromptView` is present (FR-8 holds).

## Positive control (same call with `ScriptCompilationOptions.DevelopmentBuild`)

`Uninarok.World.Dev.dll` is produced with 13 types (`CombatDebugView`, `DevCommandLine`, `DevInventory`, `DevMessages`, `WorldDebugView`, `WorldDevTools` and nested), so the check does detect Dev types when they are compiled in.

Verdict: pass.
