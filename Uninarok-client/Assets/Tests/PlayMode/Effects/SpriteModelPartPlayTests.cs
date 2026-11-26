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
    // SPEC-0012 FR-4, FR-6, FR-9 (plan 4.5 "Parts", D14, D29): SpritePart (a pooled SpriteActor stepped from the runtime's tick)
    // and ModelPart (one prefab instance per pooled slot). Assets are built in code; the sprite animation comes from the
    // runtime's lookup hook (the catalog path is checked on real converted data, NeedsData). Ticks are by hand.
    public class SpriteModelPartPlayTests
    {
        const int SpriteOneShot = 0, SpriteAction1 = 1, SpriteUnknown = 2, ModelA = 3, ModelB = 4, ModelNone = 5;
        const float Step = 0.11f;     // 110 ms: lands inside a 100 ms frame, clear of float edges

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
        GameObject prefabA, prefabB;

        T Make<T>() where T : ScriptableObject
        {
            var o = ScriptableObject.CreateInstance<T>();
            created.Add(o);
            return o;
        }

        // Action 0: 4 frames of 100 ms (0.4 s). Action 1: 2 frames of 50 ms (0.1 s). One layer (slot 0) per frame.
        SpriteAnimation MakeAnimation()
        {
            var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            created.Add(texture);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 10f);
            created.Add(sprite);
            var a = Make<SpriteAnimation>();
            a.Sprites = new[] { sprite };
            a.PixelsPerUnit = 10f;
            a.FileActionCount = 2;
            a.Directional = false;
            a.ActionMap = new short[] { 0, 1 };
            a.Actions = new[]
            {
                new SpriteAnimation.ActionData { FirstFrame = 0, FrameCount = 4, FrameMs = 100f },
                new SpriteAnimation.ActionData { FirstFrame = 4, FrameCount = 2, FrameMs = 50f },
            };
            a.Frames = new SpriteAnimation.FrameData[6];
            a.Layers = new SpriteAnimation.LayerData[6];
            for (int i = 0; i < 6; i++)
            {
                a.Frames[i] = new SpriteAnimation.FrameData { FirstLayer = i, LayerCount = 1, EventId = -1 };
                a.Layers[i] = new SpriteAnimation.LayerData { Slot = 0, ScaleX = 1f, ScaleY = 1f, Tint = new Color32(255, 255, 255, 255) };
            }
            a.MaxLayers = 1;
            return a;
        }

        EffectAsset Entry(string path, EffectKind kind)
        {
            var a = Make<EffectAsset>();
            a.Path = path; a.Kind = kind;
            return a;
        }

        [SetUp]
        public void SetUp()
        {
            settings = Make<EffectSettings>();
            settings.PlaceholderSeconds = 0.25f;
            settings.AuthoredFrameRate = 60f;
            settings.Shader = Shader.Find("Uninarok/RoEffect");

            prefabA = new GameObject("PrefabA"); created.Add(prefabA);
            prefabB = new GameObject("PrefabB"); created.Add(prefabB);

            var s0 = Entry("sprite/이팩트/fake0.act", EffectKind.Sprite); s0.SpriteName = "fake"; s0.SpriteAction = 0;
            var s1 = Entry("sprite/이팩트/fake1.act", EffectKind.Sprite); s1.SpriteName = "fake"; s1.SpriteAction = 1;
            var su = Entry("sprite/이팩트/nope.act", EffectKind.Sprite); su.SpriteName = "nope"; su.SpriteAction = 0;
            var ma = Entry("model/a.rsm", EffectKind.Model); ma.Model = prefabA; ma.Length = 0.5f;
            var mb = Entry("model/b.rsm", EffectKind.Model); mb.Model = prefabB; mb.Length = 0.5f;
            var mn = Entry("model/none.rsm", EffectKind.Model); mn.Length = 0.5f;

            var assets = new[] { s0, s1, su, ma, mb, mn };
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
            root = new GameObject("SpriteModelPartPlayTestsRoot");
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

        static EffectRef Ref(int i) => new EffectRef(i);

        // ---- SpritePart (FR-4, FR-6, D14, D29) ----

        [Test]
        public void Sprite_PlaysThroughAPooledActor_AndAdvancesWithAge()
        {
            var h = runtime.Play(Ref(SpriteOneShot), Vector3.zero);
            var inst = runtime.InstanceOf(h);
            var actor = inst.SpritePart.Actor;

            Assert.IsTrue(inst.SpritePart.CanDraw, inst.SpritePart.Problem);
            Assert.That(inst.Length, Is.EqualTo(0.4f).Within(1e-5f), "the sprite action length is the cycle length");
            Assert.IsTrue(actor.Driven, "SpriteSystem does not advance it; the runtime's tick does");
            Assert.AreEqual(0, actor.CurrentFrame, "frame 0 is written at bind");

            runtime.Tick(Step);
            Assert.AreEqual(1, actor.CurrentFrame);
            Assert.That(inst.Age, Is.EqualTo(Step).Within(1e-6f));
            runtime.Tick(Step);
            Assert.AreEqual(2, actor.CurrentFrame);
        }

        [Test]
        public void Sprite_OneShot_EndsAtTheActionLength_AndTheActorIsDisabled()
        {
            var h = runtime.Play(Ref(SpriteOneShot), Vector3.zero);
            var actor = runtime.InstanceOf(h).SpritePart.Actor;

            for (int i = 0; i < 3; i++) runtime.Tick(Step);      // 0.33 s of 0.4 s
            Assert.IsTrue(runtime.IsActive(h));
            runtime.Tick(Step);                                   // 0.44 s
            Assert.IsFalse(runtime.IsActive(h));
            Assert.AreEqual(0, runtime.ActiveCount);
            Assert.IsFalse(actor.gameObject.activeSelf, "the pooled actor is returned (disabled)");
        }

        [Test]
        public void Sprite_Loop_WrapsWithTheAction()
        {
            var h = runtime.Play(Ref(SpriteOneShot), Vector3.zero, loop: true);
            var inst = runtime.InstanceOf(h);
            for (int i = 0; i < 4; i++) runtime.Tick(Step);       // 0.44 s: one cycle done, 0.04 s into the next
            Assert.IsTrue(runtime.IsActive(h));
            Assert.AreEqual(1, inst.Cycle);
            Assert.AreEqual(0, inst.SpritePart.Actor.CurrentFrame);
        }

        [Test]
        public void Sprite_Speed2_StepsTwiceAsFast()
        {
            var h = runtime.Play(Ref(SpriteOneShot), Vector3.zero);
            var actor = runtime.InstanceOf(h).SpritePart.Actor;
            runtime.SetSpeed(h, 2f);
            runtime.Tick(Step);                                   // 0.22 s of effect time: frame 2
            Assert.AreEqual(2, actor.CurrentFrame);
        }

        [Test]
        public void Sprite_Pause_HoldsTheFrame_AndSeekIsExact()
        {
            var h = runtime.Play(Ref(SpriteOneShot), Vector3.zero);
            var actor = runtime.InstanceOf(h).SpritePart.Actor;
            runtime.TimeScale = 0f;
            runtime.Tick(1f);
            Assert.AreEqual(0, actor.CurrentFrame, "paused");

            runtime.Seek(h, 0.25f);
            runtime.Tick(1f);
            Assert.AreEqual(2, actor.CurrentFrame, "seek 0.25 s is frame 2");
            runtime.Tick(1f);
            Assert.AreEqual(2, actor.CurrentFrame, "still held");
            runtime.Seek(h, 0.05f);
            runtime.Tick(1f);
            Assert.AreEqual(0, actor.CurrentFrame, "seek back");
            runtime.Seek(h, 0.39f);
            runtime.Tick(1f);
            Assert.AreEqual(3, actor.CurrentFrame, "seek to the last frame");
        }

        [Test]
        public void Sprite_OtherAction_UsesItsOwnLength()
        {
            var h = runtime.Play(Ref(SpriteAction1), Vector3.zero);
            Assert.That(runtime.InstanceOf(h).Length, Is.EqualTo(0.1f).Within(1e-5f));
        }

        [Test]
        public void Sprite_ActorIsPooledPerSlot_AndReusedAfterStop()
        {
            var a = runtime.Play(Ref(SpriteOneShot), Vector3.zero);
            var actor = runtime.InstanceOf(a).SpritePart.Actor;
            runtime.Stop(a);
            Assert.IsFalse(actor.gameObject.activeSelf);

            var b = runtime.Play(Ref(SpriteOneShot), Vector3.zero);
            Assert.AreEqual(a.Slot, b.Slot);
            Assert.AreSame(actor, runtime.InstanceOf(b).SpritePart.Actor, "the same actor");
            Assert.IsTrue(actor.gameObject.activeSelf);
            Assert.AreEqual(1, root.GetComponentsInChildren<SpriteActor>(true).Length, "no second actor was made");
        }

        [Test]
        public void Sprite_WithoutAnAnimation_CannotDraw_AndKeepsTime()
        {
            var h = runtime.Play(Ref(SpriteUnknown), Vector3.zero);
            var inst = runtime.InstanceOf(h);
            Assert.IsFalse(inst.SpritePart.CanDraw);
            Assert.IsNotNull(inst.SpritePart.Problem);
            Assert.That(inst.Length, Is.EqualTo(0.25f), "PlaceholderSeconds when the part cannot give a length");
            Assert.IsNull(inst.SpritePart.Actor, "no actor is made for a sprite that is not found");
        }

        [Test]
        public void Sprite_WithoutACatalog_CannotDraw()
        {
            runtime.SpriteAnimations.Resolver = null;
            var h = runtime.Play(Ref(SpriteOneShot), Vector3.zero);
            Assert.IsFalse(runtime.InstanceOf(h).SpritePart.CanDraw);
            Assert.IsNull(runtime.SpriteCatalog);
        }

        // ---- ModelPart (FR-4, FR-9) ----

        [Test]
        public void Model_IsInstantiatedOncePerSlot_AndReusedAfterStopAndPlay()
        {
            var a = runtime.Play(Ref(ModelA), Vector3.zero);
            var inst = runtime.InstanceOf(a);
            var model = inst.ModelPart.Model;
            Assert.IsNotNull(model);
            Assert.AreEqual(inst.Object.transform, model.transform.parent);
            Assert.IsTrue(model.activeSelf);
            Assert.That(inst.Length, Is.EqualTo(0.5f));

            runtime.Stop(a);
            Assert.IsFalse(model.activeSelf, "disabled with the instance");

            var b = runtime.Play(Ref(ModelA), Vector3.zero);
            Assert.AreEqual(a.Slot, b.Slot);
            Assert.AreSame(model, runtime.InstanceOf(b).ModelPart.Model, "the same prefab instance is reused");
            Assert.IsTrue(model.activeSelf);
            Assert.AreEqual(1, runtime.InstanceOf(b).Object.transform.childCount, "the instance count stays 1");
        }

        [Test]
        public void Model_OneShot_EndsAtItsLength()
        {
            var h = runtime.Play(Ref(ModelA), Vector3.zero);
            var model = runtime.InstanceOf(h).ModelPart.Model;
            for (int i = 0; i < 4; i++) runtime.Tick(Step);       // 0.44 s
            Assert.IsTrue(runtime.IsActive(h));
            runtime.Tick(Step);                                   // 0.55 s
            Assert.IsFalse(runtime.IsActive(h));
            Assert.IsFalse(model.activeSelf);
        }

        [Test]
        public void Model_DifferentPrefabOnTheSameSlot_ReplacesTheInstance()
        {
            var a = runtime.Play(Ref(ModelA), Vector3.zero);
            var first = runtime.InstanceOf(a).ModelPart.Model;
            runtime.Stop(a);

            var b = runtime.Play(Ref(ModelB), Vector3.zero);
            var second = runtime.InstanceOf(b).ModelPart.Model;
            Assert.AreEqual(a.Slot, b.Slot);
            Assert.AreNotSame(first, second);
            Assert.AreEqual("PrefabB(Clone)", second.name);
            Assert.IsTrue(second.activeSelf);
        }

        [Test]
        public void Model_WithoutAPrefab_CannotDraw()
        {
            var h = runtime.Play(Ref(ModelNone), Vector3.zero);
            var inst = runtime.InstanceOf(h);
            Assert.IsFalse(inst.ModelPart.CanDraw);
            Assert.IsNotNull(inst.ModelPart.Problem);
        }

        // ---- the catalog path, on converted data (NeedsData) ----

        // fireball: SPEC-0008 effect name; the effects index and its animation come from Addressables, nothing is imported here.
        [UnityTest]
        public IEnumerator Sprite_Fireball_PlaysThroughSpriteCatalog_WithTheAssetCountUnchanged()
        {
            if (UnityEditor.AssetDatabase.LoadMainAssetAtPath(SpriteCatalogKeys.Index(SpriteCatalogCategory.Effects)) == null)
                Assert.Ignore("NeedsData: the effect sprite catalog is not imported (SPEC-0008)");

            int before = UnityEditor.AssetDatabase.FindAssets("").Length;
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

            runtime.SpriteAnimations.Resolver = null;
            runtime.SpriteCatalog = spriteCatalog;
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

            var h = runtime.Play(new EffectRef(0), Vector3.zero);
            var inst = runtime.InstanceOf(h);
            Assert.IsTrue(inst.SpritePart.CanDraw, inst.SpritePart.Problem);
            Assert.Greater(inst.Length, 0f);
            runtime.Tick(inst.Length * 0.5f);
            Assert.IsTrue(runtime.IsActive(h));
            Assert.GreaterOrEqual(inst.SpritePart.Actor.CurrentFrame, 0);
            runtime.Tick(inst.Length);
            Assert.IsFalse(runtime.IsActive(h), "a one-shot ends at the action length");

            runtime.Dispose();
            spriteCatalog.Release();
            Assert.AreEqual(before, UnityEditor.AssetDatabase.FindAssets("").Length, "no asset was imported or created");
        }
    }
}
