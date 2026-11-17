# Backlog

Unrelated issues noticed while working. One line each (file, symptom). Never fixed inline in another change.

- `Uninarok-client/Assets/Scenes/Bootstrap.unity`: references `DevAutoLogin`, whose type is compiled out of release players (SPEC-0001 FR-15), so a release player carries a missing-script component. Strip it from scenes at build time (`IProcessSceneWithReport` or a build preprocessor) when the build is not a development build.
- `Uninarok-client/ProjectSettings/ProjectSettings.asset`: the selected Windows scripting backend (CoreCLR) is not installed in this editor, so `BuildPipeline.BuildPlayer` fails until the module is installed or the backend is changed. Release/dev player builds in this repo need a decision on the backend.
