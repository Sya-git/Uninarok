using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Uninarok.Catalog;
using Uninarok.Units;
using UnityEngine;
using UnityEngine.TestTools;

namespace Uninarok.Effects.PlayTests
{
    // SPEC-0012 FR-6, FR-7 (plan 4.5, EffectPlace.Unit): a warp entry played on a walking poring UnitView follows the unit.
    // The effect is the project's authored warp asset through the AssetDatabase; its textures are empty until T6.7, so the
    // placeholder texture draws, which is fine for placement.
    [Category("NeedsData")]
    public class EffectOnUnitPlayTests
    {
        const string WarpPath = "texture/effect/_authored/warp_portal/ring_outer.cylinder";
        const string WarpAsset = "Assets/Data/Effects/Authored/" + WarpPath + ".asset";

        sealed class AssetDatabaseSource : IEffectAssetSource
        {
            public void Load(EffectAssetRef r, Action<EffectAsset> done)
                => done(UnityEditor.AssetDatabase.LoadAssetAtPath<EffectAsset>(UnityEditor.AssetDatabase.GUIDToAssetPath(r.Guid)));
            public void Release(EffectAssetRef r) { }
        }

        AddressablesUnitAssetSource _source;
        UnitLoader _loader;
        UnitView _unit;
        Camera _camera;
        EffectSettings _settings;
        EffectCatalogIndex _index;
        GameObject _root;
        EffectRuntime _runtime;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            if (UnityEditor.AssetDatabase.LoadMainAssetAtPath(SpriteCatalogKeys.Index(SpriteCatalogCategory.Bodies)) == null)
                Assert.Ignore("NeedsData: the sprite catalog is not imported (Tools/Ragnarok/Import Catalog...)");
            if (UnityEditor.AssetDatabase.LoadAssetAtPath<EffectAsset>(WarpAsset) == null)
                Assert.Ignore("NeedsData: the authored warp asset is missing: " + WarpAsset);

            bool done = false;
            string error = null;
            AddressablesUnitAssetSource.LoadAsync((s, e) => { _source = s; error = e; done = true; });
            for (int i = 0; i < 1800 && !done; i++) yield return null;
            if (_source == null) Assert.Ignore("NeedsData: unit assets did not load: " + error);

            _settings = ScriptableObject.CreateInstance<EffectSettings>();
            _settings.PlaceholderSeconds = 0.25f;
            _settings.AuthoredFrameRate = 60f;
            _settings.Shader = Shader.Find("Uninarok/RoEffect");

            _index = ScriptableObject.CreateInstance<EffectCatalogIndex>();
            _index.Keys = new[] { WarpPath };
            _index.Assets = new[] { new EffectAssetRef { Guid = UnityEditor.AssetDatabase.AssetPathToGUID(WarpAsset) } };
            _index.SourceMissing = new[] { false };
            _root = new GameObject("EffectOnUnitPlayTestsRoot");
            _runtime = new EffectRuntime(EffectCatalog.Create(_index, new AssetDatabaseSource()), _settings, _root.transform);

            _camera = new GameObject("Camera").AddComponent<Camera>();
            _camera.transform.rotation = Quaternion.Euler(50f, 0f, 0f);
            UnitSystem.Camera = _camera;
            _loader = new UnitLoader(_source);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _runtime?.Dispose();
            if (_root != null) UnityEngine.Object.Destroy(_root);
            if (_unit != null) UnityEngine.Object.Destroy(_unit.gameObject);
            UnitSystem.Camera = null;
            if (_camera != null) UnityEngine.Object.Destroy(_camera.gameObject);
            if (_settings != null) UnityEngine.Object.DestroyImmediate(_settings);
            if (_index != null) UnityEngine.Object.DestroyImmediate(_index);
            yield return null;
            _source?.Dispose();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator WarpEntry_PlayedOnAWalkingPoring_FollowsTheUnitEveryFrame()
        {
            _unit = UnitView.Create(_loader, null);
            _unit.SetLook(UnitLook.ForJob(1002));   // poring
            for (int i = 0; i < 600 && !_unit.Loaded; i++) yield return null;
            if (!_unit.Loaded) Assert.Ignore("NeedsData: the poring did not load");
            _unit.Play(MonsterAnimation.Walk);

            _unit.transform.position = new Vector3(10f, 0f, 10f);
            var handle = _runtime.PlayOn(new EffectRef(0), _unit.transform, true);
            Assert.That(handle.IsValid, Is.True);
            var inst = _runtime.InstanceOf(handle);
            Assert.That(inst, Is.Not.Null);
            Assert.That(inst.Place, Is.EqualTo(EffectPlace.Unit));

            // walk 6 cells (cell = 0.5 units) at 0.2 s per cell, ticking the runtime once per frame
            const float cell = 0.5f, secondsPerCell = 0.2f;
            int cells = 6, frames = 0;
            Vector3 start = _unit.transform.position;
            float walkTime = cells * secondsPerCell;
            for (float t = 0f; t < walkTime; t += Time.deltaTime)
            {
                float f = Mathf.Min(t / walkTime, 1f);
                _unit.transform.position = start + new Vector3(cells * cell * f, 0f, cells * cell * f * 0.5f);
                _runtime.Tick(Time.deltaTime);
                yield return null;
                frames++;
                Assert.That(_runtime.IsActive(handle), Is.True, "still active at frame " + frames);
                Assert.That(Vector3.Distance(inst.Object.transform.position, _unit.transform.position), Is.LessThan(1e-3f),
                    "the effect follows the unit at frame " + frames);
            }
            Assert.That(frames, Is.GreaterThan(3));
            Assert.That(Vector3.Distance(_unit.transform.position, start), Is.GreaterThan(cells * cell * 0.9f), "the unit moved");
            Assert.That(inst.Position.x, Is.EqualTo(_unit.transform.position.x).Within(1e-3f));
        }
    }
}
