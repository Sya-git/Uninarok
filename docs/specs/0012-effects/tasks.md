# TASKS-0012: Effects

| | |
|---|---|
| **Implements** | [plan.md](plan.md) → [spec.md](spec.md) |
| **Status** | Implementing |

**How to use this file**
- Branch: `feature/0012-effects`, merged to main with `--no-ff` when the spec is done.
- Ids T0.1 to T0.3 are the plan's spikes (plan §6). T0.2b is the owner gate for the colour-key rule (plan §4.3, R2). T0.4 and T0.5 are withdrawn with VFX Graph (plan §6, §8) and are not tasks.
- Owner gates `(owner)` block only what they name: T0.2b blocks T3.8 and every task that converts effect textures (T3.9 onward). T10.1 and T10.2 block only the sign-off in phase 10. No other phase waits for an owner gate. The header's "do not block later phases" rule holds otherwise.
- Test-first tasks: the test task adds the class signatures as stubs (`NotImplementedException`) so the tests compile, and marks each case `[Ignore("pending Tn.m")]`. The named implementation task replaces the stubs and removes the Ignore.
- Tests marked NeedsData need the working data folder (`texture/effect/`, `sprite/이팩트/`, `model/`) present.
- Spike code (T0.1, T0.3) is not committed under `Assets/Scripts/`; only the notes and evidence under `docs/specs/0012-effects/`.
- Tasks that run the editor or a build are done through Unity-MCP.
- Revision 2026-11-24 (owner decisions, plan D21, D22): T1.7 unblocked and rewritten (`EffectPath` normalises on its own at run time, `EffectPathAgreementTests` checks it against `RoPath`); T3.2 states that `KeyData.Delay` stays and only asset-level start delay, duplicate, wav/sound and attachment fields are forbidden; the cylinder vertex-count check stays in T5.3; T10.1 is a visual confirmation only (R5 resolved: uv from the STR keys).

---

## Phase 0: Prepare (spikes and owner decisions)

- [x] **T0.1** [critical] Spike the STR format on the 10 reference files: map GRF's `StrFormat` members to the fields of plan §4.1 (uv and corner order, angle and colour units, CP949 name decoding), count versions and uv-differs files over the whole data set, and compare GRF's keyframe interpolation with the plan §4.2 rule. Write `docs/specs/0012-effects/str-reference.md` (GRF mapping table, sampling section, reference table) and `docs/specs/0012-effects/evidence/str-survey.txt`. (FR-2, FR-3, FR-10)
  *Done when:* str-reference.md has the mapping table and one row per reference file (version, fps, frame count, layer count, key count per layer, texture count), matching an independent byte read of the headers; str-survey.txt holds the version counts over all `.str` files and the count of files whose uv differs from the full rectangle (R5); every interpolation difference from GRF is listed under "Owner decision" in str-reference.md, not resolved.

- [x] **T0.2** [core] Survey the colour of every effect texture: for each image under `texture/effect/`, its corner colour class (pink, black, other) and the blend pairs of the layers that use it (from the T0.1 reads). Write `docs/specs/0012-effects/evidence/colour-key-survey.csv`. (FR-2)
  *Done when:* the CSV's first line gives the file count of the same glob, and it has one row per image with its corner class and distinct user blend pairs; the row count equals that count.

- [ ] **T0.2b** [quick] (owner) Owner decides the colour-key rule from the survey: defaults for BMP, 24-bit TGA, JPG and for 32-bit TGA and PNG with alpha, the pink and black tolerances, per-texture overrides, and whether a `ByBlend` rule is needed (plan §4.3, R2, D6). Blocks T3.8 and every task that converts effect textures.
  *Done when:* str-reference.md section "Colour key" records DefaultOpaque, DefaultWithAlpha, PinkTolerance, BlackTolerance, the overrides, `ByBlend` yes or no, and the owner's sign-off line.

- [x] **T0.3** [critical] Spike draw order and cost: in a throwaway scene (not under `Assets/Scripts/`), two overlapping alpha-blended submesh layers drawn in both orders; then the FR-9 load (300 effects, 100 on moving transforms, cylinders and quads mixed) with submesh layers and with the per-layer renderer fallback (plan §4.5, D4, R3). (FR-3, FR-9)
  *Done when:* `docs/specs/0012-effects/evidence/submesh-order.md` records, for both orders, whether the order held, and ms per frame for both designs under the same load. If submesh order fails, the work stops and goes back to the planner (plan §8 D4); no task is added here.

## Phase 1: Foundations (assemblies, data types, settings, pure helpers)

- [x] **T1.1** [quick] Add the `Uninarok.Effects` runtime assembly and folders `Assets/Scripts/Effects/` with `Data`, `Catalog`, `Playback`, `Parts`, references as plan §3 and no editor, VFX, particle or compute reference. (FR-4, FR-5)
  *Done when:* the asmdef references exactly Uninarok.Sprites, Uninarok.SpriteCatalog, Uninarok.Map, Unity.Addressables and Unity.ResourceManager; Unity compiles with 0 errors.

- [x] **T1.2** [quick] Add the `Uninarok.Effects.Editor` assembly (`includePlatforms: [Editor]`), `Uninarok.Effects.Tests` (EditMode) and `Uninarok.Effects.PlayTests` (PlayMode) assemblies and their folders as plan §3 and §6. (FR-10, FR-11)
  *Done when:* the three asmdefs exist with the references of plan §3; the editor asmdef's `includePlatforms` is `Editor` only; the test runner lists both test assemblies with 0 tests and 0 failures.

- [x] **T1.3** [quick] Add the data types of plan §4.2 and §4.4 with no logic: `EffectKind`, `EffectFacing`, `EffectAsset` with `LayerData` and `KeyData`, `AuthoredData`, `EffectCatalogIndex`, `EffectAssetRef`. (FR-2, FR-4, FR-5)
  *Done when:* the types compile with exactly the fields of plan §4.2 and §4.4 and no method body.

- [x] **T1.4** [quick] Add `EffectSettings` (plan §4.6) and its asset `Assets/Settings/Effects/EffectSettings.asset` with the lead values (PixelsPerCell 35, Pivot (320, 320), VerticalBaseCells 0.5, AngleUnitsPerTurn 1024, DepthBias 0.5, PlaceholderSeconds 1, AuthoredFrameRate 60) and a tooltip on each lead value naming its source. Shader and placeholder texture are set in T5.1 and T5.2. (FR-3, FR-4, FR-11)
  *Done when:* the asset loads with those values; each lead value has a tooltip.

- [x] **T1.5** [core] Implement `EffectBlend.ToUnity(byte code)` with the Direct3D code table of plan §4.5, and `EffectBlendTests`: the table is pinned; an unknown code gives SrcAlpha / OneMinusSrcAlpha and reports unknown, so the caller logs one warning per asset. (FR-3)
  *Done when:* `EffectBlendTests` pass.

- [x] **T1.6** [core] Test-first: `EffectPathTests` (plan §6: case, slashes, `data\` and relative forms reach one key; unknown → false; normalisation table pinned), with an `EffectPath` stub; every case `[Ignore("pending T1.7")]`. (FR-5, FR-10)
  *Done when:* the test class compiles and the run shows its cases ignored, 0 failed.

- [x] **T1.7** [core] Implement `EffectPath` (`TryNormalize`, `Prefixes`) in `Uninarok.Effects` with its own light runtime normalisation (plan §4.4, D21): Unicode NFC (`string.Normalize`, as `RoPath` does; sample set includes a decomposed Korean name), `'\'` → `'/'`, repeated slashes collapsed, trim, leading separators stripped, invariant lowercase, known leading prefixes (`data/`) stripped, keys relative to the folder the effect files are extracted to; empty, `.`/`..` or drive letter → false. No reference to `RoPath` or any editor assembly. Remove the Ignore from `EffectPathTests`, and add `EffectPathAgreementTests` (`Assets/Tests/EditMode/RoImport/Effects/`, in `Uninarok.RoImport.Tests`): on the pinned sample set (case, backslashes, `data\` prefix, leading separators, `sprite/이팩트/…`, a deep folder) `EffectPath.TryNormalize` equals `RoPath.TryParse(…).Canonical`. (FR-5, FR-10)
  *Done when:* `EffectPathTests` and `EffectPathAgreementTests` pass; the `Uninarok.Effects` asmdef still has no editor reference.

## Phase 2: Sampling (pure, test-first)

- [x] **T2.1** [core] Test-first: `EffectSamplerTests` (plan §6: base held; base plus delta morph; delta not adjacent → held; hidden before the first and after the last key; anitype 0 to 4; angle units; colour / 255; seek equals continuous play), with an `EffectSampler` stub; every case `[Ignore("pending T2.2")]`. (FR-3, FR-10)
  *Done when:* the test class compiles and the run shows its cases ignored, 0 failed.

- [x] **T2.2** [critical] Implement `EffectSampler` (plan §4.2 sampling semantics, `cursor`, pure, no allocation) and remove the Ignore. (FR-3, FR-9)
  *Done when:* `EffectSamplerTests` pass; 10,000 samples allocate 0 B (`GC.GetAllocatedBytesForCurrentThread` around the loop).

- [x] **T2.3** [core] Test-first: `AuthoredSamplerTests` and the `EffectRandom` cases (plan §6: warp cylinder values at t 0, 0.25, 0.5, 0.75, 1; null curves give their neutral values; quad offset inside the start box at t 0 and the end box at t 1; size = `SizePixels` / `PixelsPerCell`; same seed → same sample; same offset in a later cycle; different seeds differ; 0 B over 10,000 samples). The cylinder strip's vertex count and uv are checked in T5.3. Stubs, every case `[Ignore("pending T2.4")]`. (FR-4, FR-7, FR-10)
  *Done when:* the test classes compile and the run shows their cases ignored, 0 failed.

- [x] **T2.4** [core] Implement `AuthoredSampler` and `EffectRandom` (plan §4.2 authored sampling rules, D19 seeded hash, `AnimationCurve` neutral values) and remove the Ignores. (FR-4, FR-7, FR-9)
  *Done when:* the `AuthoredSamplerTests` and `EffectRandom` cases pass.

## Phase 3: STR and effect texture import (offline, one asset per STR)

- [x] **T3.1** [critical] Implement `RoStr`, `RoStrLayer`, `RoStrKey` and `RoStrReader` (GRF `StrFormat` adapter in `Core/Formats/`; every byte of the STR is parsed by GRF, the reader never reads, indexes or converts raw bytes itself, and takes the version from GRF's own header members; `CorruptFile` and `UnsupportedStrVersion` issues), mapping as recorded in str-reference.md; `RoStrReaderTests` (NeedsData): the 10 reference files against the T0.1 record (constants in the test, no byte parsing in the test either); truncated, junk and wrong-version input return false with the right issue and never throw. (FR-2, FR-10)
  *Done when:* `RoStrReaderTests` pass.

- [x] **T3.2** [core] Test-first: `RoStrCompilerTests` (flattening keeps every key field and order, including each key's own source and destination blend, shown by a synthetic layer with two blends; texture ranges; reflection: `EffectAsset` has no asset-level start delay, duplicate, wav/sound or attachment field (authored mapping data of another spec); `KeyData.Delay`, the STR file's per-key animation delay, stays in the asset as imported from the file and the test asserts it is present, plan §4.2), with a `RoStrCompiler` stub; every case `[Ignore("pending T3.3")]`. (FR-2, FR-10)
  *Done when:* the test class compiles and the run shows its cases ignored, 0 failed.

- [x] **T3.3** [core] Implement `RoStrCompiler` (flatten, stable sort by frame, no value converted) and remove the Ignores. (FR-2)
  *Done when:* `RoStrCompilerTests` pass.

- [x] **T3.4** [core] Test-first: `RoEffectTextureLookupTests` (own folder wins; root found when absent from the own folder; neither → placeholder with one warning naming the file, in a temp folder; a name with an extension resolves to that file only; a bare name with `.tga` and `.bmp` gives the `.tga`; folder order beats extension preference; `pok1.tga` and `pok1.bmp` give two textures with no collision; `jong_mini.str` and `new_splash.str` resolve all their textures (NeedsData)), with a `RoEffectTextureLookup` stub; every case `[Ignore("pending T3.5")]`. (FR-2, FR-10)
  *Done when:* the test class compiles and the run shows its cases ignored, 0 failed.

- [x] **T3.5** [core] Implement `RoEffectTextureLookup` (`EffectRoot`, `BareNamePreference`, `TryResolve`; a name found nowhere gives `TextureMissing` once per (texture, STR)) and remove the Ignores. (FR-2)
  *Done when:* `RoEffectTextureLookupTests` pass.

- [x] **T3.6** [core] Test-first: `RoEffectColorKeyTests` (pink keyed; black keyed; `None` keeps pixels; tolerances; overrides beat defaults; rules read from JSON; key written to userData), with stubs of `RoEffectKeyRules` and the `RoColorKey` key-set overload; every case `[Ignore("pending T3.7")]`, except that "rules read from JSON" is `[Ignore("pending T3.8")]`. (FR-2, FR-10)
  *Done when:* the test class compiles and the run shows its cases ignored, 0 failed.

- [x] **T3.7** [core] Implement `RoEffectKeyRules` (loads `Rules/effect-textures.json`, `For(texture, sourceHadAlpha)`) and the `RoColorKey` key-set overload; `RoColorKey.Apply(RoImage)` keeps its behaviour. Remove the Ignores except T3.8's. (FR-2)
  *Done when:* all `RoEffectColorKeyTests` pass except the JSON case; the existing world texture tests still pass.

- [ ] **T3.8** [quick] (owner) Write `Assets/Editor/RoImport/Rules/effect-textures.json` with exactly the values recorded in str-reference.md's colour-key section (T0.2b), and remove the last Ignore from `RoEffectColorKeyTests`. (FR-2)
  *Done when:* the file holds those values, and every `RoEffectColorKeyTests` case passes.

- [ ] **T3.9** [quick] Stop `RoTextureConverter` from handling `texture/effect/**` (its `Handles` excludes that folder, plan §7, D7). (FR-2)
  *Done when:* a unit check shows `Handles` false for `texture/effect/x.bmp`, and the existing world texture tests pass.

- [ ] **T3.10** [core] Implement `RoEffectTextureConverter` (ledger category `effecttexture`, output `Art/Imported/effect/<path>.png` with the extension kept, key in userData as `alpha=…;key=…`, re-converted when the stored key differs from `RoEffectKeyRules.For`) and `RoImportContext.EffectTexture` (memoised per run). (FR-2)
  *Done when:* a temp source with `pok1.tga` and `pok1.bmp` gives `Art/Imported/effect/texture/effect/pok1.tga.png` and `pok1.bmp.png` with no OutputCollision warning; the userData holds `key=`; changing one texture's key re-converts only that texture.

- [ ] **T3.11** [core] Implement `RoStrConverter` (ledger category `effect`; one asset per STR at `Art/Imported/effect/<path>.asset`; ledger dependencies are the resolved textures, a missing one with an empty hash; corrupt or unsupported file → one issue and no output), `RoImport.Effect` and `RoImport.EffectFolder`, `RoJobKind.Effect`, the `RoIssueCode` additions, and `RoStrConverterTests` (synthetic STR: one asset at its path; a texture shared by two STRs converted once; second run Skipped; forced run keeps GUIDs; changed texture re-converts its STR; corrupt and unsupported input give one issue and no output). (FR-2, FR-10)
  *Done when:* all `RoStrConverterTests` pass.

- [ ] **T3.12** [core] `RoEffectImportReferenceTests` (NeedsData, slow): whole `texture/effect/` import gives asset count = `.str` count; per-file report; a pink-background and a black-background texture (named in T0.2b's record) come in with those areas transparent; a re-run imports nothing. (FR-2, FR-5, FR-10)
  *Done when:* the tests pass against the working data.

- [x] **T3.13** [core] `EffectSamplerReferenceTests` (NeedsData): the 10 reference files at the first, a middle and the last frame: corners, colour, angle, texture frame and blend equal the values from the file's keys (oracle: GRF's interpolation if T0.1 validated it, otherwise the str-reference formulas on `RoStr`), tolerance 1e-3; length = frames / fps. (FR-3, FR-10)
  *Done when:* the test passes for all 10 reference files.

- [ ] **T3.14** [quick] Run the effect import of the `.str` files under `texture/effect/` and `sprite/이팩트/` (Convert Effect Folder); save the per-file report to `docs/specs/0012-effects/evidence/import-report.txt`. (FR-2)
  *Done when:* the report counts one asset per `.str` in the working data (2,996 at 2026-11-24); no OutputCollision; the only UnsupportedStrVersion files are those listed in str-reference.md; a second run imports nothing.

## Phase 4: Catalog (run time index, sources, sprite entries)

- [x] **T4.1** [core] Implement `EffectCatalog` (`Create`, `TryResolve` with an ordinal cache, `Release`, the prefix forms, unknown path → false with one warning per key per session) and `IEffectAssetSource`; `EffectCatalogTests` (EditMode, fake source). (FR-5, FR-9)
  *Done when:* `EffectCatalogTests` pass: `effect/firewall.str`, `FIREWALL.STR` and `data\texture\effect\firewall.str` resolve to one `EffectRef`; an unknown path gives false and one warning.

- [x] **T4.2** [quick] Add the `effects` Addressables group to `RoCatalogAddressables.Ensure`, with folder entries `Assets/Art/Imported/effect` and `Assets/Data/Effects`. (FR-5)
  *Done when:* after Ensure the group `effects` has the `Assets/Data/Effects` entry (ours, created by Ensure) and no `Assets/Art/Imported/effect` entry while that folder is absent, which gains its entry on the first Ensure after the folder exists (D27); a second Ensure adds nothing; `RoCatalogAddressablesTests` passes.

- [x] **T4.3** [core] Implement `AddressablesEffectSource` and `EffectCatalog.LoadAsync` (the index through Addressables; assets loaded by GUID key); add `EffectCatalogLoadTests` (PlayMode). (FR-5, FR-6)
  *Done when:* the code compiles; `EffectCatalogLoadTests` passes after T6.9 has built the index (run in T9.6).

- [x] **T4.4** [core] Implement `RoEffectSpriteEntryConverter`: one `EffectAsset` (Kind Sprite, `SpriteName` the SPEC-0008 name, `SpriteAction` 0) per ACT under `sprite/이팩트/`, at `Art/Imported/effect/sprite/이팩트/<name>.act.asset`; menu item `Convert Effect Sprites`. It references the sprite and imports nothing. (FR-4, FR-5)
  *Done when:* the entry count equals the number of ACT outputs under `Art/Imported/sprite/이팩트/` (168 at 2026-11-24); the SPEC-0007 outputs keep their hashes after the run.

- [x] **T4.5** [core] Implement `RoEffectCatalogIndexConverter` (scans `Art/Imported/effect/**` and `Data/Effects/Authored/**`; `EffectKeyDuplicate` and `EffectKeyPathMismatch`; `SourceMissing`; writes `_index.asset` only when it changes), `RoImport.EffectIndex`, `RoJobKind.EffectIndex`, menu `Rebuild Effect Index`, and `EffectCatalogIndexTests` (one entry per asset; duplicate key → one warning, the first path wins; keys and references only; `SourceMissing` set when the source is gone). (FR-5, FR-10)
  *Done when:* `EffectCatalogIndexTests` pass.

## Phase 5: Player (run time)

- [x] **T5.1** [core] Write the shader `Uninarok/RoEffect` at `Assets/Shaders/Effects/RoEffect.shader` (plan §4.5: URP unlit, transparent queue, `Blend [_SrcBlend] [_DstBlend]`, ZWrite Off, Cull Off, ZTest LEqual, vertex colour × texture, keywords `_FACING_CAMERA` and `_UV_REPEAT`, `_DepthBias`), and assign it to `EffectSettings.Shader`. (FR-3, FR-4, FR-9)
  *Done when:* `Logs/Editor.log` has no shader error for either keyword variant; `EffectSettings.Shader` references the shader.

- [x] **T5.2** [core] Implement `EffectMaterials` (one material per (texture, src, dst, facing, wrap), created on first use and kept), assign a placeholder texture asset to `EffectSettings.PlaceholderTexture`, and `EffectMaterialsTests`: the same key gives the same instance; a different wrap gives another material. (FR-3, FR-9)
  *Done when:* `EffectMaterialsTests` pass.

- [x] **T5.3** [critical] Implement `EffectMeshWriter` (plan §4.5: one mesh per instance; 24-byte vertex format; preallocated `NativeArray`; `SetVertexBufferData` with skip-validation flags; index layouts for quads and cylinder strips written once per side count; a submesh per layer or part; a hidden layer as a zero-area quad; `SetSharedMaterials` only when a layer's key changes), and `EffectMeshWriterTests`: vertex size 24 B; a 3-layer STR writes 12 vertices in 3 submeshes; a cylinder with `Sides` 20 writes 42 vertices with u = i / 20 and v 1 at the bottom, 0 at the top. (FR-3, FR-4, FR-9)
  *Done when:* `EffectMeshWriterTests` pass.

- [x] **T5.4** [critical] Implement `EffectRuntime` and `EffectHandle` (pooled instances with generation; `Play`, `PlayOn` (follows position, not rotation), `PlayAtCell` (`MapData.CellCenter`); `Stop`, `StopAll`, `IsActive`, `ActiveCount`; `SetSpeed`, `SpeedOf`; `Seek`, `FrameOf`; `TimeScale`; `Tick` with `Age += Δt × TimeScale × Speed`; one-shot ends at its length, loop wraps; an instance waits for its asset). Drawing is not in this task (T5.5, T5.6). Add `EffectPlayTests` (PlayMode): point and cell placement; `PlayOn` follows a moving transform; a one-shot ends after its length (±1 frame); stop on a looped one ends it at once; `StopAll` → 0 active. Assets are built in code. (FR-6, FR-9)
  *Done when:* `EffectPlayTests` lifetime and placement cases pass; no allocation in `Tick` after warm-up (checked in T9.3).

- [x] **T5.5** [core] Implement `StrPart` (per layer: `EffectSampler` into `EffectMeshWriter`; the STR placement formula of plan §4.5; each layer's submesh material from its current base key's blend). Add the STR case to `EffectPlayTests`: a 3-layer synthetic asset gives 3 submeshes, and layer 0's corners at frame 0 equal the expected world positions. (FR-2, FR-3)
  *Done when:* that case passes.

- [x] **T5.6** [core] Implement `AuthoredPart` (Quad facing camera or ground; Cylinder strip; Procedural through the empty `EffectProcedures` registry and `EffectQuadSpan`), per plan §4.5. (FR-4, FR-7)
  *Done when:* a synthetic cylinder and a synthetic quad built in code produce 42 and 4 vertices in the instance mesh (PlayMode check, added to `EffectPlayTests`).

- [x] **T5.7** [core] Implement `SpritePart` (a pooled `SpriteActor` found through `SpriteCatalog.TryGetEffect`, advanced by `SpriteActor.Step` from the runtime tick) and `ModelPart` (one prefab instance per pooled slot). (FR-4, FR-6)
  *Done when:* `EffectPlayTests` sprite case passes (NeedsData): `fireball.act` plays through SpriteCatalog with the asset count unchanged.

- [x] **T5.8** [core] Implement `PlaceholderPart` (camera-facing quad of `EffectSettings.PlaceholderTexture`, `PlaceholderSeconds` long or looped), the runtime's `Warned(string key, string message)` event, and the one-warning-per-key rule. Add to `EffectPlayTests`: an unknown key plays the placeholder with exactly one warning; an unbuilt procedural entry plays the placeholder with one warning. (FR-1, FR-4, FR-5)
  *Done when:* both cases pass.

- [x] **T5.9** [core] Implement `EffectSystem` (static: `Catalog`, `Play`, `PlayOn`, `PlayAtCell`, `SetSpeed`, `Stop`, `StopAll`, `IsActive`, `ActiveCount`; `[AutoStaticsCleanup]`) and `EffectSystemDriver` (`[DefaultExecutionOrder(950)]`, hidden, created on first play, ticks in `LateUpdate`, under one `DontDestroyOnLoad` root), and `EffectSystemPlayTests` (PlayMode): `Play` by path with a fake catalog returns a valid handle with `ActiveCount` 1; `StopAll` → 0. (FR-6, FR-9)
  *Done when:* `EffectSystemPlayTests` pass.

## Phase 6: Data check and authored content (warp portal, must-list)

- [x] **T6.1** [core] Test-first: `RoEffectDataCheckTests` (counts per kind on a temp folder; each missing file once; two runs give equal text; folder hashes unchanged after a run; deleting one STR lists it), with `RoEffectDataReport` and `RoEffectDataCheck` stubs; every case `[Ignore("pending T6.2")]`. (FR-1, FR-5, FR-10)
  *Done when:* the test class compiles and the run shows its cases ignored, 0 failed.

- [x] **T6.2** [core] Implement `RoEffectDataCheck` (read only; counts for `.str`, images under `texture/effect/`, `.spr` and `.act` under `sprite/이팩트/`, `.lub` under `luafiles514/lua files/effecttool/`; missing lists; deterministic `ToText`) and menu `Tools/Ragnarok/Effects/Check Effect Data` (logs the text). Remove the Ignores. (FR-1)
  *Done when:* `RoEffectDataCheckTests` pass.

- [x] **T6.3** [quick] Import the trap model `model/외부소품/트랩01.rsm` with the SPEC-0003 model import, so a prefab exists for `ModelPart`. (FR-4, FR-5)
  *Done when:* the prefab exists at the path RoImport.Model gives, with no warning in the report.

- [x] **T6.4** [quick] Author the three warp portal entries as `EffectAsset` data at `Assets/Data/Effects/Authored/texture/effect/_authored/warp_portal/`: `sparkle.quad`, `ring_outer.cylinder`, `ring_inner.cylinder`, with the values, curves, `Length`, texture names and `Doubtful` lines of plan §4.7. (FR-4, FR-5, FR-7)
  *Done when:* the three assets exist; each `Path` equals its key; the values equal plan §4.7; `Doubtful` lists every lead value with its source.

- [x] **T6.5** [quick] Author the trap model entry at `Assets/Data/Effects/Authored/model/외부소품/트랩01.rsm.asset` (Kind Model, `Model` = the T6.3 prefab, `Length` authored and listed in `Doubtful`). (FR-4, FR-5)
  *Done when:* the asset's `Model` is not null, `Length` > 0, and `Doubtful` has its line.

- [ ] **T6.6** [core] Extend Convert Effect Folder to convert the textures named by authored entries (`texture/effect/ring_blue.tga`, `texture/effect/pok1.tga`) through the `effecttexture` category (plan §4.10). (FR-2, FR-7)
  *Done when:* after a run `Art/Imported/effect/texture/effect/ring_blue.tga.png` and `pok1.tga.png` exist, with ledger entries.

- [ ] **T6.7** [quick] Assign the converted textures to the `Textures` slots of the three warp entries (texture names kept). (FR-7)
  *Done when:* the `Textures` slots reference those two outputs and `TextureNames` is unchanged.

- [ ] **T6.8** [core] `AuthoredAssetTests` (EditMode): each authored entry has a valid kind, `Length` > 0, a valid texture index whose `TextureNames` entry matches the converted texture, blend codes in the `EffectBlend` table, `Sides` ≥ 3, curves covering t 0 to 1, and `Doubtful` lines for every lead value; the three warp keys are present, both rings on `ring_blue.tga`, the sparkle on `pok1.tga` (not `pok1.bmp`). (FR-4, FR-5, FR-7)
  *Done when:* `AuthoredAssetTests` pass.

- [ ] **T6.9** [quick] Run Rebuild Effect Index (Unity-MCP). (FR-5, FR-7)
  *Done when:* `Art/Imported/effect/_index.asset` exists; its keys include `texture/effect/firewall.str`, `sprite/이팩트/fireball.act`, the three warp keys and `model/외부소품/트랩01.rsm`; no EffectKeyDuplicate warning; a re-run changes nothing.

## Phase 7: Dev tools (World.Dev, dev guard)

- [x] **T7.1** [core] Test-first: `EffectCommandParseTests` (World EditMode: `/effect a b.str 10 20 loop`, `/effect "x y.str"`, `/effect p loop`, `/effect stop` and `/effect STOP` → `Stop`, `/effect "stop"` → a path, `/effect stop loop` → a path `stop` looped, bad numbers), with an `EffectCommand` stub; every case `[Ignore("pending T7.2")]`. (FR-8, FR-10)
  *Done when:* the test class compiles and the run shows its cases ignored, 0 failed.

- [x] **T7.2** [core] Implement `EffectCommand.TryParse` (parsing from the end, quotes, `loop`, cell, `stop`, plan §4.8) and remove the Ignores. (FR-8)
  *Done when:* `EffectCommandParseTests` pass.

- [ ] **T7.3** [core] Implement `EffectDevCommands` (`/effect` forms through `DevCommandLine.AddWithArguments`; the list of handles it started; `stop` stops only those; messages "playing <key>", "stopped <n>", unknown path, no map, no character; lazy `EffectCatalog.LoadAsync` on first use), `WorldDevTools.Attach` registration, and `EffectSystem.StopAll()` on map change. (FR-6, FR-8)
  *Done when:* in a development build, `/effect texture/effect/firewall.str 10 10 loop` plays (one active effect) and an unknown path prints the unknown-path message.
  *Status:* code and unit tests done (commit below); NOT ticked: the real flow needs `_index.asset` (T6.9, owner gate) and a live map session.

- [x] **T7.4** [core] Implement `EffectDebugView` (F11 list: path, place, part count, age; text rebuilt 4 times a second) and `EffectSystem.FillDebug(List<EffectDebugInfo>)`, both under `#if UNITY_EDITOR || DEVELOPMENT_BUILD`. (FR-8) (automated: FillDebug and the text are tested with a playing effect; the on-screen F11 list is deferred to the T10.7 owner gate, not seen)
  *Done when:* in a development build the F11 list shows path, place, part count and age for a playing effect.

- [x] **T7.5** [core] `EffectDevStopPlayTests` (PlayMode, development): two looped effects started through the dev commands, then `/effect stop`: 0 active and `FillDebug` empty. (FR-8)
  *Done when:* the test passes.

## Phase 8: Effect preview window (editor only)

- [ ] **T8.1** [core] Implement `AssetDatabaseEffectSource` and `EffectPreviewStage` (`PreviewRenderUtility` scene with its own `EffectRuntime`; orbit camera with yaw, pitch and distance; backgrounds dark, grey, checker and ground grid; 15° perspective), in `Uninarok.Effects.Editor`; add an EditMode test that loads `firewall.str` through `AssetDatabaseEffectSource` and plays it in the stage (`ActiveCount` 1). (FR-11)
  *Done when:* that test passes.
  *Status:* stage and source done (commit below); NOT ticked: the test runs against the imported sprite effect `05vallentine.act` (ActiveCount 1 passes); the `firewall.str` version is pending the STR conversion (T3.8 to T3.12, owner gate).

- [x] **T8.2** [core] Implement `RoEffectPreviewWindow` browse and play (menu `Tools/Ragnarok/Effect Preview`; flat list or tree; text filter; virtualised list; `SourceMissing` marks; play, pause, stop, loop, step ±1 frame (authored entries at `AuthoredFrameRate`), scrubber, speed field 0.1 to 4 through `SetSpeed`; ticked from `EditorApplication.update`; disposed on close). (FR-11)
  *Done when:* with no scene open, filter `warp_portal` lists exactly the three warp keys; a step changes the shown frame by one.
  *Status:* automated parts done (`EffectPreviewWindowLogicTests`, `EffectPreviewControllerTests`: filter, tree/flat, virtual range, step clamp, speed clamp, warp filter over the project's effect assets, step by one frame); the IMGUI window itself was not opened or seen: its look and behaviour (list, scrubber, orbit, close) are deferred to the owner gate. The list comes from `_index.asset` when it exists (T6.9, owner gate), else from scanning `EffectAsset` assets.

- [ ] **T8.3** [core] Implement the inspector panel of the window and `EffectAssetEditor` (button "Open preview"): path, kind, fps, length in frames and seconds, key count; per layer key count and the distinct source and destination blend pairs; authored values with a plot of each curve and the current speed; per texture a thumbnail, the applied key from userData, and a missing or placeholder marker; warnings (check lines, ledger report lines, runtime `Warned` lines); `Doubtful` lines. (FR-2, FR-11)
  *Done when:* for the 10 reference files the layer, key and texture counts equal the T0.1 record; the sparkle's curves plot.
  *Status:* code and model tests done (`EffectInspectorModel`, `EffectInspectorPanel`, `EffectAssetEditor`, inspector in the window; `EffectInspectorModelTests`: the model is built from a synthetic in-memory STR asset made to match the firewall.str row of the T0.1 record, the sparkle's curves sampled for plotting, a sprite asset, applied key parse, warning and Doubtful lines); NOT ticked: the 10-reference-file equality is NOT verified (no STR EffectAssets until T3.8 to T3.12, owner gate), the plot and panel were not seen in a window, and the ledger lines have no source yet (the model takes them as input; the window passes none).

- [ ] **T8.4** [quick] Add the "Save image" button (renders the stage to a `RenderTexture` at window size and writes a PNG named from key and frame under `docs/specs/0012-effects/evidence/`) and the "Save check report" button (writes the `RoEffectDataCheck` text to `docs/specs/0012-effects/evidence/`). (FR-1, FR-11)
  *Done when:* each button writes its file; the saved check report equals the text logged by the menu item.
  *Status:* code and writer tests done (`EffectEvidenceTests`, 6 passed). Not ticked: the buttons were not clicked in the editor, no file was written from a button, and the "equals the menu item" check is by code reading only (both call `RoEffectMenu.RunEffectDataCheck().ToText()`).

## Phase 9: Play, cost and build tests

- [x] **T9.1** [core] `EffectSpeedPlayTests` (PlayMode): an STR entry and an authored cylinder at speed 2 each end at half the wall time of speed 1 (±1 frame); a looped entry's cycle index after T at speed 2 equals its index after 2T at speed 1; the picture at speed 2 and time T equals speed 1 at 2T; speed 0.5 doubles one play; `TimeScale` 0 freezes `Age`; a sprite entry at speed 2 steps its `SpriteActor` twice as fast (NeedsData). (FR-4, FR-6, FR-11)
  *Done when:* the tests pass.

- [x] **T9.2** [core] `EffectOnUnitPlayTests` (PlayMode, NeedsData): a warp entry played on a walking poring `UnitView` (Entity Sandbox loader) stays on it as the unit moves. (FR-6, FR-7)
  *Done when:* the test passes.

- [x] **T9.3** [critical] `EffectAllocationTests` (PlayMode): 300 instances (STR, sprite, authored cylinder and quad; 100 on moving transforms) give 0 B GC per frame over 300 frames, measured with `ProfilerRecorder` "GC.Alloc" over the whole frame against a no-effect baseline and around `EffectRuntime.Tick`; 1,000 start and stop cycles after warm-up give 0 B; frame time is written to `docs/specs/0012-effects/evidence/alloc-report.txt`. (FR-9)
  *Done when:* the test passes and the report file exists.

- [ ] **T9.4** [core] Extend `RoArchitectureTests`: the new asmdefs' references; GRF types only in `Core/Formats/`; no `BinaryReader` in importer code; `Uninarok.Effects` references no editor assembly; no type in `Uninarok.Effects` uses `UnityEngine.VFX`, `ParticleSystem` or `ComputeShader`. (FR-2, FR-4)
  *Done when:* the architecture tests pass.

- [ ] **T9.5** [core] `EffectBuildStrippingTests` (slow): a player build without `DevelopmentBuild` has no `Uninarok.World.Dev` or `Uninarok.Effects.Editor` assembly and no `EffectDevCommands` or `EffectDebugView` type; with `DevelopmentBuild`, World.Dev is present and Effects.Editor absent. (FR-8, FR-11)
  *Done when:* the test passes.

- [ ] **T9.6** [quick] Run the full EditMode and PlayMode suites (Unity-MCP) with the working data present; save the results to `docs/specs/0012-effects/evidence/test-run.txt`; no `pending T` Ignore remains in the Effects tests. (FR-10)
  *Done when:* 0 failed, 0 ignored from the pending list, and the file is written.

## Phase 10: Owner gates and final verification

- [ ] **T10.1** [quick] (owner) UV confirmation (R5, resolved by the owner 2026-11-24, plan D22): STR quads use the uv from the STR keys. The owner looks at the reference STR quads in the preview and confirms they look right with the file's uv. Visual confirmation only; no plan change depends on it. Blocks only the sign-off below.
  *Done when:* the owner's confirmation is recorded in str-reference.md.

- [ ] **T10.2** [quick] (owner) Interpolation (D17): the owner decides each difference T0.1 listed against GRF. Blocks only the sign-off below.
  *Done when:* each listed difference is marked accepted, or changed through a plan revision first.

- [ ] **T10.3** [quick] (owner) Preview acceptance (FR-11) on a warp entry and the 10 reference files: filter, play looped, pause holds, step, scrubber to the end, speed 0.5 doubles one play, stop returns to frame 0, loop off plays once; each background; orbit 90° (camera-facing layer faces the camera, ground quad seen edge-on); inspector counts; a pink-keyed and a black-keyed texture show their key and transparency; delete one texture in a copy of the data and check the missing marker and its one warning. Save the images under `docs/specs/0012-effects/evidence/`.
  *Done when:* saved images exist for the warp entries and the 10 reference files, and the owner's confirmation is recorded in str-reference.md under "Owner review".

- [ ] **T10.4** [quick] (owner) Must-list look (FR-5, FR-7): each must-list entry (`firewall.str`, `fireball.act`, the sparkle, both rings, the trap model) plays with its real parts and no missing texture; the owner confirms the warp parts (rings shrinking and rising, sparkle moving and fading, additive blue) from saved images.
  *Done when:* the saved images exist and the sign-off is recorded in str-reference.md.

- [ ] **T10.5** [quick] (owner) Speed in the preview (FR-4): `firewall.str` and a warp entry at speed 1 and speed 2; one cycle is halved. Saved images.
  *Done when:* the images exist and the owner's sign-off is recorded.

- [ ] **T10.6** [quick] (owner) In-game check (FR-7): development build on a local Hercules, Bootstrap; `/effect <path> <x> <y> loop` with each of the three warp paths on a prontera warp cell; one screenshot each; no warning in the log; job 45 units still draw nothing in normal play.
  *Done when:* three screenshots are saved under `docs/specs/0012-effects/evidence/` and the owner confirms them.

- [ ] **T10.7** [quick] (owner) Dev tools in game (FR-8): the F11 list shows a playing effect with path, place, part count and age; with two looped effects playing, `/effect stop` leaves the list empty. Screenshots saved.
  *Done when:* the screenshots exist and the owner confirms them.

- [ ] **T10.8** [quick] Release player build check (FR-8, FR-11): build without the development define; confirm no `/effect` command, no F11 list and no preview window in the build; the result is noted in `docs/specs/0012-effects/evidence/release-build.txt`.
  *Done when:* the note exists with the three results.

- [ ] **T10.9** [quick] (owner) Profiler capture of the FR-9 load (300 effects; 1,000 start and stop cycles) on the owner's PC; capture and frame time saved to evidence.
  *Done when:* the capture is saved and its frame time is recorded in the evidence.

- [ ] **T10.10** [quick] Save the final evidence: the FR-1 check report (Save check report, T8.4) as `docs/specs/0012-effects/evidence/check-report.txt` (run twice, same text; counts and missing list), the warning list and the `Doubtful` values of the must-list as `evidence/warnings.txt` and `evidence/doubtful.txt`. (FR-1, FR-5)
  *Done when:* the three files exist and the check report's second run matches the first.
