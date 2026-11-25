using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Uninarok.Catalog;
using Uninarok.Effects;
using Uninarok.Map;
using Uninarok.World.Dev;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.TestTools;

namespace Uninarok.World.PlayTests
{
    // SPEC-0012 FR-6, FR-8 (T7.3): the real /effect commands over a real EffectSystem with the REAL catalog index (T6.9),
    // the real EffectSettings and the real effect sprite catalog, loaded through Addressables the way WorldEffectBackend does
    // (WorldEffectBackend itself needs a live WorldBootstrap, so its load steps are replicated; the only difference is that
    // the sprite material is not set, as it comes from the world).
    public class EffectDevRealCatalogPlayTests
    {
        const string SettingsKey = "Assets/Settings/Effects/EffectSettings.asset";
        const string Firewall = "texture/effect/firewall.str";

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
            public bool Resolve(string path, out string key)
            {
                key = path;
                if (!EffectSystem.Catalog.TryResolve(path, out _)) return false;
                if (EffectPath.TryNormalize(path, out var normal)) key = normal;
                return true;
            }
            public EffectHandle PlayOnSelf(string path, bool loop) => EffectSystem.PlayOn(path, _self, loop);
            public EffectHandle PlayAtCell(string path, int x, int y, bool loop) => EffectSystem.PlayAtCell(path, _map, x, y, loop);
            public void Stop(EffectHandle handle) => EffectSystem.Stop(handle);
            public void StopAll() => EffectSystem.StopAll();
        }

        AsyncOperationHandle<EffectCatalog> catalogHandle;
        AsyncOperationHandle<EffectSettings> settingsHandle;
        AsyncOperationHandle<SpriteCatalog> spritesHandle;
        MapData map;
        GameObject self;

        static IEnumerator Wait<T>(AsyncOperationHandle<T> h)
        {
            for (int i = 0; i < 1200 && !h.IsDone; i++) yield return null;
            Assert.IsTrue(h.IsDone, "the Addressables operation did not finish");
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            EffectSystem.ResetForTests();
            yield return null;

            catalogHandle = EffectCatalog.LoadAsync();
            settingsHandle = Addressables.LoadAssetAsync<EffectSettings>(SettingsKey);
            spritesHandle = SpriteCatalog.LoadAsync(SpriteCatalogCategory.Effects);
            yield return Wait(catalogHandle);
            yield return Wait(settingsHandle);
            yield return Wait(spritesHandle);
            Assert.AreEqual(AsyncOperationStatus.Succeeded, catalogHandle.Status, "catalog: " + catalogHandle.OperationException?.Message);
            Assert.AreEqual(AsyncOperationStatus.Succeeded, settingsHandle.Status, "settings: " + settingsHandle.OperationException?.Message);
            if (spritesHandle.Status == AsyncOperationStatus.Succeeded) EffectSystem.SpriteCatalog = spritesHandle.Result;
            EffectSystem.Settings = settingsHandle.Result;
            EffectSystem.Catalog = catalogHandle.Result;

            const int n = 16;
            map = ScriptableObject.CreateInstance<MapData>();
            map.Assign("small", n, n, 0.5f, new Vector2(10f, 20f), 1f, false, new float[n * n * 4], new byte[n * n], new byte[n * n], new byte[n * n]);
            self = new GameObject("own character");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            EffectSystem.ResetForTests();
            if (self != null) UnityEngine.Object.DestroyImmediate(self);
            if (map != null) UnityEngine.Object.DestroyImmediate(map);
            if (catalogHandle.IsValid()) { catalogHandle.Result?.Release(); Addressables.Release(catalogHandle); }
            if (settingsHandle.IsValid()) Addressables.Release(settingsHandle);
            if (spritesHandle.IsValid()) Addressables.Release(spritesHandle);
            yield return null;
        }

        static IEnumerator Frames(float seconds)
        {
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < seconds) yield return null;
        }

        // FR-8: the Done-when of T7.3 against the real catalog: firewall plays, an unknown path says so, stop clears.
        [UnityTest]
        public IEnumerator Effect_RealCatalog_FirewallPlays_UnknownPathMessages_StopClears()
        {
            Assert.IsTrue(EffectSystem.Catalog.TryResolve(Firewall, out _), "firewall.str is not in the real index (" + EffectSystem.Catalog.Count + " keys)");
            LogAssert.ignoreFailingMessages = true;   // the player logs warnings for the placeholder and for a missing material; the checks are on the results
            try
            {
                var shown = new List<string>();
                var commands = new EffectDevCommands(new SystemBackend(map, self.transform), shown.Add);

                Assert.AreEqual("playing " + Firewall, commands.Run(Firewall + " 10 10 loop"));
                yield return Frames(1.0f);
                var debug = new List<EffectDebugInfo>();
                EffectSystem.FillDebug(debug);
                Debug.Log("[T7.3] real catalog keys=" + EffectSystem.Catalog.Count + " active=" + EffectSystem.ActiveCount + " debugEntries=" + debug.Count
                    + (debug.Count > 0 ? " parts=" + debug[0].Parts + " path=" + debug[0].Key : ""));
                Assert.AreEqual(1, EffectSystem.ActiveCount);
                Assert.AreEqual(1, debug.Count);
                Assert.AreEqual(11, debug[0].Parts, "part count of firewall.str");

                string unknown = commands.Run("nosuch/path.str");
                StringAssert.StartsWith("unknown effect path: nosuch/path.str", unknown);
                Assert.AreEqual(2, commands.Started);   // the placeholder plays (FR-6)
                yield return null;
                Assert.AreEqual(2, EffectSystem.ActiveCount);

                Assert.AreEqual("stopped 2", commands.Run("stop"));
                EffectSystem.FillDebug(debug);
                Assert.AreEqual(0, EffectSystem.ActiveCount);
                Assert.AreEqual(0, debug.Count);
            }
            finally { LogAssert.ignoreFailingMessages = false; }
        }
    }
}
