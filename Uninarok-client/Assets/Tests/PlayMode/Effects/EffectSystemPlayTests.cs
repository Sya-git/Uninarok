using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Scripting.LifecycleManagement;
using Uninarok.Map;
using UnityEngine;
using UnityEngine.TestTools;

namespace Uninarok.Effects.PlayTests
{
    // SPEC-0012 FR-6, FR-9 (plan 4.5, 5): the EffectSystem static, its hidden driver and the by-path plays. The catalog is
    // built in code over a fake source; time passes through real frames (the driver ticks in LateUpdate).
    public class EffectSystemPlayTests
    {
        sealed class FakeSource : IEffectAssetSource
        {
            public readonly Dictionary<string, EffectAsset> Assets = new Dictionary<string, EffectAsset>();
            public int Released;

            public void Load(EffectAssetRef r, Action<EffectAsset> done)
            {
                Assets.TryGetValue(r.Guid, out var a);
                done(a);
            }

            public void Release(EffectAssetRef r) => Released++;
        }

        const string ShortPath = "texture/effect/short.str";
        const string LongPath = "texture/effect/long.str";

        readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();
        FakeSource source;
        EffectCatalog catalog;
        EffectSettings settings;

        T Make<T>() where T : ScriptableObject
        {
            var o = ScriptableObject.CreateInstance<T>();
            created.Add(o);
            return o;
        }

        EffectCatalog MakeCatalog(FakeSource src)
        {
            var shortStr = Make<EffectAsset>();      // 0.1 s
            shortStr.Path = ShortPath; shortStr.Kind = EffectKind.Str; shortStr.Fps = 20f; shortStr.FrameCount = 2;
            var longStr = Make<EffectAsset>();       // 100 s: outlives every test
            longStr.Path = LongPath; longStr.Kind = EffectKind.Str; longStr.Fps = 10f; longStr.FrameCount = 1000;
            var index = Make<EffectCatalogIndex>();
            index.Keys = new[] { shortStr.Path, longStr.Path };
            index.Assets = new[] { new EffectAssetRef { Guid = "s" }, new EffectAssetRef { Guid = "l" } };
            index.SourceMissing = new bool[2];
            src.Assets["s"] = shortStr;
            src.Assets["l"] = longStr;
            return EffectCatalog.Create(index, src);
        }

        [SetUp]
        public void SetUp()
        {
            EffectSystem.ResetForTests();
            source = new FakeSource();
            settings = Make<EffectSettings>();
            settings.PlaceholderSeconds = 0.1f;
            settings.AuthoredFrameRate = 60f;
            settings.Shader = Shader.Find("Uninarok/RoEffect");
            catalog = MakeCatalog(source);
            EffectSystem.Settings = settings;
            EffectSystem.Catalog = catalog;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            EffectSystem.ResetForTests();
            foreach (var o in created) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            created.Clear();
            yield return null;      // the destroyed driver is gone before the next test
        }

        static IEnumerator WaitUntilInactive(EffectHandle h, float maxSeconds = 5f)
        {
            float start = Time.realtimeSinceStartup;
            while (EffectSystem.IsActive(h) && Time.realtimeSinceStartup - start < maxSeconds) yield return null;
        }

        [Test]
        public void IsAutoStaticsCleanup()
        {
            Assert.IsNotNull(typeof(EffectSystem).GetCustomAttribute<AutoStaticsCleanupAttribute>());
        }

        [Test]
        public void Play_ByPath_ReturnsAValidHandle_AndCountsOne()
        {
            Assert.AreEqual(0, EffectSystem.ActiveCount);
            var h = EffectSystem.Play(LongPath, new Vector3(1f, 2f, 3f));

            Assert.IsTrue(h.IsValid);
            Assert.IsTrue(EffectSystem.IsActive(h));
            Assert.AreEqual(1, EffectSystem.ActiveCount);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), EffectSystem.Runtime.InstanceOf(h).Position);
        }

        [UnityTest]
        public IEnumerator Play_OneShot_EndsAfterItsLength_AsFramesPass()
        {
            var h = EffectSystem.Play(ShortPath, Vector3.zero);
            Assert.IsTrue(EffectSystem.IsActive(h));

            yield return WaitUntilInactive(h);

            Assert.IsFalse(EffectSystem.IsActive(h));
            Assert.AreEqual(0, EffectSystem.ActiveCount);
        }

        [UnityTest]
        public IEnumerator Play_Looped_KeepsPlayingAcrossFrames()
        {
            var h = EffectSystem.Play(ShortPath, Vector3.zero, loop: true);
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < 0.4f) yield return null;   // 4 cycles
            Assert.IsTrue(EffectSystem.IsActive(h));
            EffectSystem.Stop(h);
            Assert.IsFalse(EffectSystem.IsActive(h));
        }

        [Test]
        public void PlayOn_FollowsTheUnit_AndAUnitless_PlaysNothing()
        {
            var unit = new GameObject("unit");
            try
            {
                unit.transform.position = new Vector3(1f, 0f, 1f);
                var h = EffectSystem.PlayOn(LongPath, unit.transform, loop: true);
                Assert.IsTrue(h.IsValid);
                Assert.AreEqual(EffectPlace.Unit, EffectSystem.Runtime.InstanceOf(h).Place);

                unit.transform.position = new Vector3(4f, 0f, 5f);
                EffectSystem.Tick(0.01f);
                Assert.AreEqual(new Vector3(4f, 0f, 5f), EffectSystem.Runtime.InstanceOf(h).Position);

                Assert.IsFalse(EffectSystem.PlayOn(LongPath, null).IsValid);
            }
            finally { UnityEngine.Object.DestroyImmediate(unit); }
        }

        [Test]
        public void PlayAtCell_UsesTheMapCellCentre()
        {
            const int n = 4;
            var map = Make<MapData>();
            var heights = new float[n * n * 4];
            for (int i = 0; i < heights.Length; i++) heights[i] = 0.75f;
            map.Assign("small", n, n, 0.5f, new Vector2(10f, 20f), 1f, false, heights, new byte[n * n], new byte[n * n], new byte[n * n]);

            var h = EffectSystem.PlayAtCell(LongPath, map, 2, 3);

            Assert.IsTrue(h.IsValid);
            var inst = EffectSystem.Runtime.InstanceOf(h);
            Assert.AreEqual(EffectPlace.Cell, inst.Place);
            Assert.AreEqual(map.CellCenter(2, 3), inst.Position);
            Assert.IsFalse(EffectSystem.PlayAtCell(LongPath, null, 0, 0).IsValid);
        }

        [Test]
        public void SetSpeed_ChangesTheInstanceSpeed()
        {
            var h = EffectSystem.Play(LongPath, Vector3.zero);
            EffectSystem.SetSpeed(h, 2f);
            Assert.AreEqual(2f, EffectSystem.Runtime.SpeedOf(h));
            EffectSystem.SetSpeed(default, 3f);     // a default handle is ignored
        }

        [Test]
        public void Stop_EndsOne_AndStopAll_EndsEverything()
        {
            var a = EffectSystem.Play(LongPath, Vector3.zero);
            var b = EffectSystem.Play(LongPath, Vector3.one);
            var c = EffectSystem.Play(LongPath, Vector3.up);
            Assert.AreEqual(3, EffectSystem.ActiveCount);

            EffectSystem.Stop(a);
            Assert.IsFalse(EffectSystem.IsActive(a));
            Assert.AreEqual(2, EffectSystem.ActiveCount);

            EffectSystem.StopAll();
            Assert.AreEqual(0, EffectSystem.ActiveCount);
            Assert.IsFalse(EffectSystem.IsActive(b));
            Assert.IsFalse(EffectSystem.IsActive(c));
        }

        [Test]
        public void BeforeTheFirstPlay_Queries_AreSafe_AndNoDriverExists()
        {
            Assert.AreEqual(0, EffectSystem.ActiveCount);
            Assert.IsFalse(EffectSystem.IsActive(default));
            EffectSystem.StopAll();
            EffectSystem.Stop(default);
            Assert.IsNull(EffectSystem.Driver);
            Assert.AreEqual(0, UnityEngine.Object.FindObjectsByType<EffectSystemDriver>(FindObjectsInactive.Include).Length);
        }

        [Test]
        public void Play_WithNoCatalog_ReturnsDefault_AndWarnsOnce()
        {
            EffectSystem.Catalog = null;
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("no Catalog"));

            Assert.IsFalse(EffectSystem.Play(LongPath, Vector3.zero).IsValid);
            Assert.IsFalse(EffectSystem.Play(LongPath, Vector3.zero).IsValid);
            Assert.IsFalse(EffectSystem.PlayOn(LongPath, null).IsValid);

            Assert.AreEqual(0, EffectSystem.ActiveCount);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Play_WithNoSettings_ReturnsDefault_AndWarnsOnce()
        {
            EffectSystem.Settings = null;
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("no Settings"));

            Assert.IsFalse(EffectSystem.Play(LongPath, Vector3.zero).IsValid);
            Assert.IsFalse(EffectSystem.Play(LongPath, Vector3.zero).IsValid);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void UnknownPath_PlaysThePlaceholder()
        {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("unknown path.*ghost"));

            var h = EffectSystem.Play("effect/GHOST.str", Vector3.zero);

            Assert.IsTrue(h.IsValid);
            Assert.AreEqual(1, EffectSystem.ActiveCount);
            Assert.IsInstanceOf<PlaceholderPart>(EffectSystem.Runtime.InstanceOf(h).Part);
        }

        [UnityTest]
        public IEnumerator Driver_ExistsOnce_IsHidden_AndSurvivesSceneChange()
        {
            EffectSystem.Play(LongPath, Vector3.zero);
            EffectSystem.Play(LongPath, Vector3.zero);
            EffectSystem.Play(ShortPath, Vector3.zero);
            yield return null;

            var drivers = UnityEngine.Object.FindObjectsByType<EffectSystemDriver>(FindObjectsInactive.Include);
            Assert.AreEqual(1, drivers.Length, "created on the first play, not again");
            var go = drivers[0].gameObject;
            Assert.AreEqual("DontDestroyOnLoad", go.scene.name);
            Assert.IsTrue((go.hideFlags & HideFlags.HideInHierarchy) != 0);
            Assert.IsNull(go.transform.parent);
            Assert.AreEqual(go.transform, EffectSystem.Runtime.InstanceOf(EffectSystem.Play(LongPath, Vector3.zero)).Object.transform.parent,
                "the pooled effect objects live under the one root");
            var order = (DefaultExecutionOrder)Attribute.GetCustomAttribute(typeof(EffectSystemDriver), typeof(DefaultExecutionOrder));
            Assert.AreEqual(950, order.order);

            var scene = UnityEngine.SceneManagement.SceneManager.CreateScene("EffectSystemPlayTestsScene");
            yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(scene);
            Assert.IsTrue(go != null, "survives a scene unload");
            Assert.AreEqual(4, EffectSystem.ActiveCount);
        }

        [Test]
        public void ReplacingTheCatalog_StopsEverything_ThenReleasesTheOldOne()
        {
            var h = EffectSystem.Play(LongPath, Vector3.zero);
            Assert.AreEqual(1, EffectSystem.ActiveCount);
            Assert.AreEqual(0, source.Released);

            var otherSource = new FakeSource();
            var other = MakeCatalog(otherSource);
            EffectSystem.Catalog = other;

            Assert.AreEqual(0, EffectSystem.ActiveCount);
            Assert.IsFalse(EffectSystem.IsActive(h));
            Assert.Greater(source.Released, 0, "the old catalog released its loaded assets");
            Assert.AreEqual(0, otherSource.Released);

            Assert.IsTrue(EffectSystem.Play(LongPath, Vector3.zero).IsValid, "plays through the new catalog");
            Assert.AreEqual(1, EffectSystem.ActiveCount);
        }

        [Test]
        public void SpriteWiring_ReachesTheRuntime()
        {
            var mat = new Material(Shader.Find("Sprites/Default"));
            created.Add(mat);
            EffectSystem.SpriteMaterial = mat;
            EffectSystem.Play(LongPath, Vector3.zero);
            Assert.AreSame(mat, EffectSystem.Runtime.SpriteMaterial, "set before the runtime exists");

            EffectSystem.SpriteMaterial = null;
            Assert.IsNull(EffectSystem.Runtime.SpriteMaterial, "set after");
        }

        [UnityTest]
        public IEnumerator DestroyingTheDriver_DisposesTheRuntime_AndANewPlayRecreatesIt()
        {
            EffectSystem.Play(LongPath, Vector3.zero);
            UnityEngine.Object.Destroy(EffectSystem.Driver.gameObject);
            yield return null;

            Assert.IsNull(EffectSystem.Runtime);
            Assert.AreEqual(0, EffectSystem.ActiveCount);
            Assert.IsTrue(EffectSystem.Play(LongPath, Vector3.zero).IsValid);
            Assert.AreEqual(1, EffectSystem.ActiveCount);
        }
    }
}
