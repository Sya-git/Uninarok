# STR facts (as read through GRF)

| | |
|---|---|
| **Belongs to** | [SPEC-0012](spec.md) (reference material, normative for its topic: STR format, GRF mapping, sampling semantics; plan [PLAN-0012](plan.md) §4.1, §4.2) |
| **Status** | T0.1 survey of 2026-11-24. Sections still to be written by later tasks: colour key (T0.2b), scale and pivot, authored-entry rules |
| **Data set** | `Uninarok-client/Assets/Editor/GRF Resources~/data`: every file whose name ends in `.str` (2,995: 2,994 under `texture/effect/`, 1 under `sprite/이팩트/`) |
| **Method** | Every file read twice: through `GRF.dll` (`GRF.FileFormats.StrFormat.Str`, in a scratch PowerShell tool) and by an independent byte reader (Python `struct`, layout of §2), compared field by field. GRF's interpolation API called on every integer frame of the 10 reference files and compared with the plan §4.2 rule. Raw output: [evidence/str-survey.txt](evidence/str-survey.txt). The scratch tools are not committed. |

Status of each claim: **Confirmed** (checked on every file of the data set), **Candidate** (consistent with the data or a second source, not yet confirmed visually by the owner), **Lead** (roBrowserLegacy only, plan §1).

## 1. Summary

- One version in the data: `0x94`, all 2,995 files. Frame rate 60 in every file.
- GRF reads every file. It agrees with the byte read on every field of every key, except three things the adapter must handle (§3): the angle comes in degrees, a `+inf` delay comes as 0, and texture names need the Korean encoding.
- No file has texture coordinates other than the full rectangle (R5: 0 of 2,995 files).
- GRF's interpolation API does **not** apply the file's per-frame delta keys, so it cannot be the offline oracle. The differences are listed in §8 for the owner, not resolved.
- The data confirms the lead's meaning of delta keys: for each of the 49,383 delta keys, base + delta × (next base frame − base frame) gives the next base key's position, corners, angle, colour and uv.

## 2. File layout (version 0x94) — Confirmed

Little-endian. The byte reader consumes every file exactly to its end (no trailing bytes in any file).

| Offset | Size | Field |
|---|---|---|
| 0 | 4 | magic `STRM` |
| 4 | 4 | version, uint32 (`0x94`) |
| 8 | 4 | frame rate, uint32 |
| 12 | 4 | frame count ("max key"), uint32 |
| 16 | 4 | layer count, uint32 |
| 20 | 16 | reserved |
| 36 | … | layers, in file order |

Layer: texture count (int32), then that many 128-byte name slots (NUL-terminated, CP949), then key count (int32), then that many 124-byte keys.

Key (124 bytes): frame int32, type uint32, position 2 × float, uv 8 × float, corners 8 × float (four x, then four y), aniframe float, anitype uint32, delay float, angle float, colour 4 × float, source blend uint32, destination blend uint32, mtpreset uint32.

## 3. GRF mapping (plan §4.1 `RoStr`, `RoStrKey`) — Confirmed

GRF types: `Str` (constructor `Str(MultiType)`, as `RoSprReader` builds it from the bytes), `StrHeader`, `StrLayer`, `StrKeyFrame`. "Equal" means equal to the byte read on all 2,995 files.

| Plan field | GRF member | Notes |
|---|---|---|
| `RoStr.Version` | GRF `StrHeader` built alone from the bytes (`new StrHeader(new ByteReader(bytes, 0), new Str())`, plan D24): `MajorVersion \| MinorVersion << 8` | GRF gives 148, minor 0, for `0x94` (`HexVersionFormat` `0x9400`); `StrHeader` alone parses only the header and throws `GrfException` on a wrong magic. **GRF's `Str` accepts any version** and reads it with the 0x94 layout without an error (versions 0x95, 0x93 and 0x01 all read "OK"), so `RoStrReader` checks the header's version against `0x94` before `new Str(bytes)` reads the body. The reader never reads the bytes itself |
| `RoStr.Fps` | `Str.Fps` (int) | equal; 60 in every file |
| `RoStr.FrameCount` | `Str.MaxKeyFrame` (int) | equal. GRF's `Str.KeyFrameCount` is `MaxKeyFrame + 1` (see §8 O2); not used |
| `RoStr.Layers` | `Str.Layers` (`List<StrLayer>`), file order | equal count and order |
| `RoStrLayer.TextureNames` | `StrLayer.TextureNames` (`List<string>`) | decoded with `Utilities.Services.EncodingService.DisplayEncoding`, which is Windows-1252 by default: `번개1.bmp` comes as `¹ø°³1.bmp`. Set `DisplayEncoding = EncodingService.Korean` (code page 949) around the read and restore it after, as `RoActReader` and `RoGndReader` do; then equal. Only 3 of 58,410 names are not ASCII (`texture/effect/ado.str`) |
| `RoStrLayer.Keys` | `StrLayer.KeyFrames` (`List<StrKeyFrame>`) | equal count and order; keys are sorted by frame in every layer of the data |
| `RoStrKey.Frame` | `StrKeyFrame.FrameIndex` (int) | equal |
| `RoStrKey.Type` | `StrKeyFrame.Type` (int) | equal; only 0 (base) and 1 (delta) occur |
| `RoStrKey.Position` | `StrKeyFrame.Offset` (`TkVector2`: `X`, `Y`) | equal; pixels, y down |
| `RoStrKey.Uv0..Uv3` | `StrKeyFrame.UVs` (`float[8]`) | equal. Stored raw as `Uv0 = (UVs[0], UVs[1])`, `Uv1 = (UVs[2], UVs[3])`, `Uv2 = (UVs[4], UVs[5])`, `Uv3 = (UVs[6], UVs[7])`. These are **not four corner uvs**: GRF reads them as two points (`InterpolatedKeyFrame.GetUVVector(UvPointId)` returns `(UVs[0], UVs[1])` for `TopLeft` and `(UVs[2], UVs[3])` for the value GRF names `TopRight`, which holds (1, 1)); `UVs[4..7]` equal `UVs[0..3]` in every key of the data and GRF has no accessor for them. Every base key holds (0, 0, 1, 1, 0, 0, 1, 1), every delta key 0 (§5). Corner uvs: §6 |
| `RoStrKey.Xy0..Xy3` | `StrKeyFrame.Positions` (`float[8]`) | equal; corner i = `(Positions[i], Positions[4 + i])`, i = 0 top-left, 1 top-right, 2 bottom-right, 3 bottom-left (GRF `PointId`, `GetXYVector`), pixels relative to `Position`, y down. Example `firewall.str`: x (−32, 32, 32, −32), y (−32, −32, 32, 32) |
| `RoStrKey.AniFrame` | `StrKeyFrame.TextureIndex` (float) | equal |
| `RoStrKey.AniType` | `StrKeyFrame.AnimationType` (enum `AnimationType`: 0 `Stop`, 1 `Interpolation`, 2 `Once`, 3 `Loop`, 4 `ReverseLoop`, 5 `BiLoop`), cast to int | equal; the data also holds 6, which has no GRF name (§8 O5) |
| `RoStrKey.Delay` | `StrKeyFrame.Delay` (float) | equal, except that **GRF reads `+inf` as 0**: 8,716 delta keys in 913 files hold `+inf` in the file (their base keys hold 0). The raw `+inf` cannot be recovered without parsing bytes (§8 O4) |
| `RoStrKey.Angle` | `StrKeyFrame.Angle` (float) | **GRF converts to degrees**: GRF value = raw × 360 / 1024 (raw 36 → 12.65625, raw 1024 → 360). To keep the plan's raw value, the adapter sets `Angle = GRF value × 1024 / 360`; largest difference from the file over all 188,736 keys: 4.9e-4 raw units. This also confirms the lead's 1024 units per turn as GRF's own reading (two sources agree; still Candidate until the owner's visual check) |
| `RoStrKey.Color` | `StrKeyFrame.Color` (`float[4]`) | equal; raw 0..255 in base keys, signed per-frame deltas in delta keys. Order R, G, B, A (Candidate: alpha is the fourth value, the one that fades in from 0 at frame 0 in `firewall.str`) |
| `RoStrKey.SrcBlend`, `DstBlend` | `StrKeyFrame.BlendSrc`, `BlendDst` (int) | equal; raw codes. Delta keys hold 0 / 0 |
| `RoStrKey.MtPreset` | `StrKeyFrame.MtPresent` (int, GRF's spelling) | equal; 1 in 60 keys, 0 elsewhere |
| — | `StrKeyFrame.IsInterpolated`, `BezierPositions`, `AngleBias`, `OffsetBias`, `ScaleBias` | GRF Editor's own editing model, not file data: false / 0 on every key after `Str(MultiType)`; not used. `Str.DetectInterpolatedFrames` is an editor heuristic and is not called |

## 4. Reference files (FR-2, FR-3, FR-11) — Confirmed

Read through GRF; the byte read of the headers (offsets 4, 8, 12, 16) and of every layer's key and texture counts gives the same values for each file (column "GRF = bytes"). Layer 0 is empty (no texture, no key) in all ten files.

| File | Version | Fps | Frame count | Layers | Keys per layer | Textures per layer | Distinct textures | GRF = bytes |
|---|---|---|---|---|---|---|---|---|
| `texture/effect/firewall.str` | 0x94 | 60 | 40 | 11 | 0 11 11 11 13 13 13 23 23 27 41 | 0 2 2 2 2 2 2 2 2 2 1 | 4 | yes |
| `texture/effect/cross.str` | 0x94 | 60 | 121 | 11 | 0 45 45 34 34 17 17 15 15 21 5 | 0 1 1 1 1 1 1 1 1 1 1 | 2 | yes |
| `texture/effect/angelus.str` | 0x94 | 60 | 236 | 11 | 0 30 119 119 3 3 119 119 3 27 43 | 0 31 1 1 1 1 1 1 1 1 1 | 14 | yes |
| `texture/effect/cure.str` | 0x94 | 60 | 107 | 12 | 0 36 26 26 73 5 3 33 33 51 51 51 | 0 1 1 1 1 1 1 1 1 1 1 1 | 5 | yes |
| `texture/effect/freeze.str` | 0x94 | 60 | 60 | 8 | 0 26 26 11 8 14 14 14 | 0 1 1 1 1 1 1 1 | 3 | yes |
| `texture/effect/bs_refinesuccess.str` | 0x94 | 60 | 100 | 17 | 0 33 18 15 21 12 12 12 12 11 11 3 3 3 3 7 3 | 0 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 | 6 | yes |
| `texture/effect/dust.str` | 0x94 | 60 | 120 | 46 | 0 11 11 11 11 5 11 5 11 5 11 5 11 5 11 5 11 5 11 5 11 11 11 5 11 11 11 11 11 11 11 11 11 11 11 11 11 5 5 5 5 5 3 7 7 7 | 0, then 1 in each of the 45 other layers | 7 | yes |
| `texture/effect/new_splash/new_splash.str` | 0x94 | 60 | 38 | 2 | 0 3 | 0 20 | 20 | yes |
| `texture/effect/mineffect/new_earthgrave/new_earthgrave_bottom/new_earthgrave_bottom.str` | 0x94 | 60 | 48 | 3 | 0 5 5 | 0 1 1 | 1 | yes |
| `sprite/이팩트/jong_mini.str` | 0x94 | 60 | 100 | 6 | 0 21 5 5 38 38 | 0 31 1 1 1 1 | 10 | yes |

"Keys per layer" counts base and delta keys together. "Distinct textures" counts names over all layers, case-insensitively.

Coverage of the set: base-key blend pairs (5, 7) everywhere, plus (2, 1) in one layer of `angelus.str`, the only reference layer whose keys use **two blend pairs** (the FR-2 per-key blend case). Anitypes used: 0, 1 (`angelus.str`) and 3 (`firewall.str`, `new_splash.str`, `jong_mini.str`); anitypes 2 and 4 occur in no reference file, so only the synthetic `EffectSamplerTests` cover them. No reference file holds a `+inf` delay.

## 5. Data set facts — Confirmed

Counts are over all 2,995 files (details in the evidence file).

- **Versions:** `0x94` 2,995. No bad magic, no other version.
- **Texture coordinates (R5):** 0 files differ from the full rectangle. All 139,353 base keys hold (0, 0, 1, 1, 0, 0, 1, 1); all 49,383 delta keys hold uv deltas of 0; `UVs[4..7]` equal `UVs[0..3]` in every base key. D22 (draw with the stored uv) therefore gives the same picture as the lead's full rectangle on this data.
- **Key structure:** 139,353 base keys (type 0), 49,383 delta keys (type 1), no other type. Every delta key comes right after a base key of the same frame; no delta key follows the last base key of its layer; no two base keys share a frame in a layer.
- **Delta meaning:** for all 49,383 base + delta pairs, base + delta × (next base frame − base frame) equals the next base key (tolerance 1 % or 0.01) for position, corners, angle (raw units), colour and uv. So a delta key holds the per-frame change from its base key to the next base key.
- **Held segments:** 67,320 base keys are followed directly by another base key (no delta: values held).
- **Layer boundaries:** 12,891 layers start after frame 0; 16,584 layers end before the frame count (10,772 of them on a key with alpha 0); 6,066 keys in 2,753 files sit on frame = frame count; no key lies beyond the frame count. 2,948 of 25,598 layers have no key; 46 have a single key.
- **Anitype** (type, anitype): base keys 0: 114,925, 1: 190, 2: 12,489, 3: 11,595, 4: 140, 5: 4, 6: 10; delta keys 0: 36,495, 1: 8, 2: 7,259, 3: 5,593, 4: 23, 6: 5. A delta key's anitype always equals its base key's. Anitype 5 in `texture/effect/_devil.str` and `texture/effect/devil.str`; 6 in `texture/effect/efst_kung_fu_panda/redyoumei2.str`.
- **Delay:** a delta key's delay equals its base key's in every case except the 8,716 `+inf` ones (whose base delay is 0; anitype of those delta keys: 0 in 7,793, 3 in 564, 2 in 357, 6 in 2).
- **Blend pairs** (base keys, raw codes): (5, 7) 116,972; (5, 6) 20,425; (1, 6) 576; (2, 1) 443; (6, 6) 300; (3, 9) 244; (12, 12) 95; (5, 5) 94; (2, 2) 51; (7, 6) 30; (5, 9) 27; (1, 3) 24; (5, 2) 18; (3, 6) 16; (3, 7) 14; (2, 6) 8; (1, 7) 8; (7, 5) 4; (5, 8) 2; (2, 7) 2. Code 12 is outside the plan's 1..11 table (§4.5 default and warning). 111 layers in 67 files use more than one blend pair across their base keys.
- **Names:** 58,410 texture names, all with an extension, 3 not ASCII.

## 6. Sampling (plan §4.2, for `EffectSampler`)

The rule is the plan's (lead, accepted by the owner, D17). What the data and GRF say about each part:

| Part of the rule | Status |
|---|---|
| At frame f the base key is the last type-0 key with `Frame ≤ f`; none → hidden | Confirmed against GRF: same visibility on all 14,298 layer-frames of the reference set (GRF returns no key before the first key) |
| A delta key is the key right after its base key, at the same frame | Confirmed (§5) |
| With a delta key: value = base + delta × (f − base.Frame) for position, corners, uv, angle, colour | Confirmed by the data (§5 delta meaning); GRF does not do this (§7) |
| Without a delta key: the base key's values are held until the next base key | Confirmed against GRF on the 1,594 held layer-frames of the reference set |
| After the last key: hidden; the last key shows on its own frame only | Confirmed against GRF (it returns no key after the last key) |
| Blend = the base key's codes (delta keys hold 0 / 0) | Confirmed (§5) |
| Angle → degrees: raw × 360 / `AngleUnitsPerTurn` (1024) | Candidate: GRF and the lead agree on 1024 |
| Colour / 255 | Candidate (raw range 0..255 confirmed) |
| Texture frame by anitype: 0 → 0, 1 → base + delta.aniframe × d, 2 → min(base + delay × d, n − 1), 3 → wrap modulo n, 4 → reverse, wrapped positive | Lead; wording open on anitype 0, the delay used and anitypes 5 and 6 (§8 O3 to O5) |

`EffectSampler` (T2.2) applies the plan rule as written until the owner decides O2 to O5: anitype 0 gives texture frame 0 (O3); anitypes 2 to 4 use the delta key's delay (O4), and a non-finite delay (the `+inf` file values) reads as 0, as GRF reads it; anitypes 5, 6 and unknown hold the base key's aniframe (O5); length is frame count / fps (O2). The texture frame is floored and clamped to the layer's textures (anitype 4 wraps positive first: n = 4, d = 1 gives 3); a layer with no texture gives texture index −1.

Corner placement and uv (Candidate, from §3): corner i of the quad is `Xy_i` (0 top-left, 1 top-right, 2 bottom-right, 3 bottom-left, pixels, y down) placed relative to `Position`. The stored uv is a rectangle from `Uv0` (top-left) to `Uv1` (bottom-right), so the corner uvs are top-left `(Uv0.x, Uv0.y)`, top-right `(Uv1.x, Uv0.y)`, bottom-right `(Uv1.x, Uv1.y)`, bottom-left `(Uv0.x, Uv1.y)`, with v counted from the image's top row. `Uv2` and `Uv3` are kept raw and not used. Every file holds the full rectangle (§5), so the data cannot tell this from another layout; the owner's visual check stands.

## 7. GRF's interpolation compared with the plan rule

Called: `InterpolatedKeyFrame.Interpolate(str, layer, frame, false)` (static) on every layer and every integer frame 0..frame count of the 10 reference files (14,298 layer-frames). `interpolationOnly = true` gave the same results on the frames probed by hand (`firewall.str` layer 1).

| Result | Layer-frames |
|---|---|
| Visibility (shown or hidden) agrees | 14,298 of 14,298 |
| Values agree (frames held by a base key with no delta key) | 1,594 |
| Values differ (frames covered by a delta key) | 4,589 |

On a frame covered by a delta key, GRF returns **the delta key's own raw values** (the per-frame change, with blend 0 / 0), not base + delta × d. Example, `firewall.str` layer 1 frame 0: plan position (320, 329), GRF (−0.4, −4); plan blend (5, 7), GRF (0, 0). GRF's own interpolation (biases, Bézier, `IsInterpolated`) is GRF Editor's model for keys it marks itself; no key of the data is marked. GRF also maps frames 0..frame count inclusive (`FrameIndex2KeyIndex` has `MaxKeyFrame + 1` entries).

Consequence for plan §6: GRF's interpolation is not validated as the oracle of `EffectSamplerReferenceTests`; the plan's other branch applies unless the owner decides otherwise (§8 O1).

## 8. Owner decision

Differences found by T0.1, listed for the owner and **not resolved** here. The plan rule (D17) stays as written until the owner decides.

- **O1 Oracle of the reference sampling test.** GRF's `Interpolate` does not apply delta keys (§7: 4,589 of the 6,183 shown layer-frames differ; visibility and held frames agree). The plan's fallback is the formulas of §6 applied to `RoStr`. Owner: confirm that fallback as the oracle of `EffectSamplerReferenceTests`, or name another.
- **O2 Last frame and length.** GRF treats frames 0..frame count as frames (frame count + 1 of them); the plan's length is frame count / fps, and a one-shot ends when `Age ≥` that length. 6,066 keys in 2,753 files sit exactly on frame = frame count, so under the plan they are drawn only if a tick lands exactly on the end. Owner: length frame count / fps or (frame count + 1) / fps, and whether "the last frame" of the FR-3 test is frame count − 1 or frame count (FR-3 allows ±1 frame).
- **O3 Anitype 0.** The plan says "0 → 0". GRF names 0 `Stop`. 625 base keys with an anitype-0 delta key hold an aniframe other than 0: under "0 → 0" they draw texture 0, under "hold the base aniframe" they draw their own. Owner: which.
- **O4 Delay used by anitypes 2, 3, 4.** The plan does not say whose delay. A delta key's delay equals its base key's everywhere except 8,716 delta keys whose file value is `+inf` (base 0; 921 of them with anitype 2 or 3); GRF reads that `+inf` as 0, so through GRF both choices give 0. Owner: base key's delay or delta key's delay.
- **O5 Anitypes 5 and 6.** Outside the lead's 0..4: 5 (GRF `BiLoop`) in 4 base keys of `texture/effect/_devil.str` and `texture/effect/devil.str`; 6 (no GRF name) in 10 base and 5 delta keys of `texture/effect/efst_kung_fu_panda/redyoumei2.str`. Owner: how they draw (for example as one of 0..4, or hold, with one warning).

## 9. Malformed input through GRF (for `RoStrReader`)

`Str(MultiType)` on cut or patched copies of `firewall.str`:

| Input | GRF |
|---|---|
| empty, cut to 8 bytes, cut to half, cut by 1 byte | throws `IndexOutOfRangeException` |
| 64 junk bytes | throws `GrfException` "Unrecognized file format (STR)" |
| version patched to 0x95, 0x93, 0x01 | **reads without error** (0x94 layout) |
| layer, texture or key count 0x7FFFFFFF | throws `OutOfMemoryException` "Array dimensions exceeded supported range", at once |
| layer count 33,554,432; texture or key count 16,777,216; layer count + 1 | throws `IndexOutOfRangeException` within 20 ms, no large allocation |
| texture or key count −1 | throws `ArgumentOutOfRangeException` (capacity) |

So the reader takes magic and version from GRF's `StrHeader` before the body is read (§3, plan D24): a wrong magic is `CorruptFile`, a version other than `0x94` is `UnsupportedStrVersion`; it catches every exception type from the GRF constructors, `OutOfMemoryException` included, and turns each into `CorruptFile` (plan §4.1). It also rejects fps ≤ 0 and a negative frame count. No case hangs. `RoStrReaderTests` makes wrong-version input with GRF's own writer (`Header.SetVersion`, `Save`), never by patching bytes; the patched copies above were T0.1's survey only.

## 10. Colour key

Decided by the owner on 2026-11-25 from `evidence/colour-key-survey.csv` (21,757 images; 18,586 used by an STR). Task T0.2b.

| Field | Value | Why (survey) |
|---|---|---|
| DefaultOpaque (BMP, 24-bit TGA, JPG, PNG without alpha) | `PinkAndBlack` | 97% of the used 24-bit BMPs have black corners; 135 + 91 used BMPs have pink corners |
| DefaultWithAlpha (32-bit TGA, PNG with alpha) | `None` (use the file's alpha) | 5,975 of the 6,401 used 32-bit TGAs already have alpha 0 at the corner |
| PinkTolerance | 0 (exact 255,0,255) | the survey does not measure it; owner choice |
| BlackTolerance | 8 (of 255) | the survey does not measure it; allows for compression noise in 8-bit BMPs; owner choice |
| Per-texture overrides | none | to be added if the visual review (T10.3) finds a wrong texture |
| `ByBlend` | no | no evidence that the background follows the blend; it follows the file format |

Owner sign-off: accepted as proposed, 2026-11-25.

Reference textures (the pink and the black example): `texture/effect/a01.bmp` (24-bit, pink corners) and `texture/effect/4ig_judge/judge_cast/jud_shine_1.bmp` (24-bit, black corners), each used by one STR; the owner checks in the preview window (T10.3) that both come in transparent at the corners.

## 11. Blend codes (plan §4.5, `EffectBlend`)

STR keys and authored assets store Direct3D blend codes (uint32 in the STR). The table is pinned by `EffectBlendTests`:

| Code | Unity `BlendMode` |
|---|---|
| 1 | Zero |
| 2 | One |
| 3 | SrcColor |
| 4 | OneMinusSrcColor |
| 5 | SrcAlpha |
| 6 | OneMinusSrcAlpha |
| 7 | One (DESTALPHA, destination alpha 1) |
| 8 | Zero (INVDESTALPHA, 1 − 1) |
| 9 | DstColor |
| 10 | OneMinusDstColor |
| 11 | Zero (SRCALPHASAT, min(As, 1 − 1)) |

The client draws into a backbuffer with no alpha channel, so Direct3D reads destination alpha as 1: codes 7, 8 and 11 use that constant, never the Unity target's alpha, and `Uninarok/RoEffect` keeps the target's alpha (`Blend …, Zero One`) (plan D33). Any other code (0, 12, 255, ...) falls back to SrcAlpha / OneMinusSrcAlpha for the whole pair and is reported unknown, so the caller logs one warning per asset. Code 12 occurs in 95 base keys of the data set (pair 12 / 12, §5). The most common pair is (5, 7) = SrcAlpha / DESTALPHA, which plays as additive SrcAlpha / One.

**Keyed texels (plan D37).** The converted textures keep their RGB under the colour-keyed texels (alpha 0). A pair whose source factor weighs by the texture alpha (SrcAlpha, so 5 / x) needs nothing more; a pair whose source does not (One, SrcColor, ... so `angelus.str`'s 2 / 1, One / Zero, layers 1 and 8, 5 base keys of layer 1 from frame 21) would draw that RGB, so `Uninarok/RoEffect` discards texels with texture alpha below 0.5 for those pairs (`EffectMaterials.KeyedCutoff`). The keyed area draws nothing, as FR-2 says.

## Owner review

- **T10.1, UV (2026-11-25):** the owner confirmed in the preview window that the 10 reference files look right with the file's uv (D22). `angelus.str` first showed an opaque background and an extra quad; fixed in T8.8 (D37) and rechecked by the owner.
- **T10.3, preview acceptance (2026-11-25):** the owner checked the 12 items (filter, play looped, pause, step, scrubber to the end, speed 0.5, stop, loop off, backgrounds, orbit 90 degrees, inspector counts, pink and black keyed textures, missing texture marker) on the warp entries and the 10 reference files: all fine.
- **T10.4, must-list look (2026-11-25):** the owner checked each must-list entry (`firewall.str`, `fireball.act`, the sparkle, both rings, the trap model) in the preview window: real parts, no missing texture, the warp parts look right. A stale frame shown after switching to a sprite act was reported at the same time (T8.11).
