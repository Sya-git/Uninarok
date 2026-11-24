# T0.3 spike: submesh draw order and cost (SPEC-0012 FR-3, FR-9; plan §4.5, D4, R3)

**Result:** submesh order **held** in every case, in both orders, with the SRP Batcher on and off. Under the FR-9 load the submesh design costs about **2.0 ms** per frame over an empty baseline, and the per-layer renderer fallback costs about **10.1 ms** (editor, numbers below). D4 stands: submesh order is the design, and the fallback is not needed. The fallback would be about 5x more expensive in any case.

## Setup

| | |
|---|---|
| Unity | 6000.7.0b7, URP 17.7.0, renderer `PC_RPAsset`, linear colour space, vSync 0 |
| Graphics | Direct3D 12, NVIDIA GeForce RTX 4060 Ti; CPU Intel Core i5-14600KF |
| Where | **Editor, edit mode** (not a build, not play mode), driven through Unity-MCP `script-execute`. A disabled camera renders with `Camera.Render()` into a RenderTexture. The camera's culling mask is layer 31 only. |
| Throwaway content | Compiled in memory by `script-execute`: shader created with `ShaderUtil.CreateShaderAsset`, all objects `HideFlags.HideAndDontSave`, destroyed at the end. Nothing was written to `Assets/`. Afterwards: 0 leftover objects, 0 leftover shaders, scene not dirty. |
| Shader | Stand-in for `Uninarok/RoEffect` (plan §4.5): URP unlit `UniversalForward`, queue Transparent (3000), `Blend [_SrcBlend] [_DstBlend]`, `ZWrite Off`, `Cull Off`, `ZTest LEqual`, vertex colour × texture, `UnityPerMaterial` CBUFFER (SRP Batcher compatible), keyword `_FACING_CAMERA` (billboard expansion along the camera's right and up axes from the object origin). |
| Mesh | Plan §4.5 layout: 24-byte vertex, one `Mesh` per instance, one submesh per layer in order, 16-bit indices, `SetVertexBufferData` from a `NativeArray` with `DontValidateIndices \| DontRecalculateBounds \| DontNotifyMeshUsers \| DontResetBoneBounds`, bounds set once, `Renderer.SetSharedMaterials`. No shadows, no light or reflection probes. |
| Fallback | Plan D4: one child `MeshRenderer` (own 4-vertex or cylinder mesh) per layer, `sortingOrder` = layer index, a `SortingGroup` on each instance root. |

## 1. Draw order (R3)

Two or more layers fill the same pixels, one submesh per layer. Each layer has its own material. The colour comes from the material's texture, not the vertex colour, so a reorder by material or state could not hide. Materials are created in a fixed order: red first, so its instance id is lower. "Order A" lists the materials in creation order and "order B" reverses them. If the pipeline sorted by material id or state, one of the two orders would fail. The centre pixel of a 64×64 linear RenderTexture is read back and compared with the expected colour (tolerance ±2 of 255).

| Case | SRP Batcher on | SRP Batcher off |
|---|---|---|
| 2 layers, alpha 1, order A (red, green): green on top | held | held |
| 2 layers, alpha 1, order B (green, red): red on top | held | held |
| 2 layers, alpha 0.5, order A: expected (64,128,0) | held | held |
| 2 layers, alpha 0.5, order B: expected (128,64,0) | held | held |
| Mixed blend: alpha red, then additive green (SrcAlpha/One) = yellow | held | held |
| Mixed blend: additive green, then alpha red = red | held | held |
| `_FACING_CAMERA` variant, order A | held | held |
| `_FACING_CAMERA` variant, order B | held | held |
| 12 layers (12 materials, keyword variants mixed): creation order, reversed, 4 random permutations; the last submesh is on top | held (6/6) | held (6/6) |
| Two instances at different depths, near over far, both creation orders | held (2/2) | held (2/2) |
| Fallback (per-layer renderers + SortingGroup), order A and order B | held (2/2) | held (2/2) |

Total: **36 of 36 held, 0 failed.** The half-alpha cases match the exact blend arithmetic for each order, so the layers were drawn in submesh order, not merely with the right layer on top. Within one renderer, URP draws a transparent mesh's submeshes in index order. The material, its instance id, the keyword variant and the blend state do not reorder them. The per-renderer back-to-front sort between instances still applies.

## 2. Cost under the FR-9 load

**Load** (the same for both designs): 300 effects in a 20 × 15 grid, 2 units apart, all in view.
- 180 STR-like effects with 12 camera-facing layers each (the survey average is 8.5 layers per file: 25,598 layers in 2,995 files).
- 60 authored cylinder effects: 2 cylinder strips with 16 sides (34 vertices each) and 1 ground quad.
- 60 authored quad effects: 2 camera-facing quads.

That makes 2,460 parts in total. 100 effects are on moving transforms: every third effect, a mix of all kinds, moved each frame. Every part's vertices are rewritten and uploaded every frame (worst case). The materials are 8 textures (64×64 radial alpha) × (alpha, additive) × (object, facing) = 32. Additive is used on about a quarter of the parts. The camera is perspective, FOV 50, at (0, 18, −18) looking at (0, 0, 14). Each measurement runs 30 warm-up frames, then averages **300 frames**.

**Columns:**
- **Update**: the C# per-frame work: moving transforms, writing vertices (a stand-in for the sampler), and `SetVertexBufferData`. *Write* is the vertex-writing part alone.
- **Render()**: the CPU time of `Camera.Render()`.
- **Frame, pipelined**: the wall time of the 300 frames plus one GPU sync at the end, divided by 300. This is the throughput per frame and the number to compare.
- **Frame, sync each**: a 1-pixel `AsyncGPUReadback.WaitForCompletion` every frame. This measures latency: it is dominated by readback latency in the editor, does not depend on resolution, and is kept only for completeness.

The GPU sync is the only GPU measure. There is no GPU profiler counter for manual camera renders.

### 1920×1080 target

| Design | Renderers | Update ms (write) | Render() ms | **Frame ms, pipelined** | Over baseline | Frame ms, sync each | Managed alloc in update |
|---|---|---|---|---|---|---|---|
| Baseline (no effects) | 0 | 0 | 1.449 | **1.764** | n/a | 1.992 | 0 B |
| Submesh (plan design) | 300 | 2.245 (1.060) | 1.482 | **3.758** | **+1.99** | 8.976 | 0 B |
| Submesh + `Mesh.MarkDynamic` | 300 | 2.244 (1.052) | 1.455 | **3.772** | +2.01 | 8.699 | 0 B |
| Fallback (per-layer renderers + SortingGroup) | 2,460 | 6.803 (1.196) | 5.018 | **11.901** | **+10.14** | 18.053 | 0 B |

### 480×270 target (same load, to separate fill cost)

| Design | Update ms (write) | Render() ms | **Frame ms, pipelined** | Over baseline | Frame ms, sync each |
|---|---|---|---|---|---|
| Baseline | 0 | 0.737 | **1.184** | n/a | 2.284 |
| Submesh | 2.356 (1.120) | 1.624 | **4.007** | +2.82 | 10.496 |
| Submesh + MarkDynamic | 2.282 (1.069) | 1.554 | **3.954** | +2.77 | 8.029 |
| Fallback | 5.914 (1.202) | 5.116 | **11.159** | +9.98 | 18.002 |

An earlier run with the same load and code, before the pipelined column was added (the sync was taken per frame only), gave the same picture: submesh update 2.3 ms + Render() 1.5 ms, and fallback update 7.3 ms + Render() 4.7 ms, at both 1920×1080 and 480×270.

## Reading

- **Order:** submesh order holds, so the plan's design (D4) is confirmed and the D4 fallback is not needed. No change to the plan.
- **Cost, submesh vs fallback:** the fallback is about 5x more expensive. It needs 2,460 renderers instead of 300: `Render()` costs 3.4× as much (culling and sorting 8× the renderers), and the uploads cost about 4.7× as much (2,460 `SetVertexBufferData` calls instead of 300). Fill rate is not the limit for either design: the cost barely changes from 1080p to 270p.
- **Against the FR-9 guide of 1 ms** (a guide, not pass/fail): the submesh design measures about 2 ms over baseline in the **editor**, under a worst-case mix where every part changes every frame.
  - About 1.0 ms of it is the vertex writing, a stand-in for `EffectSampler`. In the editor this runs Mono JIT with `NativeArray` safety checks on.
  - About 1.2 ms is the 300 uploads (about 4 µs each).
  - `Render()` adds only about 0.03 ms over baseline at 1080p.
  - A development or release build removes the safety checks and editor overhead. T10.9 (owner Profiler capture in a build) is where the real figure is taken.
- **`Mesh.MarkDynamic`:** no measurable difference on D3D12 (within noise). There is no reason to add it.
- **GC:** 0 B of managed allocation per frame in the update path for both designs, which is consistent with FR-9. The whole-frame check is T9.3.

## Observation for T5.3 (`EffectMeshWriter`), not a plan change

Unity requires the vertex attributes in `VertexAttribute` enum order within a stream: Position, …, Color, …, TexCoord0. So the 24-byte `EffectVertex` must be laid out as **float3 position, Color32 colour, float2 uv**, which is the layout used here. Plan §4.5 lists the members as "float3 position, float2 uv, `Color32`". That list does not set the byte order, but a struct declared in that order would not match a valid `SetVertexBufferParams` layout.
