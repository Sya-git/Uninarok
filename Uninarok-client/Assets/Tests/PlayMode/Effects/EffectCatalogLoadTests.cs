using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.TestTools;

namespace Uninarok.Effects.PlayTests
{
    // SPEC-0012 FR-5, FR-6 (plan 4.4): the catalog loads its index and its assets through Addressables, by GUID.
    // The fixture is built here (temporary assets and a temporary group) and removed in TearDown, so the project's settings stay clean.
    public class EffectCatalogLoadTests
    {
        const string Folder = "Assets/__EffectsTests";
        const string GroupName = "__effects-tests";
        const string IndexKey = "__effects-tests/_index";
        const string EffectKey = "effect/test/fixture.str";

        AddressableAssetSettings settings;
        string effectGuid;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            Assert.IsNotNull(settings, "the project has no Addressables settings");

            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "__EffectsTests");

            var effect = ScriptableObject.CreateInstance<EffectAsset>();
            effect.Path = EffectKey;
            effect.Kind = EffectKind.Sprite;
            effect.SpriteName = "fixture";
            AssetDatabase.CreateAsset(effect, Folder + "/fixture.asset");
            effectGuid = AssetDatabase.AssetPathToGUID(Folder + "/fixture.asset");

            var index = ScriptableObject.CreateInstance<EffectCatalogIndex>();
            index.Keys = new[] { EffectKey };
            index.Assets = new[] { new EffectAssetRef { Guid = effectGuid } };
            index.SourceMissing = new[] { false };
            AssetDatabase.CreateAsset(index, Folder + "/_index.asset");
            string indexGuid = AssetDatabase.AssetPathToGUID(Folder + "/_index.asset");

            var group = settings.FindGroup(GroupName) ?? settings.CreateGroup(GroupName, false, false, false, null, typeof(UnityEditor.AddressableAssets.Settings.GroupSchemas.BundledAssetGroupSchema));
            settings.CreateOrMoveEntry(indexGuid, group, false, false).SetAddress(IndexKey, false);
            settings.CreateOrMoveEntry(effectGuid, group, false, false);   // address = its path; the GUID is also a key
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var group = settings.FindGroup(GroupName);
            if (group != null) settings.RemoveGroup(group);
            AssetDatabase.DeleteAsset(Folder);
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
            AssetDatabase.SaveAssets();
            yield return null;
        }

        static IEnumerator Wait<T>(AsyncOperationHandle<T> h)
        {
            for (int i = 0; i < 600 && !h.IsDone; i++) yield return null;
            Assert.IsTrue(h.IsDone, "the Addressables operation did not finish");
        }

        // FR-5: the index loads through Addressables and resolves a key of the fixture.
        [UnityTest]
        public IEnumerator LoadAsync_ResolvesAKeyOfTheIndex()
        {
            var h = EffectCatalog.LoadAsync(IndexKey);
            yield return Wait(h);
            Assert.AreEqual(AsyncOperationStatus.Succeeded, h.Status, h.OperationException?.Message);
            var catalog = h.Result;
            Assert.AreEqual(1, catalog.Count);
            Assert.IsTrue(catalog.TryResolve(EffectKey, out var entry));
            Assert.AreEqual(EffectAssetState.NotLoaded, catalog.GetState(entry));
            catalog.Release();
            Assert.IsFalse(h.IsValid() && h.Status != AsyncOperationStatus.Succeeded);
            Addressables.Release(h);
        }

        // FR-5: an asset loads through the catalog (AddressablesEffectSource) by its GUID.
        [UnityTest]
        public IEnumerator RequestAsset_LoadsTheAssetByGuid()
        {
            var h = EffectCatalog.LoadAsync(IndexKey);
            yield return Wait(h);
            var catalog = h.Result;
            Assert.IsTrue(catalog.TryResolve(EffectKey, out var entry));

            EffectAsset got = null;
            bool called = false;
            catalog.RequestAsset(entry, a => { got = a; called = true; });
            for (int i = 0; i < 600 && !called; i++) yield return null;

            Assert.IsTrue(called);
            Assert.IsNotNull(got);
            Assert.AreEqual(EffectKey, got.Path);
            Assert.AreEqual(EffectAssetState.Loaded, catalog.GetState(entry));
            Assert.IsTrue(catalog.TryGetAsset(entry, out var again));
            Assert.AreSame(got, again);

            catalog.Release();
            Assert.AreEqual(EffectAssetState.NotLoaded, catalog.GetState(entry));
            Addressables.Release(h);
        }

        // FR-6: the source holds one handle per loaded asset and none after Release.
        [UnityTest]
        public IEnumerator Source_ReleaseLeavesNoHandle()
        {
            var source = new AddressablesEffectSource();
            var r = new EffectAssetRef { Guid = effectGuid };
            EffectAsset got = null;
            bool called = false;
            source.Load(r, a => { got = a; called = true; });
            Assert.AreEqual(1, source.HandleCount);
            for (int i = 0; i < 600 && !called; i++) yield return null;
            Assert.IsNotNull(got);

            source.Release(r);
            Assert.AreEqual(0, source.HandleCount);
        }

        [UnityTest]
        public IEnumerator Source_UnknownGuidCompletesWithNullAndHoldsNoHandle()
        {
            var source = new AddressablesEffectSource();
            bool called = false;
            EffectAsset got = null;
            LogAssert.ignoreFailingMessages = true;
            source.Load(new EffectAssetRef { Guid = "00000000000000000000000000000000" }, a => { got = a; called = true; });
            for (int i = 0; i < 600 && !called; i++) yield return null;
            LogAssert.ignoreFailingMessages = false;
            Assert.IsTrue(called);
            Assert.IsNull(got);
            Assert.AreEqual(0, source.HandleCount);
        }

        [UnityTest]
        public IEnumerator LoadAsync_MissingIndexFails()
        {
            LogAssert.ignoreFailingMessages = true;
            var h = EffectCatalog.LoadAsync("__effects-tests/no-such-index");
            yield return Wait(h);
            LogAssert.ignoreFailingMessages = false;
            Assert.AreEqual(AsyncOperationStatus.Failed, h.Status);
            if (h.IsValid()) Addressables.Release(h);
        }
    }
}
