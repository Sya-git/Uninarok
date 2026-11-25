using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Uninarok.Catalog;
using Uninarok.Sprites;
using UnityEngine;
using UnityEngine.TestTools;

namespace Uninarok.Effects.PlayTests
{
    // SPEC-0012 FR-4, FR-6, FR-11 (plan 4.5 "player clock and speed", D14): the play-speed multiplier on one clock for every kind.
    // Age += dt * TimeScale * Speed, so the runtime is ticked by hand with a fixed step: "wall time T" is T / Dt ticks and
    // no frame timing is involved. Assets are in-memory (STR from the real importer does not exist yet); the sprite case
    // uses an in-memory animation, plus one case on converted data (NeedsData).
    public class EffectSpeedPlayTests
    {
        const float Dt = 1f / 60f;
        const int Str1s = 0, Cylinder05s = 1, ThreeLayers = 2, SpriteOneShot = 3;

        sealed class FakeSource : IEffectAssetSource
        {
            public readonly Dictionary<string, EffectAsset> Assets = new Dictionary<string, EffectAsset>();
            public void Load(EffectAssetRef r, Action<EffectAsset> done) { Assets.TryGetValue(r.Guid, out var a); done(a); }
            public void Release(EffectAssetRef r) { }
        }

        readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();
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
            settings = Make<EffectSettings>();
            settings.PlaceholderSeconds = 0.25f;
            settings.AuthoredFrameRate = 60f;
            settings.Shader = Shader.Find("Uninarok/RoEffect");

            var str = Make<EffectAsset>();
            str.Path = "texture/effect/test.str"; str.Kind = EffectKind.Str; str.Fps = 10f; str.FrameCount = 10;   // 1 s
            var cyl = Make<EffectAsset>();
            cyl.Path = "texture/effect/_authored/test.cylinder"; cyl.Kind = EffectKind.Cylinder; cyl.Length = 0.5f;
            cyl.Authored = new AuthoredData
            {
                BottomRadius = 1f, TopRadius = 2f, Height = 4f, Sides = 8,
                RadiusScale = AnimationCurve.Linear(0f, 1f, 1f, 2f), HeightScale = AnimationCurve.Linear(0f, 1f, 1f, 2f),
            };
            var three = MakeThreeLayers();
            var sprite = Make<EffectAsset>();
            sprite.Path = "sprite/이팩트/fake0.act"; sprite.Kind = EffectKind.Sprite; sprite.SpriteName = "fake"; sprite.SpriteAction = 0;

            var assets = new[] { str, cyl, three, sprite };
            var source = new FakeSource();
            var index = Make<EffectCatalogIndex>();
            index.Keys = new string[assets.Length];
            index.Assets = new EffectAssetRef[assets.Length];
            index.SourceMissing = new bool[assets.Length];
            for (int i = 0; i < assets.Length; i++)
            {
                index.Keys[i] = assets[i].Path;
                index.Assets[i] = new EffectAssetRef { Guid = "g" + i };
                source.Assets["g" + i] = assets[i];
            }
            root = new GameObject("EffectSpeedPlayTestsRoot");
            runtime = new EffectRuntime(EffectCatalog.Create(index, source), settings, root.transform);
            var animation = MakeAnimation();
            runtime.SpriteAnimations.Resolver = name => name == "fake" ? animation : null;
        }

        [TearDown]
        public void TearDown()
        {
            runtime?.Dispose();
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            foreach (var o in created) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            created.Clear();
        }

        // 10 fps, 10 frames; one layer with base keys at frames 0, 4 and 7 (base keys hold), so the picture depends on the frame.
        EffectAsset MakeThreeLayers()
        {
            var tex = new Texture2D(2, 2);
            created.Add(tex);
            var a = Make<EffectAsset>();
            a.Path = "texture/effect/three.str"; a.Kind = EffectKind.Str; a.Fps = 10f; a.FrameCount = 10;
            a.Textures = new[] { tex }; a.TextureNames = new[] { "white" };
            a.Layers = new[] { new EffectAsset.LayerData { FirstKey = 0, KeyCount = 3, FirstTexture = 0, TextureCount = 1 } };
            var k0 = StrKey(0, new Vector2(320f, 320f), 0f, new Vector4(255f, 255f, 255f, 255f));
            var k1 = StrKey(4, new Vector2(420f, 360f), 128f, new Vector4(255f, 128f, 0f, 64f));
            var k2 = StrKey(7, new Vector2(300f, 280f), 64f, new Vector4(0f, 128f, 255f, 200f));
            a.Keys = new[] { k0, k1, k2 };
            return a;
        }

        static EffectAsset.KeyData StrKey(int frame, Vector2 position, float angle, Vector4 color) => new EffectAsset.KeyData
        {
            Frame = frame, Type = 0, SrcBlend = 5, DstBlend = 6, Position = position, Angle = angle,
            Uv0 = new Vector2(0f, 0f), Uv1 = new Vector2(1f, 1f), Uv2 = new Vector2(0f, 0f), Uv3 = new Vector2(1f, 1f),
            Xy0 = new Vector2(-32f, -32f), Xy1 = new Vector2(32f, -32f), Xy2 = new Vector2(32f, 32f), Xy3 = new Vector2(-32f, 32f),
            Color = color,
        };

        // 4 frames of 100 ms (0.4 s), one layer per frame.
        SpriteAnimation MakeAnimation()
        {
            var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            created.Add(texture);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 10f);
            created.Add(sprite);
            var a = Make<SpriteAnimation>();
            a.Sprites = new[] { sprite };
            a.PixelsPerUnit = 10f;
            a.FileActionCount = 1;
            a.Directional = false;
            a.ActionMap = new short[] { 0 };
            a.Actions = new[] { new SpriteAnimation.ActionData { FirstFrame = 0, FrameCount = 4, FrameMs = 100f } };
            a.Frames = new SpriteAnimation.FrameData[4];
            a.Layers = new SpriteAnimation.LayerData[4];
            for (int i = 0; i < 4; i++)
            {
                a.Frames[i] = new SpriteAnimation.FrameData { FirstLayer = i, LayerCount = 1, EventId = -1 };
                a.Layers[i] = new SpriteAnimation.LayerData { Slot = 0, ScaleX = 1f, ScaleY = 1f, Tint = new Color32(255, 255, 255, 255) };
            }
            a.MaxLayers = 1;
            return a;
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

        int WallTicksOf(int entry, float speed)
        {
            var h = runtime.Play(Ref(entry), Vector3.zero);
            runtime.SetSpeed(h, speed);
            return TicksUntilInactive(h);
        }

        // ---- end time (FR-4, FR-6) ----

        [Test]
        public void OneShot_AtSpeed2_EndsAtHalfTheWallTime_ForStrAndCylinder()
        {
            int str1 = WallTicksOf(Str1s, 1f), str2 = WallTicksOf(Str1s, 2f);
            Assert.That(str1, Is.InRange(59, 61), "STR at speed 1: 1 s");
            Assert.That(str2, Is.InRange(str1 / 2 - 1, str1 / 2 + 1), "STR at speed 2: half");

            int cyl1 = WallTicksOf(Cylinder05s, 1f), cyl2 = WallTicksOf(Cylinder05s, 2f);
            Assert.That(cyl1, Is.InRange(29, 31), "cylinder at speed 1: 0.5 s");
            Assert.That(cyl2, Is.InRange(cyl1 / 2 - 1, cyl1 / 2 + 1), "cylinder at speed 2: half");
            Assert.AreEqual(0, runtime.ActiveCount);
        }

        [Test]
        public void Speed05_DoublesOnePlay_ForStrAndCylinder()
        {
            int str1 = WallTicksOf(Str1s, 1f), strHalf = WallTicksOf(Str1s, 0.5f);
            Assert.That(strHalf, Is.InRange(str1 * 2 - 1, str1 * 2 + 1), "STR");
            int cyl1 = WallTicksOf(Cylinder05s, 1f), cylHalf = WallTicksOf(Cylinder05s, 0.5f);
            Assert.That(cylHalf, Is.InRange(cyl1 * 2 - 1, cyl1 * 2 + 1), "cylinder");
        }

        // ---- cycle index of a loop ----

        [Test]
        public void Loop_CycleIndexAtSpeed2AfterT_EqualsSpeed1After2T()
        {
            foreach (int entry in new[] { Str1s, Cylinder05s })
            {
                var fast = runtime.Play(Ref(entry), Vector3.zero, loop: true);
                runtime.SetSpeed(fast, 2f);
                for (int i = 0; i < 100; i++) runtime.Tick(Dt);               // T = 1.667 s
                var fi = runtime.InstanceOf(fast);
                int cycleFast = fi.Cycle; float ageFast = fi.Age;
                runtime.Stop(fast);

                var slow = runtime.Play(Ref(entry), Vector3.zero, loop: true);
                for (int i = 0; i < 200; i++) runtime.Tick(Dt);               // 2T
                var si = runtime.InstanceOf(slow);

                Assert.AreEqual(si.Cycle, cycleFast, "cycle index, entry " + entry);
                Assert.That(ageFast, Is.EqualTo(si.Age).Within(1e-3f), "position in the cycle, entry " + entry);
                runtime.Stop(slow);
            }
        }

        // ---- the picture ----

        static Vector3[] Positions(EffectMeshWriter w, out Color32[] colors)
        {
            var p = new Vector3[w.VertexCount];
            colors = new Color32[w.VertexCount];
            for (int i = 0; i < p.Length; i++) { p[i] = w.Vertices[i].Position; colors[i] = w.Vertices[i].Color; }
            return p;
        }

        void AssertSamePicture(int entry, int fastTicks)
        {
            var fast = runtime.Play(Ref(entry), Vector3.zero, loop: true);
            runtime.SetSpeed(fast, 2f);
            for (int i = 0; i < fastTicks; i++) runtime.Tick(Dt);
            var a = Positions(runtime.InstanceOf(fast).GetWriter(), out var ca);
            runtime.Stop(fast);

            var slow = runtime.Play(Ref(entry), Vector3.zero, loop: true);
            for (int i = 0; i < fastTicks * 2; i++) runtime.Tick(Dt);
            var b = Positions(runtime.InstanceOf(slow).GetWriter(), out var cb);
            runtime.Stop(slow);

            Assert.AreEqual(b.Length, a.Length, "vertex count, entry " + entry);
            Assert.Greater(a.Length, 0);
            for (int i = 0; i < a.Length; i++)
            {
                Assert.That(Vector3.Distance(a[i], b[i]), Is.LessThan(1e-3f), "vertex " + i + " of entry " + entry);
                Assert.That(Mathf.Abs(ca[i].r - cb[i].r) + Mathf.Abs(ca[i].g - cb[i].g) + Mathf.Abs(ca[i].a - cb[i].a), Is.LessThanOrEqualTo(2), "colour " + i);
            }
        }

        [Test]
        public void Picture_AtSpeed2AndT_EqualsSpeed1At2T_ForStr()
        {
            AssertSamePicture(ThreeLayers, 24);                  // T = 0.4 s: 0.8 s of effect time, frame 8, past the last key
            // and it is not a constant picture: a different time gives a different one
            var h = runtime.Play(Ref(ThreeLayers), Vector3.zero, loop: true);
            runtime.Tick(Dt);
            var early = Positions(runtime.InstanceOf(h).GetWriter(), out _);
            for (int i = 0; i < 30; i++) runtime.Tick(Dt);
            var later = Positions(runtime.InstanceOf(h).GetWriter(), out _);
            Assert.Greater(Vector3.Distance(early[0], later[0]), 1e-3f, "the STR picture moves with time");
        }

        [Test]
        public void Picture_AtSpeed2AndT_EqualsSpeed1At2T_ForCylinder()
        {
            AssertSamePicture(Cylinder05s, 10);                  // T = 0.167 s: radii rise with t
            var h = runtime.Play(Ref(Cylinder05s), Vector3.zero, loop: true);
            runtime.Tick(Dt);
            var early = Positions(runtime.InstanceOf(h).GetWriter(), out _);
            for (int i = 0; i < 10; i++) runtime.Tick(Dt);
            var later = Positions(runtime.InstanceOf(h).GetWriter(), out _);
            Assert.Greater(Vector3.Distance(early[1], later[1]), 1e-3f, "the cylinder radii change with time");
        }

        // ---- pause ----

        [Test]
        public void TimeScaleZero_FreezesAge_AtAnySpeed()
        {
            var h = runtime.Play(Ref(Cylinder05s), Vector3.zero, loop: true);
            runtime.SetSpeed(h, 2f);
            runtime.Tick(Dt);
            var inst = runtime.InstanceOf(h);
            float age = inst.Age; int cycle = inst.Cycle;
            Assert.Greater(age, 0f);

            runtime.TimeScale = 0f;
            for (int i = 0; i < 300; i++) runtime.Tick(Dt);
            Assert.AreEqual(age, inst.Age);
            Assert.AreEqual(cycle, inst.Cycle);
            Assert.IsTrue(runtime.IsActive(h));
            Assert.AreEqual(2f, runtime.SpeedOf(h), "the speed is kept");
        }

        // ---- sprite (plan: NeedsData; the stepping itself is checked on an in-memory animation) ----

        [Test]
        public void Sprite_AtSpeed2_StepsItsActorTwiceAsFast()
        {
            var slow = runtime.Play(Ref(SpriteOneShot), Vector3.zero, loop: true);
            var fast = runtime.Play(Ref(SpriteOneShot), Vector3.zero, loop: true);
            runtime.SetSpeed(fast, 2f);
            var slowActor = runtime.InstanceOf(slow).SpritePart.Actor;
            var fastActor = runtime.InstanceOf(fast).SpritePart.Actor;

            runtime.Tick(0.11f);                                   // slow at 0.11 s (frame 1), fast at 0.22 s (frame 2)
            Assert.AreEqual(1, slowActor.CurrentFrame);
            Assert.AreEqual(2, fastActor.CurrentFrame);
            runtime.Tick(0.11f);                                   // 0.22 s (frame 2), 0.44 s: the next cycle, 0.04 s in (frame 0)
            Assert.AreEqual(2, slowActor.CurrentFrame);
            Assert.AreEqual(0, fastActor.CurrentFrame);
            Assert.AreEqual(1, runtime.InstanceOf(fast).Cycle);
            Assert.AreEqual(0, runtime.InstanceOf(slow).Cycle);
        }

        // On converted data: the same relation on the real fireball animation (NeedsData: skipped when the catalog is not imported).
        [UnityTest]
        public IEnumerator Sprite_Fireball_AtSpeed2AndT_SitsOnTheFrameOfSpeed1At2T()
        {
            if (UnityEditor.AssetDatabase.LoadMainAssetAtPath(SpriteCatalogKeys.Index(SpriteCatalogCategory.Effects)) == null)
                Assert.Ignore("NeedsData: the effect sprite catalog is not imported (SPEC-0008)");

            var load = SpriteCatalog.LoadAsync(SpriteCatalogCategory.Effects);
            yield return load;
            if (load.Status != UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
                Assert.Ignore("NeedsData: the effect catalog did not load through Addressables");
            var spriteCatalog = load.Result;
            if (!spriteCatalog.TryGetEffect("fireball", out var reference) || !reference.HasAnimation)
            {
                spriteCatalog.Release();
                Assert.Ignore("NeedsData: 'fireball' is not in the effect catalog");
            }

            try
            {
                var entry = Make<EffectAsset>();
                entry.Path = "sprite/이팩트/fireball.act"; entry.Kind = EffectKind.Sprite; entry.SpriteName = "fireball"; entry.SpriteAction = 0;
                var index = Make<EffectCatalogIndex>();
                index.Keys = new[] { entry.Path };
                index.Assets = new[] { new EffectAssetRef { Guid = "g0" } };
                index.SourceMissing = new bool[1];
                var source = new FakeSource();
                source.Assets["g0"] = entry;
                runtime.Dispose();
                runtime = new EffectRuntime(EffectCatalog.Create(index, source), settings, root.transform) { SpriteCatalog = spriteCatalog };

                var slow = runtime.Play(new EffectRef(0), Vector3.zero, loop: true);
                var fast = runtime.Play(new EffectRef(0), Vector3.zero, loop: true);
                runtime.SetSpeed(fast, 2f);
                var slowInst = runtime.InstanceOf(slow); var fastInst = runtime.InstanceOf(fast);
                Assert.IsTrue(fastInst.SpritePart.CanDraw, fastInst.SpritePart.Problem);
                float t = slowInst.Length * 0.2f;                  // fast reaches 0.4 of the cycle, slow 0.2: compare at equal effect time below
                runtime.Tick(t);
                int fastFrame = fastInst.SpritePart.Actor.CurrentFrame;
                runtime.SetSpeed(slow, 2f);                        // speed is applied from here on; bring slow to the same Age
                runtime.Seek(slow, fastInst.Age);
                runtime.Tick(0f);
                Assert.AreEqual(fastFrame, slowInst.SpritePart.Actor.CurrentFrame, "same Age, same frame");
                Assert.That(fastInst.Age, Is.EqualTo(2f * t).Within(1e-4f), "speed 2 doubles the clock step on the real animation");
            }
            finally
            {
                runtime.Dispose();
                spriteCatalog.Release();
            }
        }
    }
}
