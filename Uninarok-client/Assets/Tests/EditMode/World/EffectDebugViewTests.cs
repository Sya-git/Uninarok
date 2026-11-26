using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Uninarok.Effects;
using Uninarok.Map;
using Uninarok.World.Dev;
using UnityEngine;

namespace Uninarok.World.Tests
{
    // SPEC-0012 FR-8 (PLAN-0012 4.8): the F11 list. FillDebug is checked over a runtime on a fake source with an effect playing;
    // the text is the pure function EffectDebugView.Write. The on-screen list needs a live map (owner gate T10.7).
    public class EffectDebugViewTests
    {
        sealed class FakeSource : IEffectAssetSource
        {
            public readonly Dictionary<string, EffectAsset> Assets = new Dictionary<string, EffectAsset>();
            public void Load(EffectAssetRef r, Action<EffectAsset> done) { Assets.TryGetValue(r.Guid, out var a); done(a); }
            public void Release(EffectAssetRef r) { }
        }

        const string Path3 = "texture/effect/three.str";
        const string PathQuad = "texture/effect/quad.str";

        readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();
        readonly List<GameObject> _objects = new List<GameObject>();
        EffectRuntime _runtime;

        T Make<T>() where T : ScriptableObject
        {
            var o = ScriptableObject.CreateInstance<T>();
            _created.Add(o);
            return o;
        }

        [SetUp]
        public void SetUp()
        {
            var settings = Make<EffectSettings>();
            settings.PlaceholderSeconds = 0.1f;
            settings.AuthoredFrameRate = 60f;
            settings.Shader = Shader.Find("Uninarok/RoEffect");

            var three = Make<EffectAsset>();     // 3 layers, 100 s
            three.Path = Path3; three.Kind = EffectKind.Str; three.Fps = 10f; three.FrameCount = 1000;
            three.Layers = new EffectAsset.LayerData[3];
            var quad = Make<EffectAsset>();      // not an STR: one part
            quad.Path = PathQuad; quad.Kind = EffectKind.Quad; quad.Length = 100f;
            quad.Authored = new AuthoredData();

            var index = Make<EffectCatalogIndex>();
            index.Keys = new[] { Path3, PathQuad };
            index.Assets = new[] { new EffectAssetRef { Guid = "3" }, new EffectAssetRef { Guid = "q" } };
            index.SourceMissing = new bool[2];
            var source = new FakeSource();
            source.Assets["3"] = three;
            source.Assets["q"] = quad;
            var catalog = EffectCatalog.Create(index, source);

            var root = new GameObject("EffectDebugViewTests root");
            _objects.Add(root);
            _runtime = new EffectRuntime(catalog, settings, root.transform, capacity: 8);
        }

        [TearDown]
        public void TearDown()
        {
            _runtime.Dispose();
            foreach (var g in _objects) if (g != null) UnityEngine.Object.DestroyImmediate(g);
            foreach (var o in _created) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            _objects.Clear();
            _created.Clear();
        }

        MapData Map()
        {
            const int n = 4;
            var map = Make<MapData>();
            var heights = new float[n * n * 4];
            map.Assign("small", n, n, 0.5f, new Vector2(10f, 20f), 1f, false, heights, new byte[n * n], new byte[n * n], new byte[n * n]);
            return map;
        }

        EffectHandle PlayThree(Transform unit)
        {
            _runtime.Catalog.TryResolve(Path3, out var three);
            return _runtime.PlayOn(three, unit, loop: true);
        }

        static string Text(List<EffectDebugInfo> infos)
        {
            var b = new StringBuilder();
            EffectDebugView.Write(b, infos);
            return b.ToString();
        }

        [Test]
        public void FillDebug_PlayingEffect_ReportsKeyPlaceParts_AndAge()
        {
            var unit = new GameObject("Hero");
            _objects.Add(unit);
            var h = PlayThree(unit.transform);
            _runtime.Tick(0.016f);
            _runtime.Tick(0.5f);
            Assert.IsTrue(h.IsValid);

            var infos = new List<EffectDebugInfo>();
            _runtime.FillDebug(infos);

            Assert.AreEqual(1, infos.Count);
            Assert.AreEqual(Path3, infos[0].Key);
            Assert.AreEqual(EffectPlace.Unit, infos[0].Place);
            Assert.AreEqual("Hero", infos[0].UnitName);
            Assert.AreEqual(3, infos[0].Parts, "one part per STR layer");
            Assert.AreEqual(0.516f, infos[0].Age, 1e-4f);
            Assert.IsTrue(infos[0].Loop);
            Assert.IsFalse(infos[0].Waiting);

            string text = Text(infos);
            StringAssert.Contains(Path3, text);
            StringAssert.Contains("unit Hero", text);
            StringAssert.Contains("parts 3", text);
            StringAssert.Contains("age 0.52 s", text);
        }

        [Test]
        public void FillDebug_CellPointAndUnknownPath_AreNamedAndCounted()
        {
            _runtime.Catalog.TryResolve(PathQuad, out var quad);
            _runtime.PlayAtCell(quad, Map(), 2, 3);
            _runtime.Play(quad, new Vector3(1f, 2f, 3f));
            _runtime.PlayPath("effect/GHOST.str", Vector3.zero);

            var infos = new List<EffectDebugInfo>();
            _runtime.FillDebug(infos);

            Assert.AreEqual(3, infos.Count);
            Assert.AreEqual(EffectPlace.Cell, infos[0].Place);
            Assert.AreEqual(2, infos[0].CellX);
            Assert.AreEqual(3, infos[0].CellY);
            Assert.AreEqual(1, infos[0].Parts, "a non-STR effect is one part");
            Assert.AreEqual(EffectPlace.Point, infos[1].Place);
            StringAssert.Contains("ghost", infos[2].Key, "an unknown path keeps its key");
            Assert.AreEqual(1, infos[2].Parts, "the placeholder is one part");

            string text = Text(infos);
            StringAssert.Contains("cell 2 3", text);
            StringAssert.Contains("point 1.0 2.0 3.0", text);
            StringAssert.StartsWith("effects 3\n", text);
        }

        [Test]
        public void FillDebug_AfterStopAll_IsEmpty_AndTheTextSaysSo()
        {
            _runtime.Catalog.TryResolve(PathQuad, out var quad);
            _runtime.Play(quad, Vector3.zero, loop: true);
            _runtime.Play(quad, Vector3.one, loop: true);
            _runtime.StopAll();

            var infos = new List<EffectDebugInfo>();
            _runtime.FillDebug(infos);

            Assert.AreEqual(0, infos.Count);
            Assert.AreEqual("no effects", Text(infos));
        }

        [Test]
        public void System_WithoutARuntime_FillsNothing_AndClearsTheList()
        {
            EffectSystem.ResetForTests();
            var infos = new List<EffectDebugInfo> { new EffectDebugInfo { Key = "stale" } };
            EffectSystem.FillDebug(infos);
            Assert.AreEqual(0, infos.Count);
        }

        [Test]
        public void Write_CapsTheLines_AndFormatsEachField()
        {
            var infos = new List<EffectDebugInfo>();
            for (int i = 0; i < EffectDebugView.MaxLines + 5; i++)
                infos.Add(new EffectDebugInfo { Key = "k" + i, Place = EffectPlace.Cell, CellX = i, CellY = 7, Parts = 2, Age = 1.5f, Loop = i == 0, Waiting = i == 1 });

            string text = Text(infos);

            StringAssert.StartsWith("effects 45\nk0  cell 0 7  parts 2  age 1.50 s loop\nk1  cell 1 7  parts 2  age 1.50 s loading\n", text);
            StringAssert.Contains("...\n", text);
            StringAssert.DoesNotContain("k" + EffectDebugView.MaxLines + "  ", text);
        }
    }
}
