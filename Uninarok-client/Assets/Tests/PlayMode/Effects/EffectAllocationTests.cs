using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using NUnit.Framework;
using Uninarok.Catalog;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;

namespace Uninarok.Effects.PlayTests
{
    // SPEC-0012 FR-9 (plan 4.5 "0 B per frame, how it holds and how it is verified", D32): 300 effect instances (STR, sprite,
    // authored cylinder and quad; 100 of them following moving transforms) allocate 0 B of managed memory per frame, and
    // 1,000 start and stop cycles after warm-up allocate 0 B. Measured with ProfilerRecorder:
    // - whole frame: "GC Allocated In Frame" (bytes) and the "GC.Alloc" marker on the main thread (allocations), one sample per
    //   frame over 300 frames, against a baseline of the same harness (camera, moving transforms, runtime ticking) with no effect;
    // - around EffectRuntime.Tick and around the start/stop cycles: the in-frame delta of the "GC.Alloc" marker on the main
    //   thread (exact: 0 samples is 0 B); "GC Allocated In Frame" counts every thread (editor background work), so its bytes
    //   are reported, and the whole-frame check compares main-thread allocations with the baseline.
    // STR, cylinder and quad assets are in memory (STR from the real importer does not exist yet); the sprite entry is the real
    // converted fireball (NeedsData: skipped when the effect sprite catalog is not imported). The test writes frame time and the
    // GC figures to docs/specs/0012-effects/evidence/alloc-report.txt.
    public class EffectAllocationTests
    {
        const int Instances = 300, Moving = 100, Frames = 300, WarmUpFrames = 90, Cycles = 1000;
        const int Str = 0, Sprite = 1, Cylinder = 2, Quad = 3, Kinds = 4;
        static readonly string[] KindNames = { "STR", "sprite", "cylinder", "quad" };

        sealed class FakeSource : IEffectAssetSource
        {
            public readonly Dictionary<string, EffectAsset> Assets = new Dictionary<string, EffectAsset>();
            public void Load(EffectAssetRef r, Action<EffectAsset> done) { Assets.TryGetValue(r.Guid, out var a); done(a); }
            public void Release(EffectAssetRef r) { }
        }

        readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();
        EffectRuntime runtime;
        GameObject root;
        SpriteCatalog spriteCatalog;

        T Make<T>() where T : ScriptableObject
        {
            var o = ScriptableObject.CreateInstance<T>();
            created.Add(o);
            return o;
        }

        Texture2D MakeTexture(Color color)
        {
            var t = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
            t.SetPixels(pixels);
            t.Apply();
            created.Add(t);
            return t;
        }

        [TearDown]
        public void TearDown()
        {
            runtime?.Dispose();
            runtime = null;
            spriteCatalog?.Release();
            spriteCatalog = null;
            if (root != null) UnityEngine.Object.Destroy(root);
            foreach (var o in created) if (o != null) UnityEngine.Object.Destroy(o);
            created.Clear();
        }

        // ---- synthetic assets ----

        // Three layers, 20 frames at 20 fps (1 s): each layer has a base key at 0 and 10 with a delta key that moves, turns,
        // fades and cycles two textures (anitype 3), and the second base key changes blend, so materials swap during a cycle.
        EffectAsset MakeStr(Texture2D a, Texture2D b)
        {
            var s = Make<EffectAsset>();
            s.Path = "texture/effect/alloc_test.str"; s.Kind = EffectKind.Str; s.Fps = 20f; s.FrameCount = 20;
            const int layers = 3;
            s.Layers = new EffectAsset.LayerData[layers];
            s.Textures = new Texture2D[layers * 2];
            s.TextureNames = new string[layers * 2];
            var keys = new List<EffectAsset.KeyData>();
            for (int l = 0; l < layers; l++)
            {
                s.Textures[2 * l] = a; s.Textures[2 * l + 1] = b;
                s.TextureNames[2 * l] = "a"; s.TextureNames[2 * l + 1] = "b";
                s.Layers[l] = new EffectAsset.LayerData { FirstKey = keys.Count, KeyCount = 4, FirstTexture = 2 * l, TextureCount = 2 };
                keys.Add(Base(0, 5, 6, new Vector2(320f + 20f * l, 320f)));
                keys.Add(Delta(0));
                keys.Add(Base(10, 5, 2, new Vector2(300f, 300f - 10f * l)));
                keys.Add(Delta(10));
            }
            s.Keys = keys.ToArray();
            return s;
        }

        static EffectAsset.KeyData Base(int frame, byte src, byte dst, Vector2 position) => new EffectAsset.KeyData
        {
            Frame = frame, Type = 0, SrcBlend = src, DstBlend = dst, Position = position, Angle = 0f,
            Uv0 = new Vector2(0f, 0f), Uv1 = new Vector2(1f, 1f), Uv2 = new Vector2(0f, 0f), Uv3 = new Vector2(1f, 1f),
            Xy0 = new Vector2(-32f, -32f), Xy1 = new Vector2(32f, -32f), Xy2 = new Vector2(32f, 32f), Xy3 = new Vector2(-32f, 32f),
            Color = new Vector4(255f, 255f, 255f, 255f),
        };

        static EffectAsset.KeyData Delta(int frame) => new EffectAsset.KeyData
        {
            Frame = frame, Type = 1, AniType = 3, Delay = 0.5f, Position = new Vector2(2f, -1f), Angle = 20f,
            Xy0 = new Vector2(-1f, -1f), Xy1 = new Vector2(1f, -1f), Xy2 = new Vector2(1f, 1f), Xy3 = new Vector2(-1f, 1f),
            Color = new Vector4(0f, -5f, -5f, -10f),
        };

        static AnimationCurve Fade()
        {
            var c = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.25f, 1f), new Keyframe(0.75f, 1f), new Keyframe(1f, 0f));
            return c;
        }

        EffectAsset MakeCylinder(Texture2D tex)
        {
            var c = Make<EffectAsset>();
            c.Path = "texture/effect/_authored/alloc_test/ring.cylinder"; c.Kind = EffectKind.Cylinder; c.Length = 4f;
            c.Textures = new[] { tex };
            c.Authored = new AuthoredData
            {
                Texture = 0, SrcBlend = 5, DstBlend = 2, Tint = new Color(0.5f, 0.5f, 1f), MaxAlpha = 0.4f, Alpha = Fade(),
                Rotation = AnimationCurve.Linear(0f, 0f, 1f, 90f),
                BottomRadius = 2f, TopRadius = 3.3f, Height = 1.1f, Sides = 20, RepeatU = 2f,
                RadiusScale = AnimationCurve.Linear(0f, 1f, 1f, 0f),
                HeightScale = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0f)),
            };
            return c;
        }

        EffectAsset MakeQuad(Texture2D tex)
        {
            var q = Make<EffectAsset>();
            q.Path = "texture/effect/_authored/alloc_test/sparkle.quad"; q.Kind = EffectKind.Quad; q.Length = 1f;
            q.Textures = new[] { tex };
            q.Authored = new AuthoredData
            {
                Facing = EffectFacing.Camera, Texture = 0, SrcBlend = 5, DstBlend = 2, Tint = new Color(0.9f, 1f, 0.9f),
                Alpha = Fade(), SizePixels = 50f, SizeScale = AnimationCurve.Linear(0f, 1f, 1f, 0.5f),
                Rotation = AnimationCurve.Linear(0f, 0f, 1f, 180f),
                StartMin = new Vector3(-3f, -3f, 0f), StartMax = new Vector3(3f, 3f, 0f), EndMin = new Vector3(0f, 0f, 0f), EndMax = new Vector3(0f, 0f, 4f),
            };
            return q;
        }

        // ---- the test ----

        [UnityTest]
        public IEnumerator ThreeHundredEffects_AllocateNothingPerFrame_AndStartStopCyclesAllocateNothing()
        {
            // The sprite part plays the real converted fireball (NeedsData).
            if (UnityEditor.AssetDatabase.LoadMainAssetAtPath(SpriteCatalogKeys.Index(SpriteCatalogCategory.Effects)) == null)
                Assert.Ignore("NeedsData: the effect sprite catalog is not imported (SPEC-0008); the sprite part cannot be measured");
            var load = SpriteCatalog.LoadAsync(SpriteCatalogCategory.Effects);
            yield return load;
            if (load.Status != AsyncOperationStatus.Succeeded)
                Assert.Ignore("NeedsData: the effect sprite catalog did not load through Addressables");
            spriteCatalog = load.Result;
            if (!spriteCatalog.TryGetEffect("fireball", out var fireballRef) || !fireballRef.HasAnimation)
                Assert.Ignore("NeedsData: 'fireball' is not in the effect sprite catalog");

            // Harness: camera (so every renderer is drawn and its callbacks count), runtime, moving transforms.
            root = new GameObject("EffectAllocationTestsRoot");
            var camGo = new GameObject("AllocCamera") { tag = "MainCamera" };
            camGo.transform.SetParent(root.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.transform.SetPositionAndRotation(new Vector3(0f, 25f, -35f), Quaternion.Euler(35f, 0f, 0f));
            cam.farClipPlane = 200f;

            var settings = Make<EffectSettings>();
            settings.Shader = Shader.Find("Uninarok/RoEffect");
            Assert.IsNotNull(settings.Shader, "shader Uninarok/RoEffect");
            settings.PlaceholderTexture = MakeTexture(Color.magenta);
            var texA = MakeTexture(Color.white);
            var texB = MakeTexture(new Color(1f, 0.6f, 0.2f));

            var fireball = Make<EffectAsset>();
            fireball.Path = "sprite/이팩트/fireball.act"; fireball.Kind = EffectKind.Sprite; fireball.SpriteName = "fireball"; fireball.SpriteAction = 0;
            var assets = new EffectAsset[Kinds];
            assets[Str] = MakeStr(texA, texB);
            assets[Sprite] = fireball;
            assets[Cylinder] = MakeCylinder(texA);
            assets[Quad] = MakeQuad(texB);

            var index = Make<EffectCatalogIndex>();
            index.Keys = new string[Kinds];
            index.Assets = new EffectAssetRef[Kinds];
            index.SourceMissing = new bool[Kinds];
            var source = new FakeSource();
            for (int i = 0; i < Kinds; i++)
            {
                index.Keys[i] = assets[i].Path;
                index.Assets[i] = new EffectAssetRef { Guid = "g" + i };
                source.Assets["g" + i] = assets[i];
            }
            var effectsRoot = new GameObject("Effects");
            effectsRoot.transform.SetParent(root.transform, false);
            runtime = new EffectRuntime(EffectCatalog.Create(index, source), settings, effectsRoot.transform) { SpriteCatalog = spriteCatalog };
            int warnings = 0;
            runtime.Warned += (k, m) => warnings++;

            var movers = new Transform[Moving];
            var moverBase = new Vector3[Moving];
            for (int i = 0; i < Moving; i++)
            {
                var m = new GameObject("Mover").transform;
                m.SetParent(root.transform, false);
                moverBase[i] = new Vector3(-15f + (i % 10) * 3f, 0f, 10f + (i / 10) * 3f);
                m.position = moverBase[i];
                movers[i] = m;
            }

            var frameBytes = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", Frames);
            var frameAllocs = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", Frames,
                ProfilerRecorderOptions.CollectOnlyOnCurrentThread | ProfilerRecorderOptions.SumAllSamplesInFrame | ProfilerRecorderOptions.WrapAroundWhenCapacityReached);
            var inFrameAllocs = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", 1, ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
            var samples = new List<ProfilerRecorderSample>(Frames);
            try
            {
                var baseFrameMs = new float[Frames];
                var baseTickMs = new float[Frames];
                var effFrameMs = new float[Frames];
                var effTickMs = new float[Frames];
                long tickBytes = 0, tickAllocs = 0;
                float clock = 0f;

                // ---- baseline: the same harness, no effect ----
                for (int f = 0; f < 30; f++) { clock = MoveAndTick(movers, moverBase, clock, null, null, f, out _, out _, out _); yield return null; }
                yield return null;                                    // the recorders' first (partial) frame is left out below
                for (int f = 0; f <= Frames; f++)
                {
                    clock = MoveAndTick(movers, moverBase, clock, inFrameAllocs, frameBytes, f, out float tms, out long tb, out long ta);
                    if (f < Frames) { baseTickMs[f] = tms; baseFrameMs[f] = Time.unscaledDeltaTime * 1000f; }
                    yield return null;
                }
                var baseBytes = new long[Frames]; var baseAllocs = new long[Frames];
                Copy(frameBytes, samples, baseBytes); Copy(frameAllocs, samples, baseAllocs);
                Assert.AreEqual(0, runtime.ActiveCount);

                // ---- 300 instances: 75 per kind, 25 of each on moving transforms; all looped so the load holds ----
                var handles = new EffectHandle[Instances];
                var kindCount = new int[Kinds];
                int moving = 0;
                for (int i = 0; i < Instances; i++)
                {
                    int kind = i % Kinds;
                    kindCount[kind]++;
                    if (i < Moving) handles[i] = runtime.PlayOn(new EffectRef(kind), movers[moving++], loop: true);
                    else handles[i] = runtime.Play(new EffectRef(kind), new Vector3(-20f + (i % 20) * 2f, 0f, -10f + (i / 20) * 2f), loop: true);
                    Assert.IsTrue(handles[i].IsValid, "play " + i);
                }
                Assert.AreEqual(Moving, moving, "instances on moving transforms");
                Assert.AreEqual(Instances, runtime.ActiveCount);
                for (int i = 0; i < Kinds; i++)
                {
                    var inst = runtime.InstanceOf(handles[i]);
                    Assert.IsFalse(inst.Part is PlaceholderPart, KindNames[i] + " plays its own part, not the placeholder");
                }
                Assert.IsTrue(runtime.InstanceOf(handles[Sprite]).SpritePart.CanDraw, runtime.InstanceOf(handles[Sprite]).SpritePart.Problem);

                // warm-up: pools, meshes, materials and sprite renderers sized
                for (int f = 0; f < WarmUpFrames; f++) { clock = MoveAndTick(movers, moverBase, clock, null, null, f, out _, out _, out _); yield return null; }
                yield return null;
                for (int f = 0; f <= Frames; f++)
                {
                    clock = MoveAndTick(movers, moverBase, clock, inFrameAllocs, frameBytes, f, out float tms, out long tb, out long ta);
                    if (f < Frames) { effTickMs[f] = tms; effFrameMs[f] = Time.unscaledDeltaTime * 1000f; tickBytes += tb; tickAllocs += ta; }
                    yield return null;
                }
                var effBytes = new long[Frames]; var effAllocs = new long[Frames];
                Copy(frameBytes, samples, effBytes); Copy(frameAllocs, samples, effAllocs);
                Assert.AreEqual(Instances, runtime.ActiveCount, "every looped instance still plays");

                // ---- 1,000 start and stop cycles (every kind, at a point and on a unit) after warm-up ----
                RunCycles(movers, Cycles);                            // warm-up of the cycle path (slots that change kind)
                long cBytes0 = frameBytes.CurrentValue, cAllocs0 = inFrameAllocs.Count;
                RunCycles(movers, Cycles);
                long cycleBytes = frameBytes.CurrentValue - cBytes0, cycleAllocs = inFrameAllocs.Count - cAllocs0;
                Assert.AreEqual(Instances, runtime.ActiveCount);
                // Per kind and place, so a failure names its path (main thread, exact).
                var perKind = new long[Kinds];
                for (int k = 0; k < Kinds; k++)
                {
                    long a0 = inFrameAllocs.Count;
                    for (int rep = 0; rep < 50; rep++)
                    {
                        runtime.Stop(runtime.Play(new EffectRef(k), Vector3.zero));
                        runtime.Stop(runtime.PlayOn(new EffectRef(k), movers[rep % Moving], loop: true));
                    }
                    perKind[k] = inFrameAllocs.Count - a0;
                }
                yield return null;

                // ---- report ----
                long baseTotal = Sum(baseBytes), effTotal = Sum(effBytes);
                long baseAllocTotal = Sum(baseAllocs), effAllocTotal = Sum(effAllocs);
                var sb = new StringBuilder();
                sb.AppendLine("SPEC-0012 FR-9 allocation and frame time report (EffectAllocationTests, T9.3)");
                sb.AppendLine("Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  Unity " + Application.unityVersion + "  " + (Application.isEditor ? "Editor play mode" : "player") + "  " + SystemInfo.graphicsDeviceType + "  " + SystemInfo.processorType);
                sb.AppendLine();
                sb.AppendLine("Load: " + Instances + " looped instances (" + KindNames[Str] + " " + kindCount[Str] + " (3 layers, texture and blend changes), " + KindNames[Sprite] + " " + kindCount[Sprite] + " (converted fireball), " + KindNames[Cylinder] + " " + kindCount[Cylinder] + " (20 sides), " + KindNames[Quad] + " " + kindCount[Quad] + "); " + Moving + " on moving transforms (25 per kind)");
                sb.AppendLine("Warm-up: " + WarmUpFrames + " frames with the load, and " + Cycles + " start and stop cycles before the measured " + Cycles);
                sb.AppendLine("Measured: " + Frames + " frames each; baseline = the same harness (camera, " + Moving + " moving transforms, runtime ticking) with no effect");
                sb.AppendLine("Recorders: ProfilerRecorder \"GC Allocated In Frame\" (bytes, whole frame) and \"GC.Alloc\" marker (main thread, allocations per frame); around Tick and the cycles: in-frame deltas");
                sb.AppendLine("Materials created: " + runtime.Materials.Count + "  Runtime warnings: " + warnings);
                sb.AppendLine();
                sb.AppendLine("GC, whole frame (" + Frames + " frames)          baseline      with effects");
                sb.AppendLine("  bytes total                      " + Pad(baseTotal) + "    " + Pad(effTotal));
                sb.AppendLine("  bytes max in one frame           " + Pad(Max(baseBytes)) + "    " + Pad(Max(effBytes)));
                sb.AppendLine("  main-thread allocations total    " + Pad(baseAllocTotal) + "    " + Pad(effAllocTotal));
                sb.AppendLine("  frames with any allocation       " + Pad(NonZero(baseBytes)) + "    " + Pad(NonZero(effBytes)));
                sb.AppendLine("  difference (effects - baseline)  " + (effTotal - baseTotal) + " B, " + (effAllocTotal - baseAllocTotal) + " allocations");
                sb.AppendLine("  per-frame median of main-thread allocations  " + Median(baseAllocs) + " baseline, " + Median(effAllocs) + " with effects");
                sb.AppendLine("GC around EffectRuntime.Tick (" + Frames + " ticks of " + Instances + "): " + tickAllocs + " main-thread allocations; " + tickBytes + " B process-wide");
                sb.AppendLine("GC over " + Cycles + " start and stop cycles: " + cycleAllocs + " main-thread allocations; " + cycleBytes + " B process-wide");
                sb.Append("GC per kind, 50 plays at a point + 50 on a unit, each stopped:");
                for (int k = 0; k < Kinds; k++) sb.Append(" " + KindNames[k] + " " + perKind[k]);
                sb.AppendLine(" main-thread allocations");
                sb.AppendLine("Main-thread allocations are the \"GC.Alloc\" marker (CollectOnlyOnCurrentThread): 0 there is 0 B allocated by the effect code.");
                sb.AppendLine("\"GC Allocated In Frame\" counts every thread (editor background work too), so its bytes are reported, not asserted.");
                sb.AppendLine();
                sb.AppendLine("Frame time (ms)              mean     median   p95      max");
                sb.AppendLine("  frame, baseline          " + Stats(baseFrameMs));
                sb.AppendLine("  frame, with effects      " + Stats(effFrameMs));
                sb.AppendLine("  Tick, baseline (0 inst.) " + Stats(baseTickMs));
                sb.AppendLine("  Tick, " + Instances + " instances    " + Stats(effTickMs));
                sb.AppendLine();
                sb.AppendLine("Editor numbers (Unity's editor loop, the test runner and this machine); frame time is held near 16.7 ms by the");
                sb.AppendLine("editor's frame pacing, so the Tick time is the effect cost. T10.9 is this measurement run again on the owner's request.");
                string report = sb.ToString();
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "docs", "specs", "0012-effects", "evidence", "alloc-report.txt"));
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, report.Replace("\r\n", "\n"));
                Debug.Log("[EffectAllocationTests] report written to " + path + "\n" + report);

                // ---- FR-9 ----
                Assert.AreEqual(0, tickAllocs, "GC.Alloc (main thread) around EffectRuntime.Tick");
                Assert.AreEqual(0, cycleAllocs, "GC.Alloc (main thread) over 1,000 start and stop cycles");
                for (int k = 0; k < Kinds; k++) Assert.AreEqual(0, perKind[k], "GC.Alloc (main thread) of start and stop, " + KindNames[k]);
                // Whole frame: the editor allocates every frame in both runs; effects may add nothing on top of it. One
                // allocation per frame from the effects would put the total 300 over the baseline.
                Assert.LessOrEqual(effAllocTotal, baseAllocTotal, "whole-frame main-thread allocations with effects vs the no-effect baseline");
                Assert.LessOrEqual(Median(effAllocs), Median(baseAllocs), "per-frame median of main-thread allocations vs baseline");
                Assert.AreEqual(0, warnings, "no runtime warning (every kind plays as authored)");
            }
            finally
            {
                frameBytes.Dispose();
                frameAllocs.Dispose();
                inFrameAllocs.Dispose();
            }
        }

        // Moves the transforms, ticks the runtime and measures the tick (no allocation here: preallocated arrays, structs).
        float MoveAndTick(Transform[] movers, Vector3[] moverBase, float clock, ProfilerRecorder? allocs, ProfilerRecorder? bytes, int frame,
            out float tickMs, out long tickBytes, out long tickAllocs)
        {
            float dt = Time.deltaTime;
            clock += dt;
            for (int i = 0; i < movers.Length; i++)
                movers[i].position = moverBase[i] + new Vector3(Mathf.Sin(clock * 2f + i) * 2f, 0f, Mathf.Cos(clock * 1.5f + i) * 2f);

            long b0 = bytes.HasValue ? bytes.Value.CurrentValue : 0, a0 = allocs.HasValue ? allocs.Value.Count : 0;
            long t0 = Stopwatch.GetTimestamp();
            runtime.Tick(dt);
            long t1 = Stopwatch.GetTimestamp();
            tickBytes = bytes.HasValue ? bytes.Value.CurrentValue - b0 : 0;
            tickAllocs = allocs.HasValue ? allocs.Value.Count - a0 : 0;
            tickMs = (float)((t1 - t0) * 1000.0 / Stopwatch.Frequency);
            return clock;
        }

        // Start one effect and stop it, per cycle: every kind in turn, at a point and on a moving unit by turns.
        void RunCycles(Transform[] movers, int cycles)
        {
            for (int c = 0; c < cycles; c++)
            {
                var entry = new EffectRef(c % Kinds);
                bool loop = ((c >> 3) & 1) == 0;
                var h = (c & 4) == 0
                    ? runtime.Play(entry, new Vector3(c % 7, 0f, c % 5), loop)
                    : runtime.PlayOn(entry, movers[c % movers.Length], loop);
                runtime.Stop(h);
            }
        }

        static void Copy(ProfilerRecorder r, List<ProfilerRecorderSample> into, long[] values)
        {
            into.Clear();
            r.CopyTo(into);
            Assert.GreaterOrEqual(into.Count, values.Length, "recorder samples");
            int skip = into.Count - values.Length;                 // the last complete frames
            for (int i = 0; i < values.Length; i++)
            {
                var s = into[skip + i];
                values[i] = r.UnitType == ProfilerMarkerDataUnit.TimeNanoseconds ? s.Count : s.Value;
            }
        }

        static long Sum(long[] v) { long s = 0; foreach (var x in v) s += x; return s; }
        static long Max(long[] v) { long m = 0; foreach (var x in v) m = Math.Max(m, x); return m; }
        static long Median(long[] v) { var s = (long[])v.Clone(); Array.Sort(s); return s[s.Length / 2]; }
        static int NonZero(long[] v) { int n = 0; foreach (var x in v) if (x != 0) n++; return n; }
        static string Pad(long v) => v.ToString().PadLeft(10);

        static string Stats(float[] v)
        {
            var s = (float[])v.Clone();
            Array.Sort(s);
            double sum = 0; foreach (var x in s) sum += x;
            float mean = (float)(sum / s.Length), median = s[s.Length / 2], p95 = s[Math.Min(s.Length - 1, (int)(s.Length * 0.95f))], max = s[s.Length - 1];
            return F(mean) + F(median) + F(p95) + F(max);
        }

        static string F(float v) => v.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture).PadLeft(9);
    }
}
