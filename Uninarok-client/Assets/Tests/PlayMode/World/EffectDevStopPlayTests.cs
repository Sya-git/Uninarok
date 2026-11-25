using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Uninarok.Effects;
using Uninarok.Map;
using Uninarok.World.Dev;
using UnityEngine;
using UnityEngine.TestTools;

namespace Uninarok.World.PlayTests
{
    // SPEC-0012 FR-8 (PLAN-0012 6): two looped effects started through /effect are really active in the EffectSystem;
    // /effect stop leaves none and the F11 list source (FillDebug) empty. Fake catalog source, no real catalog.
    public class EffectDevStopPlayTests
    {
        sealed class FakeSource : IEffectAssetSource
        {
            public readonly Dictionary<string, EffectAsset> Assets = new Dictionary<string, EffectAsset>();

            public void Load(EffectAssetRef r, Action<EffectAsset> done)
            {
                Assets.TryGetValue(r.Guid, out var a);
                done(a);
            }

            public void Release(EffectAssetRef r) { }
        }

        // A backend over the real EffectSystem with a test map and a stand-in character.
        sealed class SystemBackend : IEffectBackend
        {
            readonly MapData _map;
            readonly Transform _self;

            internal SystemBackend(MapData map, Transform self) { _map = map; _self = self; }

            public bool IsReady => true;
            public bool IsLoading => false;
            public void BeginLoad(Action<string> done) => done(null);
            public bool HasCharacter => _self != null;
            public bool HasMap => _map != null;
            public bool InMap(int x, int y) => _map.InBounds(x, y);
            public bool Resolve(string path, out string key) { key = path; return EffectSystem.Catalog.TryResolve(path, out _); }
            public EffectHandle PlayOnSelf(string path, bool loop) => EffectSystem.PlayOn(path, _self, loop);
            public EffectHandle PlayAtCell(string path, int x, int y, bool loop) => EffectSystem.PlayAtCell(path, _map, x, y, loop);
            public void Stop(EffectHandle handle) => EffectSystem.Stop(handle);
            public void StopAll() => EffectSystem.StopAll();
        }

        const string LoopPath = "texture/effect/loop.str";

        readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();

        T Make<T>() where T : ScriptableObject
        {
            var o = ScriptableObject.CreateInstance<T>();
            created.Add(o);
            return o;
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            EffectSystem.ResetForTests();
            yield return null;
            var settings = Make<EffectSettings>();
            settings.PlaceholderSeconds = 0.1f;
            settings.AuthoredFrameRate = 60f;
            settings.Shader = Shader.Find("Uninarok/RoEffect");

            var asset = Make<EffectAsset>();
            asset.Path = LoopPath; asset.Kind = EffectKind.Str; asset.Fps = 20f; asset.FrameCount = 2;   // 0.1 s: only a loop keeps it alive
            var index = Make<EffectCatalogIndex>();
            index.Keys = new[] { asset.Path };
            index.Assets = new[] { new EffectAssetRef { Guid = "a" } };
            index.SourceMissing = new bool[1];
            var source = new FakeSource();
            source.Assets["a"] = asset;

            EffectSystem.Settings = settings;
            EffectSystem.Catalog = EffectCatalog.Create(index, source);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            EffectSystem.ResetForTests();
            foreach (var o in created) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            created.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator EffectStop_AfterTwoLoopedPlays_LeavesNoneActive_AndTheDebugListEmpty()
        {
            const int n = 4;
            var map = Make<MapData>();
            var heights = new float[n * n * 4];
            map.Assign("small", n, n, 0.5f, new Vector2(10f, 20f), 1f, false, heights, new byte[n * n], new byte[n * n], new byte[n * n]);
            var self = new GameObject("own character");
            try
            {
                var shown = new List<string>();
                var commands = new EffectDevCommands(new SystemBackend(map, self.transform), shown.Add);

                Assert.AreEqual("playing " + LoopPath, commands.Run(LoopPath + " loop"));
                Assert.AreEqual("playing " + LoopPath, commands.Run(LoopPath + " 1 2 loop"));
                Assert.AreEqual(2, commands.Started);

                // Frames pass, past the 0.1 s of a single cycle: only the loops keep them alive.
                float start = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - start < 0.4f) yield return null;
                var debug = new List<EffectDebugInfo>();
                EffectSystem.FillDebug(debug);
                Assert.AreEqual(2, EffectSystem.ActiveCount);
                Assert.AreEqual(2, debug.Count);

                Assert.AreEqual("stopped 2", commands.Run("stop"));

                EffectSystem.FillDebug(debug);
                Assert.AreEqual(0, EffectSystem.ActiveCount);
                Assert.AreEqual(0, debug.Count);
                Assert.AreEqual(0, commands.Started);
            }
            finally { UnityEngine.Object.DestroyImmediate(self); }
        }
    }
}
