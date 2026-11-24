using System;
using System.Collections.Generic;
using NUnit.Framework;
using Uninarok.Map;
using UnityEngine;
using UnityEngine.TestTools;

namespace Uninarok.Effects.PlayTests
{
    // SPEC-0012 FR-4, FR-6, FR-9 (plan 4.5, D14, D29): EffectRuntime placement, clock and lifetime. Assets, index and
    // catalog are built in code; the runtime is ticked by hand with a fixed step, so no frame timing is involved.
    // Drawing (parts) is T5.5-T5.8: here an instance only keeps time and place.
    public class EffectPlayTests
    {
        const float Dt = 1f / 60f;
        const int Str1s = 0, Cylinder05s = 1, Deferred = 2, Sprite = 3, Missing = 4, ThreeLayers = 5;

        sealed class FakeSource : IEffectAssetSource
        {
            public readonly Dictionary<string, EffectAsset> Assets = new Dictionary<string, EffectAsset>();
            public readonly HashSet<string> Defer = new HashSet<string>();
            public readonly Dictionary<string, Action<EffectAsset>> Pending = new Dictionary<string, Action<EffectAsset>>();

            public void Load(EffectAssetRef r, Action<EffectAsset> done)
            {
                if (Defer.Contains(r.Guid)) { Pending[r.Guid] = done; return; }
                Assets.TryGetValue(r.Guid, out var a);
                done(a);
            }

            public void Release(EffectAssetRef r) { }

            public void Complete(string guid)
            {
                var done = Pending[guid];
                Pending.Remove(guid);
                Assets.TryGetValue(guid, out var a);
                done(a);
            }
        }

        readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();
        FakeSource source;
        EffectCatalog catalog;
        EffectSettings settings;
        GameObject root;
        EffectRuntime runtime;

        static EffectRef Ref(int i) => new EffectRef(i);

        T Make<T>() where T : ScriptableObject
        {
            var o = ScriptableObject.CreateInstance<T>();
            created.Add(o);
            return o;
        }

        [SetUp]
        public void SetUp()
        {
            source = new FakeSource();
            settings = Make<EffectSettings>();
            settings.PlaceholderSeconds = 0.25f;
            settings.AuthoredFrameRate = 60f;
            settings.Shader = Shader.Find("Uninarok/RoEffect");

            var str = Make<EffectAsset>();
            str.Path = "texture/effect/test.str"; str.Kind = EffectKind.Str; str.Fps = 10f; str.FrameCount = 10;   // 1 s
            var cyl = Make<EffectAsset>();
            cyl.Path = "texture/effect/_authored/test.cylinder"; cyl.Kind = EffectKind.Cylinder; cyl.Length = 0.5f;
            var late = Make<EffectAsset>();
            late.Path = "texture/effect/late.str"; late.Kind = EffectKind.Str; late.Fps = 10f; late.FrameCount = 5;  // 0.5 s
            var sprite = Make<EffectAsset>();
            sprite.Path = "sprite/이팩트/test.act"; sprite.Kind = EffectKind.Sprite; sprite.SpriteName = "test";

            var three = MakeThreeLayers();

            var assets = new[] { str, cyl, late, sprite, null, three };
            var index = Make<EffectCatalogIndex>();
            index.Keys = new[] { str.Path, cyl.Path, late.Path, sprite.Path, "texture/effect/missing.str", three.Path };
            index.Assets = new EffectAssetRef[assets.Length];
            index.SourceMissing = new bool[assets.Length];
            for (int i = 0; i < assets.Length; i++)
            {
                index.Assets[i] = new EffectAssetRef { Guid = "g" + i };
                if (assets[i] != null) source.Assets["g" + i] = assets[i];
            }
            source.Defer.Add("g" + Deferred);
            catalog = EffectCatalog.Create(index, source);

            root = new GameObject("EffectPlayTestsRoot");
            runtime = new EffectRuntime(catalog, settings, root.transform);
        }

        // Three layers, 10 fps, 10 frames, one white texture each:
        //   layer 0: base at frame 0 (blend 5/6, angle 0, Position (330, 300)), base at frame 5 (blend 2/2), base at frame 9
        //   layer 1: base at frame 0, angle 256 units (90 degrees)
        //   layer 2: first base key at frame 3 (hidden before it)
        EffectAsset MakeThreeLayers()
        {
            var tex = new Texture2D(2, 2);
            created.Add(tex);
            var a = Make<EffectAsset>();
            a.Path = "texture/effect/three.str"; a.Kind = EffectKind.Str; a.Fps = 10f; a.FrameCount = 10;
            a.Textures = new[] { tex }; a.TextureNames = new[] { "white" };
            a.Layers = new[]
            {
                new EffectAsset.LayerData { FirstKey = 0, KeyCount = 3, FirstTexture = 0, TextureCount = 1 },
                new EffectAsset.LayerData { FirstKey = 3, KeyCount = 1, FirstTexture = 0, TextureCount = 1 },
                new EffectAsset.LayerData { FirstKey = 4, KeyCount = 2, FirstTexture = 0, TextureCount = 1 },
            };
            a.Keys = new[]
            {
                StrKey(0, new Vector2(330f, 300f), 0f, 5, 6),
                StrKey(5, new Vector2(330f, 300f), 0f, 2, 2),
                StrKey(9, new Vector2(330f, 300f), 0f, 2, 2),
                StrKey(0, new Vector2(320f, 320f), 256f, 5, 6),
                StrKey(3, new Vector2(320f, 320f), 0f, 5, 6),
                StrKey(8, new Vector2(320f, 320f), 0f, 5, 6),
            };
            return a;
        }

        static EffectAsset.KeyData StrKey(int frame, Vector2 position, float angle, byte src, byte dst) => new EffectAsset.KeyData
        {
            Frame = frame, Type = 0, SrcBlend = src, DstBlend = dst, Position = position, Angle = angle,
            Uv0 = new Vector2(0f, 0f), Uv1 = new Vector2(1f, 1f), Uv2 = new Vector2(0f, 0f), Uv3 = new Vector2(1f, 1f),
            Xy0 = new Vector2(-32f, -32f), Xy1 = new Vector2(32f, -32f), Xy2 = new Vector2(32f, 32f), Xy3 = new Vector2(-32f, 32f),
            Color = new Vector4(255f, 255f, 255f, 255f),
        };

        // Draws the three-layer asset at one effect second (TimeScale 0: the tick draws without moving the clock).
        EffectMeshWriter DrawThreeLayersAt(float seconds, out EffectInstance inst)
        {
            runtime.TimeScale = 0f;
            var h = runtime.Play(Ref(ThreeLayers), Vector3.zero, loop: true);
            runtime.Seek(h, seconds);
            runtime.Tick(Dt);
            inst = runtime.InstanceOf(h);
            return inst.GetWriter();
        }

        // pixels (y down) to object-space units, by hand from plan 4.5: x / (35 / 0.5), y flipped, lowered 0.5 x 0.5
        static Vector3 Units(float px, float py) => new Vector3(px * 0.5f / 35f, -py * 0.5f / 35f - 0.25f, 0f);

        // ---- STR drawing (FR-2, FR-3) ----

        [Test]
        public void Str_ThreeLayers_GiveThreeSubmeshes_AndPlaceLayerZeroAtFrameZero()
        {
            var w = DrawThreeLayersAt(0f, out _);

            Assert.AreEqual(3, w.SubMeshCount);
            Assert.AreEqual(12, w.VertexCount);
            // layer 0, frame 0: Xy (+-32) + (Position - Pivot) = + (10, -20), no rotation
            var expected = new[] { Units(-22f, -52f), Units(42f, -52f), Units(42f, 12f), Units(-22f, 12f) };
            for (int i = 0; i < 4; i++)
                Assert.That(Vector3.Distance(expected[i], w.Vertices[i].Position), Is.LessThan(1e-5f), "corner " + i);
            // uv: the full rectangle, v counted from the image's top row
            Assert.AreEqual(new Vector2(0f, 1f), w.Vertices[0].Uv);
            Assert.AreEqual(new Vector2(1f, 1f), w.Vertices[1].Uv);
            Assert.AreEqual(new Vector2(1f, 0f), w.Vertices[2].Uv);
            Assert.AreEqual(new Vector2(0f, 0f), w.Vertices[3].Uv);
            Assert.AreEqual(new Color32(255, 255, 255, 255), w.Vertices[0].Color);
        }

        [Test]
        public void Str_LayerOne_IsRotatedByItsAngle()
        {
            var w = DrawThreeLayersAt(0f, out _);
            // 256 of 1024 units = 90 degrees: (x, y) -> (-y, x) on the pixel coordinates; Position = Pivot
            var expected = new[] { Units(32f, -32f), Units(32f, 32f), Units(-32f, 32f), Units(-32f, -32f) };
            for (int i = 0; i < 4; i++)
                Assert.That(Vector3.Distance(expected[i], w.Vertices[4 + i].Position), Is.LessThan(1e-5f), "corner " + i);
        }

        [Test]
        public void Str_LayerHiddenBeforeItsFirstKey_ThenShown()
        {
            var w = DrawThreeLayersAt(0.1f, out var inst);          // frame 1, layer 2 starts at frame 3
            for (int i = 8; i < 12; i++) Assert.AreEqual(default(EffectVertex), w.Vertices[i], "layer 2 hidden at frame 1");
            Assert.AreNotEqual(default(EffectVertex), w.Vertices[0], "layer 0 shown");

            runtime.Seek(inst.Handle, 0.4f);
            runtime.Tick(Dt);
            Assert.AreNotEqual(default(EffectVertex), w.Vertices[8], "layer 2 shown at frame 4");
        }

        [Test]
        public void Str_BlendChangeAtAKey_SwapsTheSubmeshMaterial()
        {
            var w = DrawThreeLayersAt(0.1f, out var inst);
            var renderer = inst.Object.GetComponent<MeshRenderer>();
            var before = renderer.sharedMaterials[0];
            int uploads = w.MaterialUploads;
            Assert.AreEqual(3, renderer.sharedMaterials.Length);
            Assert.AreEqual((float)UnityEngine.Rendering.BlendMode.SrcAlpha, before.GetFloat("_SrcBlend"));
            Assert.IsTrue(before.IsKeywordEnabled("_FACING_CAMERA"));

            runtime.Seek(inst.Handle, 0.6f);                        // frame 6: past layer 0's second base key
            runtime.Tick(Dt);

            var after = renderer.sharedMaterials[0];
            Assert.AreNotSame(before, after);
            Assert.AreEqual((float)UnityEngine.Rendering.BlendMode.One, after.GetFloat("_SrcBlend"));
            Assert.AreEqual((float)UnityEngine.Rendering.BlendMode.One, after.GetFloat("_DstBlend"));
            Assert.AreSame(renderer.sharedMaterials[2], before, "layer 2 keeps the first blend");
            Assert.AreEqual(uploads + 1, w.MaterialUploads, "one renderer update for the swap");
            runtime.Tick(Dt);
            Assert.AreEqual(uploads + 1, w.MaterialUploads, "none while nothing changes");
        }

        [TearDown]
        public void TearDown()
        {
            runtime?.Dispose();
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            foreach (var o in created) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            created.Clear();
        }

        int TicksUntilInactive(EffectHandle h, int max = 10000)
        {
            for (int n = 1; n <= max; n++)
            {
                runtime.Tick(Dt);
                if (!runtime.IsActive(h)) return n;
            }
            return -1;
        }

        // ---- placement (FR-6) ----

        [Test]
        public void Play_AtPoint_PlacesTheInstanceThere()
        {
            var p = new Vector3(1f, 2f, 3f);
            var h = runtime.Play(Ref(Str1s), p);

            Assert.IsTrue(h.IsValid);
            Assert.IsTrue(runtime.IsActive(h));
            Assert.AreEqual(1, runtime.ActiveCount);
            var inst = runtime.InstanceOf(h);
            Assert.AreEqual(EffectPlace.Point, inst.Place);
            Assert.AreEqual(p, inst.Position);
            Assert.AreEqual(p, inst.Object.transform.position);
            Assert.AreEqual(root.transform, inst.Object.transform.parent);
            runtime.Tick(Dt);
            Assert.AreEqual(p, inst.Object.transform.position);
        }

        [Test]
        public void PlayAtCell_UsesMapCellCenter()
        {
            const int n = 4;
            var map = Make<MapData>();
            var heights = new float[n * n * 4];
            for (int i = 0; i < heights.Length; i++) heights[i] = 0.75f;
            map.Assign("small", n, n, 0.5f, new Vector2(10f, 20f), 1f, false, heights, new byte[n * n], new byte[n * n], new byte[n * n]);

            var h = runtime.PlayAtCell(Ref(Str1s), map, 2, 3);

            var inst = runtime.InstanceOf(h);
            Assert.AreEqual(EffectPlace.Cell, inst.Place);
            Assert.AreEqual(map.CellCenter(2, 3), inst.Position);
            var expected = new Vector3(10f + 2.5f * 0.5f, 0.75f, 20f + 3.5f * 0.5f);
            Assert.That(Vector3.Distance(expected, inst.Object.transform.position), Is.LessThan(1e-5f));
            Assert.IsFalse(runtime.PlayAtCell(Ref(Str1s), null, 0, 0).IsValid, "a null map plays nothing");
        }

        [Test]
        public void PlayOn_FollowsPositionNotRotation()
        {
            var unit = new GameObject("unit");
            try
            {
                unit.transform.position = new Vector3(1f, 0f, 1f);
                var h = runtime.PlayOn(Ref(Str1s), unit.transform, loop: true);
                var inst = runtime.InstanceOf(h);
                Assert.AreEqual(EffectPlace.Unit, inst.Place);
                Assert.AreEqual(unit.transform.position, inst.Object.transform.position);

                for (int i = 1; i <= 5; i++)
                {
                    unit.transform.SetPositionAndRotation(new Vector3(i, 0.5f * i, -i), Quaternion.Euler(0f, 37f * i, 10f));
                    runtime.Tick(Dt);
                    Assert.AreEqual(unit.transform.position, inst.Object.transform.position);
                    Assert.AreEqual(Quaternion.identity, inst.Object.transform.rotation, "rotation is not followed");
                }

                var last = unit.transform.position;
                UnityEngine.Object.DestroyImmediate(unit);
                runtime.Tick(Dt);
                Assert.IsTrue(runtime.IsActive(h), "a destroyed unit leaves the instance where it was (plan 5)");
                Assert.AreEqual(last, inst.Object.transform.position);
                Assert.IsFalse(runtime.PlayOn(Ref(Str1s), null).IsValid, "a null unit plays nothing");
            }
            finally
            {
                if (unit != null) UnityEngine.Object.DestroyImmediate(unit);
            }
        }

        // ---- lifetime (FR-6) ----

        [Test]
        public void OneShot_EndsAfterItsLength_WithinOneFrame()
        {
            var str = runtime.Play(Ref(Str1s), Vector3.zero);
            Assert.That(TicksUntilInactive(str), Is.InRange(59, 61), "STR: FrameCount / Fps = 1 s");
            Assert.AreEqual(0, runtime.ActiveCount);

            var cyl = runtime.Play(Ref(Cylinder05s), Vector3.zero);
            Assert.That(TicksUntilInactive(cyl), Is.InRange(29, 31), "authored: Length = 0.5 s");
            Assert.AreEqual(0, runtime.ActiveCount);
        }

        [Test]
        public void Loop_Wraps_AndCountsCycles()
        {
            var h = runtime.Play(Ref(Cylinder05s), Vector3.zero, loop: true);
            for (int i = 0; i < 200; i++) runtime.Tick(Dt);   // 3.33 s
            var inst = runtime.InstanceOf(h);
            Assert.IsNotNull(inst, "a loop does not end");
            Assert.That(inst.Age, Is.GreaterThanOrEqualTo(0f).And.LessThan(0.5f));
            Assert.AreEqual(6, inst.Cycle);
            Assert.That(inst.Age, Is.EqualTo(200f * Dt - 3f).Within(1e-3f));
        }

        [Test]
        public void Stop_OnLoop_EndsAtOnce()
        {
            var a = runtime.Play(Ref(Str1s), Vector3.zero, loop: true);
            var b = runtime.Play(Ref(Str1s), Vector3.one, loop: true);
            for (int i = 0; i < 90; i++) runtime.Tick(Dt);
            var obj = runtime.InstanceOf(a).Object;
            Assert.AreEqual(2, runtime.ActiveCount);

            runtime.Stop(a);

            Assert.IsFalse(runtime.IsActive(a), "inactive in the same call");
            Assert.AreEqual(1, runtime.ActiveCount);
            Assert.IsFalse(obj.activeSelf, "nothing of it draws in the next frame");
            Assert.IsTrue(runtime.IsActive(b));
            runtime.Stop(a);                                   // a second stop does nothing
            Assert.AreEqual(1, runtime.ActiveCount);
        }

        [Test]
        public void StopAll_LeavesNothingActive()
        {
            for (int i = 0; i < 10; i++) runtime.Play(Ref(i % 2 == 0 ? Str1s : Cylinder05s), Vector3.zero, loop: i % 3 == 0);
            runtime.Play(Ref(Deferred), Vector3.zero);
            Assert.AreEqual(11, runtime.ActiveCount);
            runtime.Tick(Dt);

            runtime.StopAll();

            Assert.AreEqual(0, runtime.ActiveCount);
            for (int i = 0; i < root.transform.childCount; i++) Assert.IsFalse(root.transform.GetChild(i).gameObject.activeSelf);
        }

        [Test]
        public void StaleHandle_IsNotActive_AfterSlotReuse()
        {
            Assert.IsFalse(runtime.IsActive(default), "default is never active");
            Assert.IsFalse(default(EffectHandle).IsValid);

            var a = runtime.Play(Ref(Str1s), Vector3.zero, loop: true);
            runtime.Stop(a);
            var b = runtime.Play(Ref(Str1s), Vector3.zero, loop: true);

            Assert.AreEqual(a.Slot, b.Slot, "the freed slot is reused");
            Assert.AreNotEqual(a.Generation, b.Generation);
            Assert.IsFalse(runtime.IsActive(a));
            Assert.IsTrue(runtime.IsActive(b));
            runtime.Stop(a);
            runtime.SetSpeed(a, 5f);
            Assert.IsTrue(runtime.IsActive(b), "a stale handle cannot stop the new play");
            Assert.AreEqual(1f, runtime.SpeedOf(b), "nor change it");
            Assert.AreEqual(0f, runtime.SpeedOf(a));
            Assert.AreEqual(-1f, runtime.FrameOf(a));
        }

        [Test]
        public void FullPool_ReturnsDefault_WithOneWarning()
        {
            var small = new EffectRuntime(catalog, settings, root.transform, capacity: 2);
            try
            {
                Assert.IsTrue(small.Play(Ref(Str1s), Vector3.zero).IsValid);
                Assert.IsTrue(small.Play(Ref(Str1s), Vector3.zero).IsValid);
                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("all 2 instance slots are in use"));
                Assert.IsFalse(small.Play(Ref(Str1s), Vector3.zero).IsValid);
                Assert.IsFalse(small.Play(Ref(Str1s), Vector3.zero).IsValid);   // no second warning
                Assert.AreEqual(2, small.ActiveCount);
            }
            finally
            {
                small.Dispose();
            }
        }

        // ---- clock (FR-4, D14) ----

        [Test]
        public void SetSpeed_ScalesTheClock()
        {
            var h = runtime.Play(Ref(Str1s), Vector3.zero);
            Assert.AreEqual(1f, runtime.SpeedOf(h), "default 1");
            runtime.SetSpeed(h, 2f);
            Assert.AreEqual(2f, runtime.SpeedOf(h));
            Assert.That(TicksUntilInactive(h), Is.InRange(29, 31), "speed 2 halves the wall time");

            var slow = runtime.Play(Ref(Cylinder05s), Vector3.zero);
            runtime.SetSpeed(slow, 0.5f);
            Assert.That(TicksUntilInactive(slow), Is.InRange(59, 61), "speed 0.5 doubles it");

            var neg = runtime.Play(Ref(Str1s), Vector3.zero);
            runtime.SetSpeed(neg, -3f);
            Assert.AreEqual(0f, runtime.SpeedOf(neg), "clamped at 0");
            runtime.SetSpeed(neg, float.NaN);
            Assert.AreEqual(0f, runtime.SpeedOf(neg));
        }

        [Test]
        public void Seek_SetsAgeExactly_AndFrameOfFollows()
        {
            var h = runtime.Play(Ref(Str1s), Vector3.zero);       // 10 fps
            runtime.Seek(h, 0.55f);
            Assert.That(runtime.FrameOf(h), Is.EqualTo(5.5f).Within(1e-4f));
            Assert.IsTrue(runtime.InstanceOf(h).Seeked, "cursors reset on the next part tick");
            runtime.Tick(Dt);
            Assert.That(runtime.FrameOf(h), Is.EqualTo((0.55f + Dt) * 10f).Within(1e-4f));
            Assert.IsFalse(runtime.InstanceOf(h).Seeked);

            runtime.Seek(h, 5f);
            Assert.That(runtime.FrameOf(h), Is.EqualTo(10f).Within(1e-4f), "a one-shot is clamped to its length");

            var loop = runtime.Play(Ref(Str1s), Vector3.zero, loop: true);
            runtime.Seek(loop, 2.3f);
            var inst = runtime.InstanceOf(loop);
            Assert.AreEqual(2, inst.Cycle);
            Assert.That(inst.Age, Is.EqualTo(0.3f).Within(1e-4f));
            Assert.That(runtime.FrameOf(loop), Is.EqualTo(3f).Within(1e-3f));

            var cyl = runtime.Play(Ref(Cylinder05s), Vector3.zero);
            runtime.Seek(cyl, 0.25f);
            Assert.That(runtime.FrameOf(cyl), Is.EqualTo(15f).Within(1e-3f), "authored: Age × AuthoredFrameRate");
        }

        [Test]
        public void TimeScaleZero_FreezesAge()
        {
            var h = runtime.Play(Ref(Str1s), Vector3.zero);
            runtime.Tick(Dt);
            float age = runtime.InstanceOf(h).Age;

            runtime.TimeScale = 0f;
            for (int i = 0; i < 600; i++) runtime.Tick(Dt);
            Assert.IsTrue(runtime.IsActive(h), "paused: the one-shot does not end");
            Assert.AreEqual(age, runtime.InstanceOf(h).Age);

            runtime.Seek(h, 1f);                                     // seek to the end while paused: still shown
            runtime.Tick(Dt);
            Assert.IsTrue(runtime.IsActive(h));

            runtime.TimeScale = 1f;
            runtime.Tick(Dt);
            Assert.IsFalse(runtime.IsActive(h), "ends once its clock moves");

            runtime.TimeScale = -1f;
            Assert.AreEqual(0f, runtime.TimeScale);
            runtime.TimeScale = 2f;
            var fast = runtime.Play(Ref(Str1s), Vector3.zero);
            Assert.That(TicksUntilInactive(fast), Is.InRange(29, 31), "TimeScale multiplies the clock");
        }

        // ---- waiting for the asset (FR-6, plan 5, D29) ----

        [Test]
        public void Instance_WaitsForItsAsset_ThenStarts()
        {
            var h = runtime.Play(Ref(Deferred), Vector3.zero);
            Assert.IsTrue(runtime.IsActive(h), "a waiting instance is active");
            Assert.AreEqual(EffectAssetState.Loading, catalog.GetState(Ref(Deferred)));
            for (int i = 0; i < 100; i++) runtime.Tick(Dt);
            var inst = runtime.InstanceOf(h);
            Assert.IsNotNull(inst, "it does not end while waiting");
            Assert.AreEqual(0f, inst.Age, "its clock has not started");
            Assert.IsNull(inst.Asset);

            source.Complete("g" + Deferred);
            runtime.Tick(Dt);

            Assert.IsNotNull(inst.Asset);
            Assert.That(inst.Age, Is.EqualTo(Dt).Within(1e-6f), "the clock starts on the tick it binds");
            Assert.That(inst.Length, Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(TicksUntilInactive(h) + 1, Is.InRange(29, 31), "then plays its whole length");
        }

        [Test]
        public void StoppedWhileWaiting_LeavesNothingBehind()
        {
            var a = runtime.Play(Ref(Deferred), Vector3.zero);
            runtime.Tick(Dt);
            runtime.Stop(a);
            Assert.AreEqual(0, runtime.ActiveCount);

            var b = runtime.Play(Ref(Str1s), Vector3.zero, loop: true);   // reuses a's slot
            Assert.AreEqual(a.Slot, b.Slot);
            source.Complete("g" + Deferred);                               // the old load lands now
            runtime.Tick(Dt);

            Assert.AreEqual(1, runtime.ActiveCount);
            Assert.IsFalse(runtime.IsActive(a));
            var inst = runtime.InstanceOf(b);
            Assert.AreEqual(EffectKind.Str, inst.Asset.Kind);
            Assert.AreEqual(1f, inst.Length, "b keeps its own asset");
        }

        [Test]
        public void Length_FallsBackToPlaceholderSeconds()
        {
            var sprite = runtime.Play(Ref(Sprite), Vector3.zero);
            Assert.That(runtime.InstanceOf(sprite).Length, Is.EqualTo(0.25f), "sprite with no part yet (T5.7 supplies it)");

            var missing = runtime.Play(Ref(Missing), Vector3.zero);
            Assert.IsTrue(runtime.IsActive(missing), "a failed load plays the placeholder rule");
            Assert.That(runtime.InstanceOf(missing).Length, Is.EqualTo(0.25f));
            Assert.That(TicksUntilInactive(missing), Is.InRange(14, 16));

            var bad = Make<EffectAsset>();
            bad.Kind = EffectKind.Str; bad.Fps = 0f; bad.FrameCount = 10;
            Assert.AreEqual(0.25f, EffectRuntime.LengthOf(bad, 0f, settings), "fps 0");
            bad.Kind = EffectKind.Quad; bad.Length = float.PositiveInfinity;
            Assert.AreEqual(0.25f, EffectRuntime.LengthOf(bad, 0f, settings), "non-finite");
            bad.Kind = EffectKind.Procedural; bad.Length = 2f;
            Assert.AreEqual(0.25f, EffectRuntime.LengthOf(bad, 0f, settings), "procedural: empty registry (D20)");
            bad.Kind = EffectKind.Model;
            Assert.AreEqual(2f, EffectRuntime.LengthOf(bad, 0f, settings));
            Assert.AreEqual(0.8f, EffectRuntime.LengthOf(bad, 0.8f, settings), "the part's own length wins");
        }
    }
}
